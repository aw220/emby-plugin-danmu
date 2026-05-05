using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Common.Net;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scrapers.Migu
{
    public class Migu : AbstractScraper
    {
        public const string ScraperProviderName = "咪咕视频";
        public const string ScraperProviderId = "MiguID";

        private readonly MiguApi _api;

        public Migu(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Migu"))
        {
            _api = new MiguApi(logManager, httpClient);
        }

        public override int DefaultOrder => 8;

        public override bool DefaultEnable => true;

        public override string Name => "咪咕视频";

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
                var title = video.ContName;
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                if (isMovieItemType && video.TypeName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.TypeName == "电影")
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
                    Id = video.ContId,
                    Name = title,
                    Category = video.TypeName,
                    Year = video.Year,
                    EpisodeSize = video.TotalEpisode,
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
                var title = video.ContName;
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                if (isMovieItemType && video.TypeName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.TypeName == "电影")
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
                var pubYear = video.Year ?? 0;
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.LogDebug("[{0}] 发行年份不一致，忽略处理. year: {1} emby: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return video.ContId;
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
            var content = await _api.GetContentAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (content == null)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }

            var media = new ScraperMedia();
            media.Id = id;

            if (content.EpisodeList != null && content.EpisodeList.Count > 0)
            {
                if (isMovieItemType)
                {
                    var firstEp = content.EpisodeList[0];
                    media.CommentId = $"{firstEp.ContId},{firstEp.Duration}";
                }

                foreach (var ep in content.EpisodeList)
                {
                    media.Episodes.Add(new ScraperEpisode()
                    {
                        Id = ep.ContId,
                        CommentId = $"{ep.ContId},{ep.Duration}",
                        Title = !string.IsNullOrEmpty(ep.ContName) ? ep.ContName : $"第{ep.EpisodeIndex}集",
                    });
                }
            }

            return media;
        }

        public override async Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            if (isMovieItemType)
            {
                var content = await _api.GetContentAsync(id, CancellationToken.None).ConfigureAwait(false);
                if (content?.EpisodeList == null || content.EpisodeList.Count <= 0)
                {
                    return null;
                }

                var firstEp = content.EpisodeList[0];
                return new ScraperEpisode() { Id = id, CommentId = $"{firstEp.ContId},{firstEp.Duration}" };
            }

            // For TV episodes, get the season provider id to find the episode's duration
            var season = ((MediaBrowser.Controller.Entities.TV.Episode)item).Season;
            season.ProviderIds.TryGetValue(ScraperProviderId, out var seasonId);
            if (!string.IsNullOrEmpty(seasonId))
            {
                var content = await _api.GetContentAsync(seasonId, CancellationToken.None).ConfigureAwait(false);
                if (content?.EpisodeList != null)
                {
                    foreach (var ep in content.EpisodeList)
                    {
                        if (ep.ContId == id)
                        {
                            return new ScraperEpisode() { Id = id, CommentId = $"{ep.ContId},{ep.Duration}" };
                        }
                    }
                }
            }

            // Fallback: use id as commentId with default duration
            return new ScraperEpisode() { Id = id, CommentId = $"{id},0" };
        }

        public override async Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            // commentId format: epsId,duration
            var arr = commentId.Split(',');
            var epsId = arr[0];
            var duration = arr.Length >= 2 ? arr[1].ToInt() : 7200;

            if (string.IsNullOrEmpty(epsId))
            {
                return null;
            }

            var comments = await _api.GetDanmuContentAsync(epsId, duration, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = epsId.ToLong();
            danmaku.ChatServer = "webapi.miguvideo.com";
            danmaku.ProviderId = ScraperProviderId;

            foreach (var comment in comments)
            {
                var danmakuText = new ScraperDanmakuText();
                danmakuText.Progress = (int)(comment.TimeOffset * 1000); // seconds to ms
                danmakuText.Mode = comment.Position switch
                {
                    1 => 5, // top
                    2 => 4, // bottom
                    _ => 1  // default scroll
                };
                danmakuText.MidHash = $"[migu]{comment.UserId}";
                danmakuText.Content = comment.Body;

                if (!string.IsNullOrEmpty(comment.Id))
                {
                    long.TryParse(comment.Id, out var idLong);
                    danmakuText.Id = idLong;
                }

                // Parse hex color
                if (!string.IsNullOrEmpty(comment.Color))
                {
                    try
                    {
                        danmakuText.Color = System.Convert.ToUInt32(comment.Color.TrimStart('#'), 16);
                    }
                    catch
                    {
                        // default white
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
            var videos = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                var title = video.ContName;
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = video.ContId,
                    Name = title,
                    Category = video.TypeName,
                    Year = video.Year,
                    EpisodeSize = video.TotalEpisode,
                });
            }
            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var content = await _api.GetContentAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (content?.EpisodeList == null)
            {
                return list;
            }

            foreach (var ep in content.EpisodeList)
            {
                var title = !string.IsNullOrEmpty(ep.ContName) ? ep.ContName : $"第{ep.EpisodeIndex}集";
                list.Add(new ScraperEpisode()
                {
                    Id = ep.ContId,
                    CommentId = $"{ep.ContId},{ep.Duration}",
                    Title = title,
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
