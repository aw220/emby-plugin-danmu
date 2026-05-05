using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Maiduidui.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Maiduidui
{
    public class MaiduiduiApi : AbstractApi
    {
        private const string PRIVATE_KEY = "e1be6b4cf4021b3d181170d1879a530a9e4130b69032144d5568abfd6cd6c1c2";
        private const string DEVICE_NUM = "853BDD7A1DC011F1C341455071C03AEB";
        private const string MDD_USER_AGENT = "Mdd/5.8.00 (Android+32+)";
        private const string MDD_VERSION = "5.8.00";
        private const string MDD_OS = "android";
        private const string BASE_URL = "https://mob.mddcloud.com.cn";

        private static readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(500);

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public MaiduiduiApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("MaiduiduiApi"), httpClient)
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MDD_USER_AGENT);
            _httpClient.DefaultRequestHeaders.Add("version", MDD_VERSION);
            _httpClient.DefaultRequestHeaders.Add("Referer", "mdd");
        }

        /// <summary>
        /// Generate MD5 signature for Maiduidui API requests.
        /// Signature = MD5(os + version + action + time + appToken + privateKey + data)
        /// </summary>
        private string GenerateSignature(string action, long time, string data, string appToken = "")
        {
            var raw = $"{MDD_OS}{MDD_VERSION}{action}{time}{appToken}{PRIVATE_KEY}{data}";
            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Build the POST form content with signature for Maiduidui API.
        /// </summary>
        private FormUrlEncodedContent BuildSignedContent(string action, string data)
        {
            var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sign = GenerateSignature(action, time, data);

            var parameters = new Dictionary<string, string>
            {
                { "os", MDD_OS },
                { "version", MDD_VERSION },
                { "action", action },
                { "time", time.ToString() },
                { "appToken", "" },
                { "sign", sign },
                { "deviceNum", DEVICE_NUM },
                { "data", data }
            };

            return new FormUrlEncodedContent(parameters);
        }

        public async Task<List<MddSearchItem>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<MddSearchItem>();
            }

            var cacheKey = $"mdd_search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<MddSearchItem>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await LimitRequestFrequently().ConfigureAwait(false);

            var action = "getAllSearchResult4820";
            var dataObj = new { keyword = keyword, pageNum = 1, pageSize = 20 };
            var data = JsonSerializer.Serialize(dataObj);
            var content = BuildSignedContent(action, data);

            var url = $"{BASE_URL}/searchApi/search/getAllSearchResult4820.action";

            try
            {
                var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<MddSearchResult>(responseStr, _jsonOptions);

                var items = new List<MddSearchItem>();
                if (result != null && result.Code == 0 && result.Data?.VideoList?.Results != null)
                {
                    // Filter by relevant types: 剧集, 电影, 综艺
                    items = result.Data.VideoList.Results
                        .Where(x => x.VodType == "剧集" || x.VodType == "电影" || x.VodType == "综艺")
                        .ToList();
                }

                _memoryCache.Set(cacheKey, items, expiredOption);
                return items;
            }
            catch (Exception ex)
            {
                _logger.Error("埋堆堆搜索失败: keyword={0}, error={1}", keyword, ex.Message);
                _memoryCache.Set(cacheKey, new List<MddSearchItem>(), expiredOption);
                return new List<MddSearchItem>();
            }
        }

        public async Task<List<MddEpisode>> GetEpisodesAsync(long vodId, CancellationToken cancellationToken)
        {
            if (vodId <= 0)
            {
                return new List<MddEpisode>();
            }

            var cacheKey = $"mdd_episodes_{vodId}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<List<MddEpisode>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await LimitRequestFrequently().ConfigureAwait(false);

            var action = "listVodSactions";
            var dataObj = new { vodId = vodId };
            var data = JsonSerializer.Serialize(dataObj);
            var content = BuildSignedContent(action, data);

            var url = $"{BASE_URL}/api/vod/listVodSactions.action";

            try
            {
                var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<MddEpisodeResult>(responseStr, _jsonOptions);

                var episodes = new List<MddEpisode>();
                if (result != null && result.Code == 0 && result.Data != null)
                {
                    foreach (var group in result.Data)
                    {
                        if (group.VodSactionItems != null)
                        {
                            episodes.AddRange(group.VodSactionItems);
                        }
                    }
                }

                // Sort by episode number
                episodes = episodes.OrderBy(x => x.ItemNum).ToList();

                _memoryCache.Set(cacheKey, episodes, expiredOption);
                return episodes;
            }
            catch (Exception ex)
            {
                _logger.Error("埋堆堆获取剧集列表失败: vodId={0}, error={1}", vodId, ex.Message);
                _memoryCache.Set(cacheKey, new List<MddEpisode>(), expiredOption);
                return new List<MddEpisode>();
            }
        }

        /// <summary>
        /// Get danmu for a specific episode.
        /// Maiduidui uses 60-second segments, so we need to request multiple segments.
        /// </summary>
        public async Task<List<MddDanmu>> GetDanmuAsync(long itemId, int durationSeconds, CancellationToken cancellationToken)
        {
            if (itemId <= 0)
            {
                return new List<MddDanmu>();
            }

            var allDanmu = new List<MddDanmu>();
            var totalSegments = (durationSeconds > 0) ? (int)Math.Ceiling(durationSeconds / 60.0) : 60;

            for (int segment = 0; segment < totalSegments; segment++)
            {
                try
                {
                    var action = "vodBarrage396";
                    var dataObj = new { itemId = itemId, page = segment + 1 };
                    var data = JsonSerializer.Serialize(dataObj);
                    var content = BuildSignedContent(action, data);

                    var url = $"{BASE_URL}/api/barrage/vodBarrage396.action";

                    var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var result = JsonSerializer.Deserialize<MddDanmuResult>(responseStr, _jsonOptions);

                    if (result != null && result.Code == 0 && result.Data?.Barrages != null)
                    {
                        if (result.Data.Barrages.Count == 0)
                        {
                            // No more danmu, stop fetching
                            break;
                        }

                        // Adjust time offset: each segment is 60 seconds, timeOffset is relative to segment start
                        foreach (var danmu in result.Data.Barrages)
                        {
                            danmu.TimeOffset = danmu.TimeOffset + (segment * 60.0);
                        }

                        allDanmu.AddRange(result.Data.Barrages);
                    }
                    else
                    {
                        break;
                    }

                    // Rate limit between segment requests
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error("埋堆堆获取弹幕分段失败: itemId={0}, segment={1}, error={2}", itemId, segment, ex.Message);
                    break;
                }
            }

            return allDanmu;
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
