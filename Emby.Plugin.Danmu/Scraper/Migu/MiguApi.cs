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
using Emby.Plugin.Danmu.Scrapers.Migu.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scrapers.Migu
{
    public class MiguApi : AbstractApi
    {
        private static readonly SemaphoreSlim _miguApiRateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastMiguApiRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _miguApiMinInterval = TimeSpan.FromMilliseconds(500);

        private readonly JsonSerializerOptions _jsonOptions;

        public MiguApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("MiguApi"), httpClient)
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
        }

        protected override Dictionary<string, string> GetDefaultHeaders()
        {
            return new Dictionary<string, string>
            {
                { "Origin", "https://www.miguvideo.com" },
                { "Referer", "https://www.miguvideo.com/" },
            };
        }

        public async Task<List<MiguSearchItem>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<MiguSearchItem>();
            }

            var cacheKey = $"migu_search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<MiguSearchItem>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            // Migu search API - POST with JSON body
            var url = "https://jadeite.migu.cn/search/v3/open-search";
            var searchPayload = new
            {
                keyword = keyword,
                pageNo = 1,
                pageSize = 20,
                sourceFrom = "pcWeb",
                type = "all"
            };

            var postBody = JsonSerializer.Serialize(searchPayload, _jsonOptions);
            var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            options.RequestContentType = "application/json";
            options.RequestContent = postBody.AsMemory();
            options.RequestHeaders["appId"] = "miguvideo";
            options.RequestHeaders["terminalId"] = "www";

            var result = new List<MiguSearchItem>();
            try
            {
                using (var response = await httpClient.Post(options).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var searchResult = JsonSerializer.Deserialize<MiguSearchResult>(json, _jsonOptions);
                            if (searchResult?.Body?.ContList != null)
                            {
                                foreach (var item in searchResult.Body.ContList)
                                {
                                    if (!string.IsNullOrEmpty(item.ContId))
                                    {
                                        result.Add(item);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "咪咕搜索失败. keyword: {0}", keyword);
            }

            _memoryCache.Set(cacheKey, result, expiredOption);
            return result;
        }

        public async Task<MiguContentBody> GetContentAsync(string contId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(contId))
            {
                return null;
            }

            var cacheKey = $"migu_content_{contId}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<MiguContentBody>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently().ConfigureAwait(false);

            // Content detail API
            var url = $"https://v3-sc.miguvideo.com/program/v4/cont/content-info/{contId}/1";
            var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            options.RequestHeaders["appId"] = "miguvideo";
            options.RequestHeaders["terminalId"] = "www";

            MiguContentBody content = null;
            try
            {
                using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var contentResult = JsonSerializer.Deserialize<MiguContentResult>(json, _jsonOptions);
                            content = contentResult?.Body;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取咪咕视频内容详情失败. contId: {0}", contId);
            }

            if (content != null)
            {
                _memoryCache.Set(cacheKey, content, expiredOption);
            }
            return content;
        }

        public async Task<List<MiguDanmuItem>> GetDanmuContentAsync(string epsId, int durationSeconds, CancellationToken cancellationToken)
        {
            var danmuList = new List<MiguDanmuItem>();
            if (string.IsNullOrEmpty(epsId))
            {
                return danmuList;
            }

            // Migu danmu uses 30-second segments
            var segmentSize = 30;
            var start = 0;
            var maxDuration = durationSeconds > 0 ? durationSeconds : 7200; // default 2h
            var itemId = 0; // page index for barrage within segment

            while (start < maxDuration)
            {
                var end = start + segmentSize;
                try
                {
                    _logger.Info("正在下载咪咕弹幕分段: {0}-{1}s (epsId={2})", start, end, epsId);
                    var url = $"https://webapi.miguvideo.com/gateway/live_barrage/videox/barrage/v2/list/{epsId}/{itemId}/{start}/{end}/020";
                    var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
                    options.RequestHeaders["appId"] = "miguvideo";
                    options.RequestHeaders["terminalId"] = "www";

                    using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                    {
                        using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                        {
                            var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(json))
                            {
                                var result = JsonSerializer.Deserialize<MiguDanmuResult>(json, _jsonOptions);
                                if (result?.Data?.List != null)
                                {
                                    _logger.Info("咪咕弹幕分段 {0}-{1}s 下载完成，获取到 {2} 条弹幕。", start, end, result.Data.List.Count);
                                    danmuList.AddRange(result.Data.List);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "下载咪咕弹幕分段时发生错误. epsId={0}, time={1}-{2}", epsId, start, end);
                }

                start = end;
                // Rate limit between segment requests
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }

            return danmuList;
        }

        protected new async Task LimitRequestFrequently()
        {
            await _miguApiRateLimiter.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastMiguApiRequestTime;
                if (timeSinceLastRequest < _miguApiMinInterval)
                {
                    await Task.Delay(_miguApiMinInterval - timeSinceLastRequest).ConfigureAwait(false);
                }
                _lastMiguApiRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _miguApiRateLimiter.Release();
            }
        }
    }
}
