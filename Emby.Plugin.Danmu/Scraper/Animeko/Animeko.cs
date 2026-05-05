using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Animeko
{
    public class Animeko : AbstractScraper
    {
        public const string ScraperProviderName = "Animeko";
        public const string ScraperProviderId = "AnimekoID";

        private readonly AnimekoApi _api;

        public Animeko(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Animeko"))
        {
            _api = new AnimekoApi(logManager, jsonSerializer, httpClient);
        }

        public override int DefaultOrder => 4;

        public override bool DefaultEnable => true;

        public override string Name => ScraperProviderName;

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var searchName = this.NormalizeSearchName(item.Name);
            var subjects = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var subject in subjects)
            {
                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{subject.Id}",
                    Name = subject.DisplayName,
                    Category = "动画",
                    Year = subject.Year,
                    EpisodeSize = subject.Eps ?? 0,
                });
            }

            return list;
        }

        public override async Task<string> SearchMediaId(BaseItem item)
        {
            var searchName = this.NormalizeSearchName(item.Name);
            var subjects = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var subject in subjects)
            {
                var title = subject.DisplayName;
                var pubYear = subject.Year;

                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.Info("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score: {2}", title, searchName, score);
                    continue;
                }

                // 检测年份是否一致
                var itemPubYear = item.ProductionYear ?? 0;
                if (itemPubYear > 0 && pubYear.HasValue && pubYear.Value > 0 && itemPubYear != pubYear.Value)
                {
                    log.Info("[{0}] 发行年份不一致，忽略处理. animeko：{1} emby: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return $"{subject.Id}";
            }

            return null;
        }

        public override async Task<ScraperMedia> GetMedia(BaseItem item, string id)
        {
            var subjectId = id.ToLong();
            if (subjectId <= 0)
            {
                return null;
            }

            var episodes = await _api.GetEpisodesAsync(subjectId, CancellationToken.None).ConfigureAwait(false);
            if (episodes == null || episodes.Count == 0)
            {
                log.Info("[{0}]获取不到剧集信息：id={1}", this.Name, subjectId);
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var media = new ScraperMedia();
            media.Id = id;
            media.ProviderId = this.ProviderId;

            if (isMovieItemType && episodes.Count > 0)
            {
                media.CommentId = $"{episodes[0].Id}";
            }

            foreach (var ep in episodes)
            {
                media.Episodes.Add(new ScraperEpisode()
                {
                    Id = $"{ep.Id}",
                    CommentId = $"{ep.Id}",
                    Title = ep.DisplayName,
                });
            }

            return media;
        }

        public override async Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            if (isMovieItemType)
            {
                // id is subjectId
                var subjectId = id.ToLong();
                var episodes = await _api.GetEpisodesAsync(subjectId, CancellationToken.None).ConfigureAwait(false);
                if (episodes == null || episodes.Count <= 0)
                {
                    return null;
                }

                return new ScraperEpisode() { Id = id, CommentId = $"{episodes[0].Id}" };
            }
            else
            {
                // id is episodeId
                var epId = id.ToLong();
                if (epId <= 0)
                {
                    return null;
                }

                return new ScraperEpisode() { Id = id, CommentId = id };
            }
        }

        public override async Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId)
        {
            var episodeId = commentId.ToLong();
            if (episodeId <= 0)
            {
                return null;
            }

            var result = await _api.GetDanmakuAsync(episodeId, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = episodeId;
            danmaku.ChatServer = "danmaku-global.myani.org";
            danmaku.ProviderId = ScraperProviderId;

            if (result?.DanmakuList != null)
            {
                foreach (var dm in result.DanmakuList)
                {
                    var danmakuText = new ScraperDanmakuText();
                    danmakuText.Progress = (int)dm.PlayTimeMs;
                    danmakuText.Mode = MapDanmakuMode(dm.Location);
                    danmakuText.Color = (uint)dm.Color;
                    danmakuText.MidHash = dm.SenderId ?? "";
                    danmakuText.Id = dm.Id != null ? long.TryParse(dm.Id, out var parsedId) ? parsedId : 0 : 0;
                    danmakuText.Content = dm.Text;

                    danmaku.Items.Add(danmakuText);
                }
            }

            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }

        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var list = new List<ScraperSearchInfo>();
            log.Info("SearchForApi={0}", keyword);
            var subjects = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var subject in subjects)
            {
                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{subject.Id}",
                    Name = subject.DisplayName,
                    Category = "动画",
                    Year = subject.Year,
                    EpisodeSize = subject.Eps ?? 0,
                });
            }

            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var subjectId = id.ToLong();
            if (subjectId <= 0)
            {
                return list;
            }

            var episodes = await _api.GetEpisodesAsync(subjectId, CancellationToken.None).ConfigureAwait(false);
            if (episodes != null)
            {
                foreach (var ep in episodes)
                {
                    list.Add(new ScraperEpisode()
                    {
                        Id = $"{ep.Id}",
                        CommentId = $"{ep.Id}",
                        Title = ep.DisplayName,
                    });
                }
            }

            return list;
        }

        public override async Task<ScraperDanmaku> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }

        /// <summary>
        /// Maps Animeko danmaku location to mode.
        /// NORMAL -> 1 (scroll), TOP -> 5, BOTTOM -> 4
        /// </summary>
        private static int MapDanmakuMode(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return 1; // default scroll
            }

            switch (location.ToUpperInvariant())
            {
                case "TOP":
                    return 5;
                case "BOTTOM":
                    return 4;
                case "NORMAL":
                default:
                    return 1; // scroll
            }
        }
    }
}
