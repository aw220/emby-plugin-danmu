using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Bahamut.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Bahamut
{
    public class BahamutApi : AbstractApi
    {
        private const string BAHAMUT_USER_AGENT =
            "Anime/2.29.2 (7N5749MM3F.tw.com.gamer.anime; build:972; iOS 26.0.0) Alamofire/5.6.4";

        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        public BahamutApi(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.GetLogger("BahamutApi"), httpClient)
        {
            _logger = logManager.GetLogger("BahamutApi");
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// 搜索巴哈姆特动画
        /// </summary>
        public async Task<List<BahamutAnime>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<BahamutAnime>();
            }

            var cacheKey = $"bahamut_search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<BahamutAnime>>(cacheKey, out var cachedResult))
            {
                return cachedResult;
            }

            keyword = HttpUtility.UrlEncode(keyword);
            var url = $"https://api.gamer.com.tw/mobile_app/anime/v1/search.php?kw={keyword}";
            var httpRequestOptions = new HttpRequestOptions
            {
                Url = url,
                UserAgent = BAHAMUT_USER_AGENT,
                TimeoutMs = 30000,
                AcceptHeader = "application/json",
                CancellationToken = cancellationToken,
            };

            try
            {
                var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Info("[Bahamut] 搜索请求失败, status={0}", response.StatusCode);
                    return new List<BahamutAnime>();
                }

                var result = _jsonSerializer.DeserializeFromStream<BahamutSearchResult>(response.Content);
                if (result?.Data?.Anime != null && result.Data.Anime.Count > 0)
                {
                    _memoryCache.Set(cacheKey, result.Data.Anime, expiredOption);
                    return result.Data.Anime;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[Bahamut] 搜索异常: keyword={0}", ex, keyword);
            }

            _memoryCache.Set(cacheKey, new List<BahamutAnime>(), expiredOption);
            return new List<BahamutAnime>();
        }

        /// <summary>
        /// 获取视频详情（含剧集列表）
        /// </summary>
        public async Task<BahamutVideoData> GetVideoAsync(long videoSn, CancellationToken cancellationToken)
        {
            if (videoSn <= 0)
            {
                return null;
            }

            var cacheKey = $"bahamut_video_{videoSn}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<BahamutVideoData>(cacheKey, out var cached))
            {
                return cached;
            }

            var url = $"https://api.gamer.com.tw/anime/v1/video.php?videoSn={videoSn}";
            var httpRequestOptions = new HttpRequestOptions
            {
                Url = url,
                UserAgent = BAHAMUT_USER_AGENT,
                TimeoutMs = 30000,
                AcceptHeader = "application/json",
                CancellationToken = cancellationToken,
            };

            try
            {
                var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Info("[Bahamut] 获取视频信息失败, videoSn={0}, status={1}", videoSn, response.StatusCode);
                    return null;
                }

                var result = _jsonSerializer.DeserializeFromStream<BahamutVideoResult>(response.Content);
                if (result?.Data != null)
                {
                    _memoryCache.Set(cacheKey, result.Data, expiredOption);
                    return result.Data;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[Bahamut] 获取视频信息异常: videoSn={0}", ex, videoSn);
            }

            _memoryCache.Set<BahamutVideoData>(cacheKey, null, expiredOption);
            return null;
        }

        /// <summary>
        /// 获取弹幕
        /// </summary>
        public async Task<List<BahamutDanmu>> GetDanmuAsync(long videoSn, CancellationToken cancellationToken)
        {
            if (videoSn <= 0)
            {
                return new List<BahamutDanmu>();
            }

            var url = $"https://api.gamer.com.tw/anime/v1/danmu.php?geo=TW%2CHK&videoSn={videoSn}";
            var httpRequestOptions = new HttpRequestOptions
            {
                Url = url,
                UserAgent = BAHAMUT_USER_AGENT,
                TimeoutMs = 30000,
                AcceptHeader = "application/json",
                CancellationToken = cancellationToken,
            };

            try
            {
                var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Info("[Bahamut] 获取弹幕失败, videoSn={0}, status={1}", videoSn, response.StatusCode);
                    return new List<BahamutDanmu>();
                }

                var result = _jsonSerializer.DeserializeFromStream<BahamutDanmuResult>(response.Content);
                if (result?.Data?.Danmu != null)
                {
                    return result.Data.Danmu;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[Bahamut] 获取弹幕异常: videoSn={0}", ex, videoSn);
            }

            return new List<BahamutDanmu>();
        }

        /// <summary>
        /// 从剧集数据中提取第一组剧集列表
        /// </summary>
        public List<BahamutEpisode> ExtractEpisodes(BahamutVideoData videoData)
        {
            if (videoData?.Anime?.Episodes == null || videoData.Anime.Episodes.Count == 0)
            {
                return new List<BahamutEpisode>();
            }

            // 优先使用 key "0"，否则取第一个可用的
            if (videoData.Anime.Episodes.ContainsKey("0"))
            {
                return videoData.Anime.Episodes["0"];
            }

            return videoData.Anime.Episodes.Values.FirstOrDefault() ?? new List<BahamutEpisode>();
        }
    }
}
