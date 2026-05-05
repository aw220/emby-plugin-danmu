using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper.Maiduidui
{
    public class Maiduidui : AbstractScraper
    {
        public const string ScraperProviderName = "埋堆堆";
        public const string ScraperProviderId = "MaiduiduiID";

        private readonly MaiduiduiApi _api;

        public Maiduidui(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Maiduidui"))
        {
            _api = new MaiduiduiApi(logManager, httpClient);
        }

        public override int DefaultOrder => 8;

        public override bool DefaultEnable => true;

        public override string Name => ScraperProviderName;

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);

            foreach (var video in videos)
            {
                if (isMovieItemType && video.VodType != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.VodType == "电影")
                {
                    continue;
                }

                // Check title similarity
                var score = searchName.Distance(video.VodName);
                if (score < 0.7)
                {
                    continue;
                }

                int? year = null;
                if (!string.IsNullOrEmpty(video.Year) && int.TryParse(video.Year, out var parsedYear))
                {
                    year = parsedYear;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{video.VodId}",
                    Name = video.VodName,
                    Category = video.VodType,
                    Year = year,
                    EpisodeSize = video.EpisodeNum,
                });
            }

            return list;
        }

        public override async Task<string> SearchMediaId(BaseItem item)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);

            foreach (var video in videos)
            {
                if (isMovieItemType && video.VodType != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.VodType == "电影")
                {
                    continue;
                }

                // Check title similarity
                var score = searchName.Distance(video.VodName);
                if (score < 0.7)
                {
                    log.Info("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score: {2}", video.VodName, searchName, score);
                    continue;
                }

                // Check year consistency
                var itemPubYear = item.ProductionYear ?? 0;
                if (itemPubYear > 0 && !string.IsNullOrEmpty(video.Year) && int.TryParse(video.Year, out var pubYear))
                {
                    if (pubYear > 0 && itemPubYear != pubYear)
                    {
                        log.Info("[{0}] 发行年份不一致，忽略处理. mdd: {1} emby: {2}", video.VodName, pubYear, itemPubYear);
                        continue;
                    }
                }

                return $"{video.VodId}";
            }

            return null;
        }

        public override async Task<ScraperMedia> GetMedia(BaseItem item, string id)
        {
            var vodId = id.ToLong();
            if (vodId <= 0)
            {
                return null;
            }

            var episodes = await _api.GetEpisodesAsync(vodId, CancellationToken.None).ConfigureAwait(false);
            if (episodes == null || episodes.Count == 0)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, vodId);
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var media = new ScraperMedia();
            media.Id = id;
            media.ProviderId = this.ProviderId;

            if (isMovieItemType && episodes.Count > 0)
            {
                // For movies, use first episode's itemId and duration as commentId
                media.CommentId = $"{episodes[0].ItemId},{episodes[0].Duration}";
            }

            foreach (var ep in episodes)
            {
                media.Episodes.Add(new ScraperEpisode()
                {
                    Id = $"{ep.ItemId}",
                    CommentId = $"{ep.ItemId},{ep.Duration}",
                    Title = ep.ItemName,
                });
            }

            return media;
        }

        public override async Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            if (isMovieItemType)
            {
                var vodId = id.ToLong();
                var episodes = await _api.GetEpisodesAsync(vodId, CancellationToken.None).ConfigureAwait(false);
                if (episodes == null || episodes.Count <= 0)
                {
                    return null;
                }

                return new ScraperEpisode()
                {
                    Id = id,
                    CommentId = $"{episodes[0].ItemId},{episodes[0].Duration}",
                };
            }

            // For TV episodes, id is the itemId already
            // Parse the commentId from the stored format
            return new ScraperEpisode() { Id = id, CommentId = id };
        }

        public override async Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            // commentId format: "itemId,duration"
            var parts = commentId.Split(',');
            var itemId = parts[0].ToLong();
            var duration = parts.Length > 1 ? (int)parts[1].ToLong() : 3600;

            if (itemId <= 0)
            {
                return null;
            }

            var comments = await _api.GetDanmuAsync(itemId, duration, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = itemId;
            danmaku.ChatServer = "mob.mddcloud.com.cn";
            danmaku.ProviderId = ScraperProviderId;

            foreach (var comment in comments)
            {
                var danmakuText = new ScraperDanmakuText();
                danmakuText.Id = comment.BarrageId;
                danmakuText.Progress = (int)(comment.TimeOffset * 1000); // Convert seconds to ms
                // Map mode: 0=scroll -> 1, 1=top -> 5, 2=bottom -> 4
                danmakuText.Mode = comment.Type switch
                {
                    1 => 5, // top
                    2 => 4, // bottom
                    _ => 1  // scroll (default)
                };
                danmakuText.Content = comment.Content;
                danmakuText.MidHash = $"[mdd]{comment.UserId}";

                // Parse color from hex string
                if (!string.IsNullOrEmpty(comment.Color))
                {
                    try
                    {
                        var colorStr = comment.Color.TrimStart('#');
                        danmakuText.Color = Convert.ToUInt32(colorStr, 16);
                    }
                    catch
                    {
                        danmakuText.Color = 0xFFFFFF; // Default white
                    }
                }
                else
                {
                    danmakuText.Color = 0xFFFFFF;
                }

                danmaku.Items.Add(danmakuText);
            }

            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }

        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var list = new List<ScraperSearchInfo>();
            var videos = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);

            foreach (var video in videos)
            {
                int? year = null;
                if (!string.IsNullOrEmpty(video.Year) && int.TryParse(video.Year, out var parsedYear))
                {
                    year = parsedYear;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{video.VodId}",
                    Name = video.VodName,
                    Category = video.VodType,
                    Year = year,
                    EpisodeSize = video.EpisodeNum,
                });
            }

            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var vodId = id.ToLong();
            if (vodId <= 0)
            {
                return list;
            }

            var episodes = await _api.GetEpisodesAsync(vodId, CancellationToken.None).ConfigureAwait(false);
            if (episodes != null)
            {
                foreach (var ep in episodes)
                {
                    list.Add(new ScraperEpisode()
                    {
                        Id = $"{ep.ItemId}",
                        CommentId = $"{ep.ItemId},{ep.Duration}",
                        Title = ep.ItemName,
                    });
                }
            }

            return list;
        }

        public override async Task<ScraperDanmaku> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }
    }
}
