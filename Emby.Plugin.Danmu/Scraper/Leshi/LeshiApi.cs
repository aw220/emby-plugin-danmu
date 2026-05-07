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
            @"<a[^>]*href=""(?:(?:https?:)?//www\.le\.com)?/ptv/vplay/(\d+)\.html""[^>]*>(.*?)</a>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _episodeHrefRegex = new Regex(
            @"<a[^>]*href=""(?:(?:https?:)?//www\.le\.com)?/ptv/vplay/(\d+)\.html""[^>]*",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

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
            @"<a[^>]*href=""(?:(?:https?:)?//www\.le\.com)?/ptv/vplay/(\d+)\.html""[^>]*?(?:title=""([^""]*?)"")?[^>]*>(.*?)</a>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _episodeCardRegex = new Regex(
            @"<dl[^>]*class=""[^""]*\bdl_temp\b[^""]*""[^>]*>[\s\S]*?</dl>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _titleTextRegex = new Regex(
            @"<dt[^>]*class=""d_tit""[^>]*>[\s\S]*?<a[^>]*>(.*?)</a>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _titleAttributeRegex = new Regex(
            @"<a[^>]*title=""([^""]+)""[^>]*href=""(?:(?:https?:)?//www\.le\.com)?/ptv/vplay/\d+\.html""",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _episodeDescriptionRegex = new Regex(
            @"<dd[^>]*class=""d_cnt""[^>]*>(.*?)</dd>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _episodeScopeRegex = new Regex(
            @"<div class=""show_cnt (?:twxj|sjxj)-[^""]*""[\s\S]*?</div>\s*</div>\s*</div>|<div class=""show_play first_videolist[\s\S]*?</div>\s*</div>\s*</div>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _bootstrapCurrentVideoRegex = new Regex(
            @"video\s*:\s*\{[^}]*\bvid\s*:\s*(?:\""|')?(\d+)(?:\""|')?[^}]*\}",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex _htmlTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);

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

            var result = new List<LeshiEpisode>();
            foreach (var url in BuildEpisodePageUrls(aid, contentType))
            {
                try
                {
                    var options = GetDefaultHttpRequestOptions(url, null, cancellationToken);
                    options.RequestContentType = null;
                    options.AcceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                    options.RequestHeaders["Referer"] = "https://www.le.com/";

                    using (var response = await httpClient.GetResponse(options).ConfigureAwait(false))
                    {
                        using (var reader = new StreamReader(response.Content, Encoding.UTF8))
                        {
                            var html = await reader.ReadToEndAsync().ConfigureAwait(false);
                            if (string.IsNullOrEmpty(html))
                            {
                                continue;
                            }

                            result = ParseEpisodes(html);
                            if (result.Count > 0)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取乐视剧集列表失败. aid: {0}, url: {1}", aid, url);
                }
            }

            if (result.Count > 0)
            {
                _memoryCache.Set(cacheKey, result, expiredOption);
            }
            return result;
        }

        internal static List<LeshiEpisode> ParseEpisodes(string html)
        {
            var episodes = new List<LeshiEpisode>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return episodes;
            }

            var scopes = ExtractEpisodeScopes(html);
            foreach (var scope in scopes)
            {
                AppendEpisodesFromCards(scope, episodes);
                AppendEpisodesFromAnchors(scope, episodes);
            }

            // show_cnt/first_videolist 可能只截到局部容器，再对整页做一次去重解析，
            // 兼容 tv/{aid}.html 电视剧详情页的分集块。
            AppendEpisodesFromCards(html, episodes);
            AppendEpisodesFromAnchors(html, episodes);

            if (episodes.Count == 0)
            {
                AppendRawVideoLinks(html, episodes);
            }

            if (episodes.Count == 0)
            {
                AppendCurrentVideoFromBootstrap(html, episodes);
            }

            return episodes;
        }

        private static IEnumerable<string> BuildEpisodePageUrls(string aid, string contentType)
        {
            var pathTypes = new List<string>();
            void AddPathType(string pathType)
            {
                if (!string.IsNullOrWhiteSpace(pathType) && !pathTypes.Contains(pathType, StringComparer.OrdinalIgnoreCase))
                {
                    pathTypes.Add(pathType);
                }
            }

            switch (contentType?.ToLowerInvariant())
            {
                case "movie":
                    AddPathType("movie");
                    break;
                case "cartoon":
                case "comic":
                    AddPathType("comic");
                    break;
                case "playlet":
                    AddPathType("playlet");
                    break;
                default:
                    AddPathType("tv");
                    break;
            }

            AddPathType("tv");
            AddPathType("comic");
            AddPathType("playlet");
            AddPathType("movie");

            foreach (var pathType in pathTypes)
            {
                yield return $"https://www.le.com/{pathType}/{aid}.html";
            }
        }

        private static List<string> ExtractEpisodeScopes(string html)
        {
            var scopes = _episodeScopeRegex
                .Matches(html)
                .Cast<Match>()
                .Where(match => match.Success && !string.IsNullOrWhiteSpace(match.Value))
                .Select(match => match.Value)
                .ToList();

            if (scopes.Count == 0)
            {
                scopes.Add(html);
            }

            return scopes;
        }

        private static void AppendEpisodesFromCards(string html, ICollection<LeshiEpisode> episodes)
        {
            foreach (Match match in _episodeCardRegex.Matches(html))
            {
                var block = match.Value;
                var videoId = ExtractVideoId(block);
                if (!videoId.HasValue)
                {
                    continue;
                }

                var title = FirstNonEmpty(
                    NormalizeTitle(_titleTextRegex.Match(block).Groups[1].Value),
                    NormalizeTitle(_titleAttributeRegex.Match(block).Groups[1].Value),
                    NormalizeTitle(_episodeDescriptionRegex.Match(block).Groups[1].Value));

                AddEpisode(episodes, videoId.Value, title);
            }
        }

        private static void AppendEpisodesFromAnchors(string html, ICollection<LeshiEpisode> episodes)
        {
            foreach (Match match in _episodeHtmlRegex.Matches(html))
            {
                if (!long.TryParse(match.Groups[1].Value, out var videoId))
                {
                    continue;
                }

                var title = FirstNonEmpty(
                    NormalizeTitle(match.Groups[3].Value),
                    NormalizeTitle(match.Groups[2].Value));

                AddEpisode(episodes, videoId, title);
            }

            foreach (Match match in _episodeLinkRegex.Matches(html))
            {
                if (!long.TryParse(match.Groups[1].Value, out var videoId))
                {
                    continue;
                }

                var title = NormalizeTitle(match.Groups[2].Value);
                AddEpisode(episodes, videoId, title);
            }
        }

        private static long? ExtractVideoId(string html)
        {
            var match = _episodeLinkRegex.Match(html);
            if (match.Success && long.TryParse(match.Groups[1].Value, out var videoId))
            {
                return videoId;
            }

            match = _episodeHtmlRegex.Match(html);
            if (match.Success && long.TryParse(match.Groups[1].Value, out videoId))
            {
                return videoId;
            }

            match = _episodeHrefRegex.Match(html);
            if (match.Success && long.TryParse(match.Groups[1].Value, out videoId))
            {
                return videoId;
            }

            return null;
        }

        private static void AppendRawVideoLinks(string html, ICollection<LeshiEpisode> episodes)
        {
            foreach (Match match in _episodeHrefRegex.Matches(html))
            {
                if (!long.TryParse(match.Groups[1].Value, out var videoId))
                {
                    continue;
                }

                AddEpisode(episodes, videoId, null);
            }
        }

        private static void AppendCurrentVideoFromBootstrap(string html, ICollection<LeshiEpisode> episodes)
        {
            var match = _bootstrapCurrentVideoRegex.Match(html);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out var videoId))
            {
                return;
            }

            AddEpisode(episodes, videoId, "第1集");
        }

        private static void AddEpisode(ICollection<LeshiEpisode> episodes, long videoId, string title)
        {
            if (episodes.Any(existing => existing.Vid == videoId))
            {
                return;
            }

            if (!string.IsNullOrEmpty(title) && title.IndexOf("预告", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            episodes.Add(new LeshiEpisode
            {
                Vid = videoId,
                Title = string.IsNullOrWhiteSpace(title) ? $"第{episodes.Count + 1}集" : title
            });
        }

        private static string NormalizeTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var decoded = HttpUtility.HtmlDecode(_htmlTagRegex.Replace(value, string.Empty));
            return string.IsNullOrWhiteSpace(decoded)
                ? null
                : Regex.Replace(decoded, @"\s+", " ").Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
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
                                    var items = ParseDanmuItems(json);
                                    _logger.Info("乐视弹幕分段 {0}-{1}s 下载完成，获取到 {2} 条弹幕。", timeBegin, timeEnd, items.Count);
                                    danmuList.AddRange(items);
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

        internal static List<LeshiDanmuItem> ParseDanmuItems(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<LeshiDanmuItem>();
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                return new List<LeshiDanmuItem>();
            }

            // 兼容旧结构: { data: [...] }
            if (dataElement.ValueKind == JsonValueKind.Array)
            {
                var legacy = JsonSerializer.Deserialize<List<LeshiDanmuItem>>(dataElement.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return legacy ?? new List<LeshiDanmuItem>();
            }

            // 兼容新结构: { data: { list: [...] } }
            if (dataElement.ValueKind == JsonValueKind.Object && dataElement.TryGetProperty("list", out var listElement) && listElement.ValueKind == JsonValueKind.Array)
            {
                var items = new List<LeshiDanmuItem>();
                foreach (var item in listElement.EnumerateArray())
                {
                    items.Add(new LeshiDanmuItem
                    {
                        Id = TryGetInt64(item, "id") ?? TryGetInt64(item, "_id") ?? 0,
                        CurrentPoint = TryGetDouble(item, "currentPoint") ?? TryGetDouble(item, "start") ?? 0,
                        Content = TryGetString(item, "content") ?? TryGetString(item, "txt"),
                        FontColor = TryGetString(item, "fontColor") ?? TryGetString(item, "color"),
                        FontSize = TryGetInt32(item, "fontSize") ?? MapFontSize(TryGetString(item, "font")),
                        Position = TryGetInt32(item, "position") ?? 0,
                        Uid = TryGetString(item, "uid"),
                        CreateTime = TryGetInt64(item, "createTime") ?? TryGetInt64(item, "addtime") ?? 0,
                    });
                }

                return items;
            }

            return new List<LeshiDanmuItem>();
        }

        private static int MapFontSize(string font)
        {
            return font?.ToLowerInvariant() switch
            {
                "s" => 18,
                "m" => 25,
                "l" => 36,
                _ => 25,
            };
        }

        private static string TryGetString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }

        private static long? TryGetInt64(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int? TryGetInt32(JsonElement element, string name)
        {
            var value = TryGetInt64(element, name);
            if (!value.HasValue)
            {
                return null;
            }

            if (value.Value > int.MaxValue || value.Value < int.MinValue)
            {
                return null;
            }

            return (int)value.Value;
        }

        private static double? TryGetDouble(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            return null;
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
