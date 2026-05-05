using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugin.Danmu.Scraper.Renren.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Renren
{
    public class RenrenApi : AbstractApi
    {
        private const string SECRET_KEY = "cf65GPholnICgyw1xbrpA79XVkizOdMq";
        private const string RENREN_USER_AGENT = "okhttp/3.12.13";
        private const string TV_SEARCH_URL = "https://api.gorafie.com/qwtv/search";
        private const string TV_DETAIL_URL = "https://api.gorafie.com/qwtv/drama/details";
        private const string TV_DANMU_URL = "https://static-dm.qwdjapp.com/v1/produce/danmu/EPISODE/";
        private const string WEB_SEARCH_URL = "https://api.rrmj.plus/m-station/search/drama";
        private const string WEB_DANMU_URL = "https://static-dm.rrmj.plus/v1/produce/danmu/EPISODE/";
        private const string CLIENT_VERSION = "4.9.1";

        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly string _deviceId;

        public RenrenApi(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.GetLogger("RenrenApi"), httpClient)
        {
            _logger = logManager.GetLogger("RenrenApi");
            _jsonSerializer = jsonSerializer;
            _deviceId = Guid.NewGuid().ToString("N").Substring(0, 16);
        }

        /// <summary>
        /// 生成HMAC-SHA256签名
        /// </summary>
        private string GenerateSign(string timestamp)
        {
            var message = $"{timestamp}{_deviceId}";
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SECRET_KEY)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 获取带签名的请求头
        /// </summary>
        private Dictionary<string, string> GetSignedHeaders()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var sign = GenerateSign(timestamp);

            return new Dictionary<string, string>
            {
                { "clientVersion", CLIENT_VERSION },
                { "deviceid", _deviceId },
                { "token", "" },
                { "aliid", "" },
                { "sign", sign },
                { "pkt", "rrmj" },
                { "timestamp", timestamp },
            };
        }

        /// <summary>
        /// TV端搜索（主要方式）
        /// </summary>
        public async Task<List<RenrenTvSearchItem>> SearchTvAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<RenrenTvSearchItem>();
            }

            var cacheKey = $"renren_search_tv_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<RenrenTvSearchItem>>(cacheKey, out var cachedResult))
            {
                return cachedResult;
            }

            try
            {
                var encodedKeyword = HttpUtility.UrlEncode(keyword);
                var url = $"{TV_SEARCH_URL}?searchWord={encodedKeyword}&num=30";

                var httpRequestOptions = new HttpRequestOptions
                {
                    Url = url,
                    UserAgent = RENREN_USER_AGENT,
                    TimeoutMs = 30000,
                    AcceptHeader = "application/json",
                    CancellationToken = cancellationToken,
                };

                var signedHeaders = GetSignedHeaders();
                foreach (var kvp in signedHeaders)
                {
                    httpRequestOptions.RequestHeaders[kvp.Key] = kvp.Value;
                }

                var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Info("[Renren] TV搜索请求失败, status={0}", response.StatusCode);
                    return new List<RenrenTvSearchItem>();
                }

                var result = _jsonSerializer.DeserializeFromStream<RenrenTvSearchResult>(response.Content);
                if (result?.Data?.Items != null && result.Data.Items.Count > 0)
                {
                    _memoryCache.Set(cacheKey, result.Data.Items, expiredOption);
                    return result.Data.Items;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[Renren] TV搜索异常: keyword={0}", ex, keyword);
            }

            // Fallback to web search
            try
            {
                var webItems = await SearchWebAsync(keyword, cancellationToken).ConfigureAwait(false);
                if (webItems.Count > 0)
                {
                    // Convert web results to TV format
                    var tvItems = new List<RenrenTvSearchItem>();
                    foreach (var item in webItems)
                    {
                        tvItems.Add(new RenrenTvSearchItem
                        {
                            Id = item.Id,
                            SeriesId = item.SeriesId,
                            Title = item.Title ?? item.Name,
                            Category = item.Category,
                            Year = item.Year,
                            EpisodeNum = item.EpisodeCount,
                        });
                    }
                    _memoryCache.Set(cacheKey, tvItems, expiredOption);
                    return tvItems;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[Renren] Web搜索回退异常: keyword={0}", ex, keyword);
            }

            _memoryCache.Set(cacheKey, new List<RenrenTvSearchItem>(), expiredOption);
            return new List<RenrenTvSearchItem>();
        }

        /// <summary>
        /// Web端搜索（回退方式）
        /// </summary>
        private async Task<List<RenrenSearchItem>> SearchWebAsync(string keyword, CancellationToken cancellationToken)
        {
            var url = WEB_SEARCH_URL;
            var postData = _jsonSerializer.SerializeToString(new { keyword = keyword });

            var httpRequestOptions = new HttpRequestOptions
            {
                Url = url,
                UserAgent = RENREN_USER_AGENT,
                TimeoutMs = 30000,
                AcceptHeader = "application/json",
                RequestContentType = "application/json",
                RequestContent = postData.AsMemory(),
                CancellationToken = cancellationToken,
            };

            var signedHeaders = GetSignedHeaders();
            foreach (var kvp in signedHeaders)
            {
                httpRequestOptions.RequestHeaders[kvp.Key] = kvp.Value;
            }

            var response = await httpClient.Post(httpRequestOptions).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new List<RenrenSearchItem>();
            }

            var result = _jsonSerializer.DeserializeFromStream<RenrenSearchResult>(response.Content);
            return result?.Data?.Items ?? new List<RenrenSearchItem>();
        }

        /// <summary>
        /// 获取剧集详情
        /// </summary>
        public async Task<RenrenDetailData> GetDetailAsync(string seriesId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(seriesId))
            {
                return null;
            }

            var cacheKey = $"renren_detail_{seriesId}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<RenrenDetailData>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var url = $"{TV_DETAIL_URL}?seriesId={seriesId}";
                var httpRequestOptions = new HttpRequestOptions
                {
                    Url = url,
                    UserAgent = RENREN_USER_AGENT,
                    TimeoutMs = 30000,
                    AcceptHeader = "application/json",
                    CancellationToken = cancellationToken,
                };

                var signedHeaders = GetSignedHeaders();
                foreach (var kvp in signedHeaders)
                {
                    httpRequestOptions.RequestHeaders[kvp.Key] = kvp.Value;
                }

                var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Info("[Renren] 获取详情失败, seriesId={0}, status={1}", seriesId, response.StatusCode);
                    return null;
                }

                var result = _jsonSerializer.DeserializeFromStream<RenrenDetailResult>(response.Content);
                if (result?.Data != null)
                {
                    _memoryCache.Set(cacheKey, result.Data, expiredOption);
                    return result.Data;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[Renren] 获取详情异常: seriesId={0}", ex, seriesId);
            }

            _memoryCache.Set<RenrenDetailData>(cacheKey, null, expiredOption);
            return null;
        }

        /// <summary>
        /// 获取弹幕数据
        /// </summary>
        public async Task<List<RenrenDanmuItem>> GetDanmuAsync(string episodeId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(episodeId))
            {
                return new List<RenrenDanmuItem>();
            }

            // Try TV danmu first, then fallback to web danmu
            var danmuUrls = new[]
            {
                $"{TV_DANMU_URL}{episodeId}",
                $"{WEB_DANMU_URL}{episodeId}",
            };

            foreach (var url in danmuUrls)
            {
                try
                {
                    var httpRequestOptions = new HttpRequestOptions
                    {
                        Url = url,
                        UserAgent = RENREN_USER_AGENT,
                        TimeoutMs = 30000,
                        AcceptHeader = "application/json",
                        CancellationToken = cancellationToken,
                    };

                    var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        continue;
                    }

                    var result = _jsonSerializer.DeserializeFromStream<RenrenDanmuResult>(response.Content);
                    if (result?.Data?.Danmaku != null && result.Data.Danmaku.Count > 0)
                    {
                        return result.Data.Danmaku;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Info("[Renren] 获取弹幕失败, url={0}, error={1}", url, ex.Message);
                }
            }

            return new List<RenrenDanmuItem>();
        }
    }
}
