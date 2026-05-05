using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Common.Net;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scrapers.Leshi
{
    public class Leshi : AbstractScraper
    {
        public const string ScraperProviderName = "乐视网";
        public const string ScraperProviderId = "LeshiID";

        private readonly LeshiApi _api;

        public Leshi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Leshi"))
        {
            _api = new LeshiApi(logManager, httpClient);
        }

        public override int DefaultOrder => 8;

        public override bool DefaultEnable => true;

        public override string Name => "乐视网";

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var docs = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var doc in docs)
            {
                var title = doc.Title;
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                if (isMovieItemType && doc.CategoryName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && doc.CategoryName == "电影")
                {
                    continue;
                }

                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{doc.Pid}",
                    Name = title,
                    Category = doc.CategoryName,
                    Year = doc.YearInt,
                    EpisodeSize = doc.EpisodeCount,
                });
            }

            return list;
        }

        public override async Task<string> SearchMediaId(BaseItem item)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var docs = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var doc in docs)
            {
                var title = doc.Title;
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                if (isMovieItemType && doc.CategoryName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && doc.CategoryName == "电影")
                {
                    continue;
                }

                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.LogDebug("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score:　{2}", title, searchName, score);
                    continue;
                }

                var itemPubYear = item.ProductionYear ?? 0;
                var pubYear = doc.YearInt ?? 0;
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.LogDebug("[{0}] 发行年份不一致，忽略处理. year: {1} emby: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                // Store aid and contentType together for episode retrieval
                return $"{doc.Pid}|{doc.ContentType}";
            }

            return null;
        }

        public override async Task<ScraperMedia> GetMedia(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;

            // Parse the composite id: aid|contentType
            var parts = id.Split('|');
            var aid = parts[0];
            var contentType = parts.Length > 1 ? parts[1] : "tv";

            var episodes = await _api.GetEpisodesAsync(aid, contentType, CancellationToken.None).ConfigureAwait(false);
            if (episodes == null || episodes.Count == 0)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }

            var media = new ScraperMedia();
            media.Id = id;

            if (isMovieItemType && episodes.Count > 0)
            {
                var firstEp = episodes[0];
                var duration = await _api.GetVideoDurationAsync(firstEp.Vid, CancellationToken.None).ConfigureAwait(false);
                media.CommentId = $"{firstEp.Vid},{duration}";
            }

            foreach (var ep in episodes)
            {
                media.Episodes.Add(new ScraperEpisode()
                {
                    Id = $"{ep.Vid}",
                    CommentId = $"{ep.Vid},{ep.Duration}",
                    Title = ep.Title,
                });
            }

            return media;
        }

        public override async Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // Try to get duration for the episode
            if (long.TryParse(id, out var vid))
            {
                var duration = await _api.GetVideoDurationAsync(vid, CancellationToken.None).ConfigureAwait(false);
                return new ScraperEpisode()
                {
                    Id = id,
                    CommentId = $"{vid},{duration}"
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

            // commentId format: vid,duration
            var arr = commentId.Split(',');
            if (arr.Length < 1)
            {
                return null;
            }

            var vidStr = arr[0];
            var duration = arr.Length >= 2 ? arr[1].ToInt() : 7200;

            if (string.IsNullOrEmpty(vidStr) || !long.TryParse(vidStr, out var vid))
            {
                return null;
            }

            // If duration is 0 or negative, try to fetch it
            if (duration <= 0)
            {
                duration = await _api.GetVideoDurationAsync(vid, CancellationToken.None).ConfigureAwait(false);
            }

            var comments = await _api.GetDanmuContentAsync(vid, duration, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = vid;
            danmaku.ChatServer = "hd-my.le.com";
            danmaku.ProviderId = ScraperProviderId;

            foreach (var comment in comments)
            {
                var danmakuText = new ScraperDanmakuText();
                danmakuText.Id = comment.Id;
                danmakuText.Progress = (int)(comment.CurrentPoint * 1000); // convert seconds to ms
                // Position mapping: 0=scroll(1), 1=top(5), 2=bottom(4)
                danmakuText.Mode = comment.Position switch
                {
                    1 => 5, // top
                    2 => 4, // bottom
                    _ => 1  // default scroll
                };
                danmakuText.Fontsize = comment.FontSize > 0 ? comment.FontSize : 25;
                danmakuText.MidHash = $"[leshi]{comment.Uid}";
                danmakuText.Content = comment.Content;
                danmakuText.Ctime = comment.CreateTime;

                // Parse hex color
                if (!string.IsNullOrEmpty(comment.FontColor))
                {
                    try
                    {
                        danmakuText.Color = Convert.ToUInt32(comment.FontColor.TrimStart('#'), 16);
                    }
                    catch
                    {
                        // default white (16777215)
                    }
                }

                danmaku.Items.Add(danmakuText);
            }

            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }

        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var list = new List<ScraperSearchInfo>();
            var docs = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var doc in docs)
            {
                var title = doc.Title;
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{doc.Pid}|{doc.ContentType}",
                    Name = title,
                    Category = doc.CategoryName,
                    Year = doc.YearInt,
                    EpisodeSize = doc.EpisodeCount,
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

            // Parse the composite id: aid|contentType
            var parts = id.Split('|');
            var aid = parts[0];
            var contentType = parts.Length > 1 ? parts[1] : "tv";

            var episodes = await _api.GetEpisodesAsync(aid, contentType, CancellationToken.None).ConfigureAwait(false);
            if (episodes == null)
            {
                return list;
            }

            foreach (var ep in episodes)
            {
                list.Add(new ScraperEpisode()
                {
                    Id = $"{ep.Vid}",
                    CommentId = $"{ep.Vid},{ep.Duration}",
                    Title = ep.Title,
                });
            }

            return list;
        }

        public override async Task<ScraperDanmaku> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }
    }
}
