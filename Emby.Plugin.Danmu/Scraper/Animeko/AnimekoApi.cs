using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Animeko.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Animeko
{
    public class AnimekoApi : AbstractApi
    {
        private static readonly object _lock = new object();
        private DateTime lastRequestTime = DateTime.Now.AddDays(-1);
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        private const string BANGUMI_API_BASE = "https://api.bgm.tv/v0";
        private const string DANMAKU_GLOBAL_BASE = "https://danmaku-global.myani.org/v1";
        private const string DANMAKU_CN_BASE = "https://danmaku-cn.myani.org/v1";
        private const string BANGUMI_USER_AGENT = "huangxd-/danmu_api/1.0(https://github.com/huangxd-/danmu_api)";

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimekoApi"/> class.
        /// </summary>
        public AnimekoApi(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.GetLogger("AnimekoApi"), httpClient)
        {
            _logger = logManager.GetLogger("AnimekoApi");
            _jsonSerializer = jsonSerializer;
        }

        public async Task<List<BangumiSubject>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<BangumiSubject>();
            }

            var cacheKey = $"animeko_search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<BangumiSubject>>(cacheKey, out var searchResult))
            {
                return searchResult;
            }

            LimitRequestFrequently();

            var url = $"{BANGUMI_API_BASE}/search/subjects";
            var postBody = new BangumiSearchRequest
            {
                Keyword = keyword,
                Filter = new BangumiSearchFilter { Type = new List<int> { 2 } }
            };

            var httpRequestOptions = new HttpRequestOptions
            {
                Url = url,
                UserAgent = BANGUMI_USER_AGENT,
                TimeoutMs = 30000,
                AcceptHeader = "application/json",
                RequestContentType = "application/json",
            };

            try
            {
                var result = await httpClient.GetSelfResultAsyncWithError<BangumiSearchResult>(
                    httpRequestOptions, null, "POST", postBody).ConfigureAwait(false);

                if (result?.Data != null)
                {
                    _memoryCache.Set(cacheKey, result.Data, expiredOption);
                    return result.Data;
                }
            }
            catch (Exception ex)
            {
                _logger.Info("Animeko search error: {0}", ex.Message);
            }

            _memoryCache.Set(cacheKey, new List<BangumiSubject>(), expiredOption);
            return new List<BangumiSubject>();
        }

        public async Task<List<BangumiEpisode>> GetEpisodesAsync(long subjectId, CancellationToken cancellationToken)
        {
            if (subjectId <= 0)
            {
                return new List<BangumiEpisode>();
            }

            var cacheKey = $"animeko_episodes_{subjectId}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<List<BangumiEpisode>>(cacheKey, out var cachedEpisodes))
            {
                return cachedEpisodes;
            }

            var allEpisodes = new List<BangumiEpisode>();
            int offset = 0;
            const int limit = 200;

            while (true)
            {
                var url = $"{BANGUMI_API_BASE}/episodes?subject_id={subjectId}&limit={limit}&offset={offset}";
                var httpRequestOptions = new HttpRequestOptions
                {
                    Url = url,
                    UserAgent = BANGUMI_USER_AGENT,
                    TimeoutMs = 30000,
                    AcceptHeader = "application/json",
                };

                try
                {
                    var result = await httpClient.GetSelfResultAsync<BangumiEpisodeResult>(httpRequestOptions)
                        .ConfigureAwait(false);

                    if (result?.Data == null || result.Data.Count == 0)
                    {
                        break;
                    }

                    allEpisodes.AddRange(result.Data);

                    if (allEpisodes.Count >= result.Total)
                    {
                        break;
                    }

                    offset += limit;
                }
                catch (Exception ex)
                {
                    _logger.Info("Animeko get episodes error: {0}", ex.Message);
                    break;
                }
            }

            // Filter: type === 0 for main episodes only
            var mainEpisodes = allEpisodes.Where(e => e.Type == 0).ToList();
            _memoryCache.Set(cacheKey, mainEpisodes, expiredOption);
            return mainEpisodes;
        }

        public async Task<AnimekoDanmakuResult> GetDanmakuAsync(long episodeId, CancellationToken cancellationToken)
        {
            if (episodeId <= 0)
            {
                return null;
            }

            // Try global server first, then CN fallback
            var urls = new[]
            {
                $"{DANMAKU_GLOBAL_BASE}/danmaku/{episodeId}",
                $"{DANMAKU_CN_BASE}/danmaku/{episodeId}"
            };

            foreach (var url in urls)
            {
                try
                {
                    var httpRequestOptions = new HttpRequestOptions
                    {
                        Url = url,
                        UserAgent = HTTP_USER_AGENT,
                        TimeoutMs = 30000,
                        AcceptHeader = "application/json",
                    };

                    var result = await httpClient.GetSelfResultAsync<AnimekoDanmakuResult>(httpRequestOptions)
                        .ConfigureAwait(false);

                    if (result?.DanmakuList != null && result.DanmakuList.Count > 0)
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Info("Animeko get danmaku from {0} error: {1}", url, ex.Message);
                }
            }

            return null;
        }

        protected new void LimitRequestFrequently(double intervalMilliseconds = 1000)
        {
            var diff = 0;
            lock (_lock)
            {
                var ts = DateTime.Now - lastRequestTime;
                diff = (int)(intervalMilliseconds - ts.TotalMilliseconds);
                lastRequestTime = DateTime.Now;
            }

            if (diff > 0)
            {
                _logger.Debug("请求太频繁，等待{0}毫秒后继续执行...", diff);
                Thread.Sleep(diff);
            }
        }
    }
}
