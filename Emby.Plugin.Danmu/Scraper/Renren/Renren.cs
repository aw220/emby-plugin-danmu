using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Renren
{
    public class Renren : AbstractScraper
    {
        public const string ScraperProviderName = "人人视频";
        public const string ScraperProviderId = "RenrenID";

        private readonly RenrenApi _api;

        public Renren(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Renren"))
        {
            _api = new RenrenApi(logManager, jsonSerializer, httpClient);
        }

        public override int DefaultOrder => 8;

        public override bool DefaultEnable => true;

        public override string Name => ScraperProviderName;

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var searchName = this.NormalizeSearchName(item.Name);
            var items = await _api.SearchTvAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var drama in items)
            {
                var id = !string.IsNullOrEmpty(drama.SeriesId) ? drama.SeriesId : drama.Id;
                int? year = null;
                if (!string.IsNullOrEmpty(drama.Year) && int.TryParse(drama.Year, out var y))
                {
                    year = y;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = id,
                    Name = drama.Title ?? drama.SeasonName,
                    Category = drama.Category ?? "影视",
                    Year = year,
                    EpisodeSize = drama.EpisodeNum ?? 0,
                });
            }

            return list;
        }

        public override async Task<string> SearchMediaId(BaseItem item)
        {
            var searchName = this.NormalizeSearchName(item.Name);
            var items = await _api.SearchTvAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var drama in items)
            {
                var title = drama.Title ?? drama.SeasonName ?? "";

                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.Info("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score: {2}", title, searchName, score);
                    continue;
                }

                // 检测年份是否一致
                var itemPubYear = item.ProductionYear ?? 0;
                int pubYear = 0;
                if (!string.IsNullOrEmpty(drama.Year))
                {
                    int.TryParse(drama.Year, out pubYear);
                }
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.Info("[{0}] 发行年份不一致，忽略处理. renren：{1} emby: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return !string.IsNullOrEmpty(drama.SeriesId) ? drama.SeriesId : drama.Id;
            }

            return null;
        }

        public override async Task<ScraperMedia> GetMedia(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var detail = await _api.GetDetailAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (detail == null)
            {
                log.Info("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var media = new ScraperMedia();
            media.Id = id;
            media.ProviderId = this.ProviderId;

            if (detail.Episodes != null)
            {
                for (int i = 0; i < detail.Episodes.Count; i++)
                {
                    var ep = detail.Episodes[i];
                    var episodeId = !string.IsNullOrEmpty(ep.EpisodeId) ? ep.EpisodeId : ep.Id;
                    // CommentId uses composite format: SeriesId-EpisodeId
                    var commentId = $"{id}-{episodeId}";

                    if (isMovieItemType && i == 0)
                    {
                        media.CommentId = commentId;
                    }

                    media.Episodes.Add(new ScraperEpisode()
                    {
                        Id = episodeId,
                        CommentId = commentId,
                        Title = ep.Title ?? ep.EpisodeName ?? $"第{i + 1}集",
                    });
                }
            }

            return media;
        }

        public override async Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // id format: SeriesId-EpisodeId
            var parts = id.Split('-');
            if (parts.Length >= 2)
            {
                var episodeId = parts[1];
                return new ScraperEpisode()
                {
                    Id = episodeId,
                    CommentId = id,
                };
            }

            return new ScraperEpisode() { Id = id, CommentId = id };
        }

        public override async Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            // commentId format: SeriesId-EpisodeId, we need the EpisodeId part for danmu
            var episodeId = commentId;
            var parts = commentId.Split('-');
            if (parts.Length >= 2)
            {
                episodeId = parts[1];
            }

            var danmuList = await _api.GetDanmuAsync(episodeId, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = episodeId.GetHashCode();
            danmaku.ChatServer = "api.gorafie.com";
            danmaku.ProviderId = ScraperProviderId;

            foreach (var d in danmuList)
            {
                var danmakuText = new ScraperDanmakuText();
                danmakuText.Id = d.Id?.GetHashCode() ?? 0;
                danmakuText.Progress = (int)d.Time; // Time is already in milliseconds
                danmakuText.Mode = ConvertTypeToMode(d.Type);
                danmakuText.Color = ParseColor(d.Color);
                danmakuText.MidHash = "[renren]" + (d.UserId ?? "");
                danmakuText.Content = d.Content;

                danmaku.Items.Add(danmakuText);
            }

            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }

        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var list = new List<ScraperSearchInfo>();
            log.Info("SearchForApi={0}", keyword);
            var items = await _api.SearchTvAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var drama in items)
            {
                var id = !string.IsNullOrEmpty(drama.SeriesId) ? drama.SeriesId : drama.Id;
                int? year = null;
                if (!string.IsNullOrEmpty(drama.Year) && int.TryParse(drama.Year, out var y))
                {
                    year = y;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = id,
                    Name = drama.Title ?? drama.SeasonName,
                    Category = drama.Category ?? "影视",
                    Year = year,
                    EpisodeSize = drama.EpisodeNum ?? 0,
                });
            }

            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            if (string.IsNullOrEmpty(id))
            {
                return list;
            }

            var detail = await _api.GetDetailAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (detail?.Episodes == null)
            {
                return list;
            }

            for (int i = 0; i < detail.Episodes.Count; i++)
            {
                var ep = detail.Episodes[i];
                var episodeId = !string.IsNullOrEmpty(ep.EpisodeId) ? ep.EpisodeId : ep.Id;
                var commentId = $"{id}-{episodeId}";

                list.Add(new ScraperEpisode()
                {
                    Id = episodeId,
                    CommentId = commentId,
                    Title = ep.Title ?? ep.EpisodeName ?? $"第{i + 1}集",
                });
            }

            return list;
        }

        public override async Task<ScraperDanmaku> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }

        /// <summary>
        /// 人人弹幕类型转换为标准模式
        /// 0=滚动(1), 1=顶部(5), 2=底部(4)
        /// </summary>
        private int ConvertTypeToMode(int type)
        {
            switch (type)
            {
                case 0: return 1; // 滚动弹幕
                case 1: return 5; // 顶部弹幕
                case 2: return 4; // 底部弹幕
                default: return 1;
            }
        }

        /// <summary>
        /// 解析颜色字符串为 uint
        /// </summary>
        private uint ParseColor(string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                return 16777215; // white
            }

            try
            {
                var hex = color.TrimStart('#');
                return uint.Parse(hex, NumberStyles.HexNumber);
            }
            catch
            {
                return 16777215;
            }
        }
    }
}
