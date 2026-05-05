using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scrapers.Leshi.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scrapers.Leshi
{
    public class LeshiApi : AbstractApi
    {
        private static readonly SemaphoreSlim _leshiApiRateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastLeshiApiRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _leshiApiMinInterval = TimeSpan.FromMilliseconds(500);

        // Regex to extract data-info JSON from search result HTML
        private static readonly Regex _dataInfoRegex = new Regex(
            @"data-info=""({.*?})""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Regex to extract data-info with single quotes (alternative)
        private static readonly Regex _dataInfoRegex2 = new Regex(
            @"data-info='({.*?})'",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Regex to extract episode links from show pages
        private static readonly Regex _episodeLinkRegex = new Regex(
            @"<a[^>]*href=""[^""]*?/ptv/vplay/(\d+)\.html""[^>]*>(.*?)</a>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Regex to extract video duration from page
        private static readonly Regex _durationRegex = new Regex(
            @"""duration""\s*:\s*(\d+)",
            RegexOptions.Compiled);

        // Regex to extract vid from video page
        private static readonly Regex _vidRegex = new Regex(
            @"""vid""\s*:\s*(\d+)",
            RegexOptions.Compiled);

        // Regex to extract episode list from show page (JSON in script tags)
        private static readonly Regex _episodeListRegex = new Regex(
            @"""vids""\s*:\s*\[(.*?)\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Regex to extract vid and title pairs from episode data
        private static readonly Regex _episodeItemRegex = new Regex(
            @"\{[^}]*""vid""\s*:\s*(\d+)[^}]*""title""\s*:\s*""([^""]*?)""[^}]*\}",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Alternative: extract from HTML list items
        private static readonly Regex _episodeHtmlRegex = new Regex(
            @"<a[^>]*href=""/ptv/vplay/(\d+)\.html""[^>]*title=""([^""]*?)""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // JSONP callback stripper
        private static readonly Regex _jsonpRegex = new Regex(
            @"^[^(]*\((.*)\)\s*;?\s*$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public LeshiApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("LeshiApi"), httpClient)
        {
        }

        public async Task<List<LeshiSearchItem>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<LeshiSearchItem>();
            }

            var cacheKey = $"leshi_search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<LeshiSearchItem>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            var encodedKeyword = HttpUtility.UrlEncode(keyword);
            var url = $"https://so.le.com/s?wd={encodedKeyword}";

            var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            options.RequestContentType = null;
            options.AcceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            options.RequestHeaders["Referer"] = "https://www.le.com/";

            var result = new List<LeshiSearchItem>();
            try
            {
                using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                    {
                        var html = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(html))
                        {
                            result = ParseSearchResults(html);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "乐视搜索失败. keyword: {0}", keyword);
            }

            _memoryCache.Set(cacheKey, result, expiredOption);
            return result;
        }

        private List<LeshiSearchItem> ParseSearchResults(string html)
        {
            var items = new List<LeshiSearchItem>();

            // Try both quote styles for data-info attribute
            var matches = _dataInfoRegex.Matches(html);
            if (matches.Count == 0)
            {
                matches = _dataInfoRegex2.Matches(html);
            }

            foreach (Match match in matches)
            {
                try
                {
                    var jsonStr = HttpUtility.HtmlDecode(match.Groups[1].Value);
                    // 乐视返回的是JS对象字面量(如 {pid:'73868',type:'tv'})，不是标准JSON
                    // 需要将无引号的key加上双引号，将单引号value换成双引号
                    jsonStr = Regex.Replace(jsonStr, @"(\w+)\s*:", "\"$1\":");
                    jsonStr = jsonStr.Replace("'", "\"");
                    var item = JsonSerializer.Deserialize<LeshiSearchItem>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (item != null && !string.IsNullOrEmpty(item.Title))
                    {
                        items.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析乐视搜索结果data-info失败: {0}", match.Groups[1].Value);
                }
            }

            return items;
        }

        public async Task<List<LeshiEpisode>> GetEpisodesAsync(string aid, string contentType, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(aid))
            {
                return new List<LeshiEpisode>();
            }

            var cacheKey = $"leshi_episodes_{aid}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<List<LeshiEpisode>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            // Determine URL path based on content type
            var pathType = "tv";
            switch (contentType?.ToLower())
            {
                case "movie":
                    pathType = "movie";
                    break;
                case "cartoon":
                case "comic":
                    pathType = "comic";
                    break;
            }

            var url = $"https://www.le.com/{pathType}/{aid}.html";
            var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            options.RequestContentType = null;
            options.AcceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            options.RequestHeaders["Referer"] = "https://www.le.com/";

            var result = new List<LeshiEpisode>();
            try
            {
                using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                    {
                        var html = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(html))
                        {
                            result = ParseEpisodes(html);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取乐视剧集列表失败. aid: {0}", aid);
            }

            if (result.Count > 0)
            {
                _memoryCache.Set(cacheKey, result, expiredOption);
            }
            return result;
        }

        private List<LeshiEpisode> ParseEpisodes(string html)
        {
            var episodes = new List<LeshiEpisode>();

            // Try to extract from HTML links with vplay pattern
            var matches = _episodeHtmlRegex.Matches(html);
            foreach (Match match in matches)
            {
                var vid = match.Groups[1].Value;
                var title = HttpUtility.HtmlDecode(match.Groups[2].Value);

                if (long.TryParse(vid, out var vidLong) && !episodes.Any(e => e.Vid == vidLong))
                {
                    episodes.Add(new LeshiEpisode
                    {
                        Vid = vidLong,
                        Title = title
                    });
                }
            }

            // Fallback: try extracting from episode link pattern
            if (episodes.Count == 0)
            {
                var linkMatches = _episodeLinkRegex.Matches(html);
                foreach (Match match in linkMatches)
                {
                    var vid = match.Groups[1].Value;
                    var title = Regex.Replace(match.Groups[2].Value, @"<[^>]+>", "").Trim();
                    title = HttpUtility.HtmlDecode(title);

                    if (long.TryParse(vid, out var vidLong) && !episodes.Any(e => e.Vid == vidLong))
                    {
                        episodes.Add(new LeshiEpisode
                        {
                            Vid = vidLong,
                            Title = string.IsNullOrEmpty(title) ? $"第{episodes.Count + 1}集" : title
                        });
                    }
                }
            }

            return episodes;
        }

        public async Task<int> GetVideoDurationAsync(long vid, CancellationToken cancellationToken)
        {
            var cacheKey = $"leshi_duration_{vid}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60) };
            if (_memoryCache.TryGetValue<int>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            var url = $"https://www.le.com/ptv/vplay/{vid}.html";
            var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            options.RequestContentType = null;
            options.AcceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            options.RequestHeaders["Referer"] = "https://www.le.com/";

            var duration = 7200; // default 2 hours
            try
            {
                using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                    {
                        var html = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(html))
                        {
                            var match = _durationRegex.Match(html);
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var dur))
                            {
                                duration = dur;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取乐视视频时长失败. vid: {0}", vid);
            }

            _memoryCache.Set(cacheKey, duration, expiredOption);
            return duration;
        }

        public async Task<List<LeshiDanmuItem>> GetDanmuContentAsync(long vid, int durationSeconds, CancellationToken cancellationToken)
        {
            var danmuList = new List<LeshiDanmuItem>();

            // Leshi danmu is fetched in 300-second segments
            var segmentSize = 300;
            var timeBegin = 0;
            var maxDuration = durationSeconds > 0 ? durationSeconds : 7200;

            while (timeBegin < maxDuration)
            {
                var timeEnd = timeBegin + segmentSize;
                try
                {
                    _logger.Info("正在下载乐视弹幕分段: {0}-{1}s (vid={2})", timeBegin, timeEnd, vid);

                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var callbackName = $"vjs_{timestamp}";
                    var url = $"https://hd-my.le.com/danmu/list?vid={vid}&start={timeBegin}&end={timeEnd}&callback={callbackName}";
                    var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
                    options.RequestContentType = null;
                    options.AcceptHeader = "*/*";
                    options.RequestHeaders["Referer"] = "https://www.le.com/";

                    using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                    {
                        using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                        {
                            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(body))
                            {
                                // Strip JSONP wrapper
                                var json = StripJsonpCallback(body);
                                if (!string.IsNullOrEmpty(json))
                                {
                                    var result = JsonSerializer.Deserialize<LeshiDanmuResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                    if (result?.Data != null)
                                    {
                                        _logger.Info("乐视弹幕分段 {0}-{1}s 下载完成，获取到 {2} 条弹幕。", timeBegin, timeEnd, result.Data.Count);
                                        danmuList.AddRange(result.Data);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "下载乐视弹幕分段时发生错误. vid={0}, time={1}-{2}", vid, timeBegin, timeEnd);
                }

                timeBegin = timeEnd;
                // Rate limit between segment requests
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }

            return danmuList;
        }

        private string StripJsonpCallback(string body)
        {
            var match = _jsonpRegex.Match(body);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            // If no JSONP wrapper, maybe it's plain JSON
            if (body.TrimStart().StartsWith("{") || body.TrimStart().StartsWith("["))
            {
                return body;
            }
            return null;
        }

        protected new async Task LimitRequestFrequently()
        {
            await _leshiApiRateLimiter.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastLeshiApiRequestTime;
                if (timeSinceLastRequest < _leshiApiMinInterval)
                {
                    await Task.Delay(_leshiApiMinInterval - timeSinceLastRequest).ConfigureAwait(false);
                }
                _lastLeshiApiRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _leshiApiRateLimiter.Release();
            }
        }
    }
}
