using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scrapers.Xigua.Entity;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scrapers.Xigua
{
    public class XiguaApi : AbstractApi
    {
        private static readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(500);

        private const string MOBILE_USER_AGENT =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 16_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Mobile/15E148 Safari/604.1";

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public XiguaApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("XiguaApi"), httpClient)
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MOBILE_USER_AGENT);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");
        }

        /// <summary>
        /// Search videos by keyword on Xigua mobile site
        /// </summary>
        public async Task<List<XiguaSearchItem>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<XiguaSearchItem>();
            }

            var cacheKey = $"xigua_search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<XiguaSearchItem>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            try
            {
                await this.LimitRequestFrequently().ConfigureAwait(false);

                var encodedKeyword = HttpUtility.UrlEncode(keyword);
                var url = $"https://m.ixigua.com/s/{encodedKeyword}";
                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = ParseSearchResults(html);

                _memoryCache.Set<List<XiguaSearchItem>>(cacheKey, result, expiredOption);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn("XiguaApi.SearchAsync - 西瓜搜索失败，按空结果降级。keyword={0}, error={1}", keyword, ex.Message);
                return new List<XiguaSearchItem>();
            }
        }

        /// <summary>
        /// Get video duration from detail page
        /// </summary>
        public async Task<int> GetVideoDurationAsync(string itemId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            var cacheKey = $"xigua_duration_{itemId}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<int>(cacheKey, out var cachedDuration))
            {
                return cachedDuration;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            var url = $"https://m.ixigua.com/video/{itemId}";
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var duration = ParseVideoDuration(html);

            _memoryCache.Set<int>(cacheKey, duration, expiredOption);
            return duration;
        }

        /// <summary>
        /// Get danmu content for a video in 5-minute segments
        /// </summary>
        public async Task<List<XiguaComment>> GetDanmuContentAsync(string itemId, CancellationToken cancellationToken)
        {
            var danmuList = new List<XiguaComment>();
            if (string.IsNullOrEmpty(itemId))
            {
                return danmuList;
            }

            // Get video duration first
            var durationSeconds = await GetVideoDurationAsync(itemId, cancellationToken).ConfigureAwait(false);
            if (durationSeconds <= 0)
            {
                // Default to 2 hours if we can't determine duration
                durationSeconds = 7200;
                _logger.Warn("无法获取西瓜视频时长，使用默认值 {0} 秒。itemId={1}", durationSeconds, itemId);
            }

            // Fetch danmu in 300-second (5-minute) segments
            var segmentSize = 300;
            var startTime = 0;

            while (startTime < durationSeconds)
            {
                var endTime = Math.Min(startTime + segmentSize, durationSeconds);
                try
                {
                    _logger.Info($"正在下载西瓜视频弹幕分段: {startTime}-{endTime}s (itemId={itemId})");
                    var segmentUrl = $"https://ib.snssdk.com/vapp/danmaku/list/v1/?item_id={itemId}&start_time={startTime}&end_time={endTime}&format=json";

                    // Use JSON accept header for danmu API
                    var request = new HttpRequestMessage(HttpMethod.Get, segmentUrl);
                    request.Headers.Add("Accept", "application/json");
                    var segmentResponse = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    segmentResponse.EnsureSuccessStatusCode();

                    var jsonStr = await segmentResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(jsonStr))
                    {
                        var danmuResult = JsonSerializer.Deserialize<XiguaDanmuResult>(jsonStr, _jsonOptions);
                        if (danmuResult?.Data?.DanmakuList != null)
                        {
                            _logger.Info($"西瓜视频弹幕分段 {startTime}-{endTime}s 下载完成，获取到 {danmuResult.Data.DanmakuList.Count} 条弹幕。");
                            danmuList.AddRange(danmuResult.Data.DanmakuList);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("下载西瓜视频弹幕分段时发生错误。startTime: {0}, endTime: {1}", ex, startTime, endTime);
                }

                startTime = endTime;
                // Rate limit between segments
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }

            return danmuList;
        }

        /// <summary>
        /// Parse search results from HTML
        /// </summary>
        private List<XiguaSearchItem> ParseSearchResults(string html)
        {
            var result = new List<XiguaSearchItem>();
            if (string.IsNullOrEmpty(html))
            {
                return result;
            }

            // Try to extract video cards from the search page HTML
            // Look for video IDs and titles in the HTML content
            // Pattern: video card links like /video/{id} with titles
            var videoPattern = new Regex(@"href=""[^""]*?/video/(\d+)""[^>]*>[\s\S]*?class=""[^""]*title[^""]*""[^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            var matches = videoPattern.Matches(html);
            var seen = new HashSet<string>();

            foreach (Match match in matches)
            {
                var videoId = match.Groups[1].Value;
                var title = match.Groups[2].Value.Trim();
                title = HttpUtility.HtmlDecode(title);

                if (!string.IsNullOrEmpty(videoId) && !string.IsNullOrEmpty(title) && seen.Add(videoId))
                {
                    result.Add(new XiguaSearchItem
                    {
                        Id = videoId,
                        Title = title,
                        Category = "视频",
                    });
                }
            }

            // Fallback: try to extract from SSR data / JSON embedded in HTML
            if (result.Count == 0)
            {
                var ssrPattern = new Regex(@"""item_id""\s*:\s*""?(\d+)""?[\s\S]*?""title""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                var ssrMatches = ssrPattern.Matches(html);
                foreach (Match match in ssrMatches)
                {
                    var videoId = match.Groups[1].Value;
                    var title = match.Groups[2].Value.Trim();
                    title = HttpUtility.HtmlDecode(title);

                    if (!string.IsNullOrEmpty(videoId) && !string.IsNullOrEmpty(title) && seen.Add(videoId))
                    {
                        result.Add(new XiguaSearchItem
                        {
                            Id = videoId,
                            Title = title,
                            Category = "视频",
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Parse video duration from HTML detail page (in seconds)
        /// </summary>
        private int ParseVideoDuration(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return 0;
            }

            // Try to find duration in SSR data embedded in the page
            // Common patterns: "duration":123, "video_duration":123, "duration":"123"
            var durationPattern = new Regex(@"""(?:video_)?duration""\s*:\s*""?(\d+)""?", RegexOptions.IgnoreCase);
            var match = durationPattern.Match(html);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var duration))
            {
                return duration;
            }

            // Try MM:SS or HH:MM:SS format
            var timePattern = new Regex(@"""(?:video_)?duration""\s*:\s*""(\d+):(\d+):?(\d+)?""", RegexOptions.IgnoreCase);
            match = timePattern.Match(html);
            if (match.Success)
            {
                if (match.Groups[3].Success)
                {
                    // HH:MM:SS
                    int.TryParse(match.Groups[1].Value, out var h);
                    int.TryParse(match.Groups[2].Value, out var m);
                    int.TryParse(match.Groups[3].Value, out var s);
                    return h * 3600 + m * 60 + s;
                }
                else
                {
                    // MM:SS
                    int.TryParse(match.Groups[1].Value, out var m);
                    int.TryParse(match.Groups[2].Value, out var s);
                    return m * 60 + s;
                }
            }

            return 0;
        }

        protected new async Task LimitRequestFrequently()
        {
            await _rateLimiter.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastRequestTime;
                if (timeSinceLastRequest < _minInterval)
                {
                    await Task.Delay(_minInterval - timeSinceLastRequest).ConfigureAwait(false);
                }
                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
    }
}
