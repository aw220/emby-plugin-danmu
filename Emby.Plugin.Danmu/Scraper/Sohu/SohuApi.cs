using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scrapers.Sohu.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scrapers.Sohu
{
    public class SohuApi : AbstractApi
    {
        private static readonly SemaphoreSlim _sohuApiRateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastSohuApiRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _sohuApiMinInterval = TimeSpan.FromMilliseconds(500);

        private const string SOHU_API_KEY = "f351515304020cad28c92f70f002261c";

        public SohuApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("SohuApi"), httpClient)
        {
        }

        public async Task<List<SohuSearchDoc>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<SohuSearchDoc>();
            }

            var cacheKey = $"sohu_search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<SohuSearchDoc>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            var encodedKeyword = HttpUtility.UrlEncode(keyword);
            var url = $"https://m.so.tv.sohu.com/search/pc/keyword";
            var postData = $"key={encodedKeyword}&type=1&num=20&start=0";

            var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            options.RequestContentType = "application/x-www-form-urlencoded";
            options.RequestContent = postData.AsMemory();
            options.RequestHeaders["Referer"] = "https://tv.sohu.com/";

            var result = new List<SohuSearchDoc>();
            try
            {
                using (var response = await httpClient.Post(options).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var searchResult = JsonSerializer.Deserialize<SohuSearchResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (searchResult?.Response?.Docs != null)
                            {
                                result.AddRange(searchResult.Response.Docs);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜狐搜索失败. keyword: {0}", keyword);
            }

            _memoryCache.Set(cacheKey, result, expiredOption);
            return result;
        }

        public async Task<SohuPlaylistData> GetPlaylistAsync(string playlistId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(playlistId))
            {
                return null;
            }

            var cacheKey = $"sohu_playlist_{playlistId}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<SohuPlaylistData>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            var url = $"https://pl.hd.sohu.com/videolist?playlistid={playlistId}&api_key={SOHU_API_KEY}";
            var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            options.RequestHeaders["Referer"] = "https://tv.sohu.com/";

            SohuPlaylistData data = null;
            try
            {
                using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var result = JsonSerializer.Deserialize<SohuPlaylistResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (result?.Status == 200 && result.Data != null)
                            {
                                data = result.Data;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取搜狐播放列表失败. playlistId: {0}", playlistId);
            }

            if (data != null)
            {
                _memoryCache.Set(cacheKey, data, expiredOption);
            }
            return data;
        }

        public async Task<List<SohuDanmuComment>> GetDanmuContentAsync(string vid, string aid, int durationSeconds, CancellationToken cancellationToken)
        {
            var danmuList = new List<SohuDanmuComment>();
            if (string.IsNullOrEmpty(vid) || string.IsNullOrEmpty(aid))
            {
                return danmuList;
            }

            // Sohu danmu is fetched in 300-second segments
            var segmentSize = 300;
            var timeBegin = 0;
            var maxDuration = durationSeconds > 0 ? durationSeconds : 7200; // default 2h if unknown

            while (timeBegin < maxDuration)
            {
                var timeEnd = timeBegin + segmentSize;
                try
                {
                    _logger.Info("正在下载搜狐弹幕分段: {0}-{1}s (vid={2})", timeBegin, timeEnd, vid);
                    var url = $"https://api.danmu.tv.sohu.com/dmh5/dmListAll?act=dmlist_v2&vid={vid}&aid={aid}&pct=2&time_begin={timeBegin}&time_end={timeEnd}&dct=1&request_from=h5_js";
                    var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
                    options.RequestHeaders["Referer"] = "https://tv.sohu.com/";

                    using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                    {
                        using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                        {
                            var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(json))
                            {
                                var result = JsonSerializer.Deserialize<SohuDanmuResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (result?.Comments != null)
                                {
                                    _logger.Info("搜狐弹幕分段 {0}-{1}s 下载完成，获取到 {2} 条弹幕。", timeBegin, timeEnd, result.Comments.Count);
                                    danmuList.AddRange(result.Comments);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "下载搜狐弹幕分段时发生错误. vid={0}, time={1}-{2}", vid, timeBegin, timeEnd);
                }

                timeBegin = timeEnd;
                // Rate limit between segment requests
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }

            return danmuList;
        }

        protected new async Task LimitRequestFrequently()
        {
            await _sohuApiRateLimiter.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastSohuApiRequestTime;
                if (timeSinceLastRequest < _sohuApiMinInterval)
                {
                    await Task.Delay(_sohuApiMinInterval - timeSinceLastRequest).ConfigureAwait(false);
                }
                _lastSohuApiRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _sohuApiRateLimiter.Release();
            }
        }
    }
}
