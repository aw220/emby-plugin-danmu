using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Common.Net;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scrapers.Sohu
{
    public class Sohu : AbstractScraper
    {
        public const string ScraperProviderName = "搜狐视频";
        public const string ScraperProviderId = "SohuID";

        private readonly SohuApi _api;

        public Sohu(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Sohu"))
        {
            _api = new SohuApi(logManager, httpClient);
        }

        public override int DefaultOrder => 7;

        public override bool DefaultEnable => true;

        public override string Name => "搜狐视频";

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
                var title = doc.AlbumName;
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
                    Id = $"{doc.PlaylistId}",
                    Name = title,
                    Category = doc.CategoryName,
                    Year = doc.Year,
                    EpisodeSize = doc.TotalCount,
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
                var title = doc.AlbumName;
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
                var pubYear = doc.Year ?? 0;
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.LogDebug("[{0}] 发行年份不一致，忽略处理. year: {1} emby: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return $"{doc.PlaylistId}";
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
            var playlist = await _api.GetPlaylistAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (playlist == null)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }

            var media = new ScraperMedia();
            media.Id = id;

            if (isMovieItemType && playlist.Videos != null && playlist.Videos.Count > 0)
            {
                var firstVideo = playlist.Videos[0];
                media.CommentId = $"{firstVideo.Vid},{firstVideo.Aid},{firstVideo.Duration}";
            }

            if (playlist.Videos != null)
            {
                foreach (var video in playlist.Videos)
                {
                    media.Episodes.Add(new ScraperEpisode()
                    {
                        Id = $"{video.Vid}",
                        CommentId = $"{video.Vid},{video.Aid},{video.Duration}",
                        Title = video.Name,
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
                // For movies, get playlist and return first episode
                var season = ((MediaBrowser.Controller.Entities.TV.Episode)item).Season;
                season.ProviderIds.TryGetValue(ScraperProviderId, out var playlistId);
                if (!string.IsNullOrEmpty(playlistId))
                {
                    var playlist = await _api.GetPlaylistAsync(playlistId, CancellationToken.None).ConfigureAwait(false);
                    if (playlist?.Videos != null && playlist.Videos.Count > 0)
                    {
                        var firstVideo = playlist.Videos[0];
                        return new ScraperEpisode()
                        {
                            Id = id,
                            CommentId = $"{firstVideo.Vid},{firstVideo.Aid},{firstVideo.Duration}"
                        };
                    }
                }
                return null;
            }

            // For TV episodes, get the season's playlist id and find matching episode
            var episodeSeason = ((MediaBrowser.Controller.Entities.TV.Episode)item).Season;
            episodeSeason.ProviderIds.TryGetValue(ScraperProviderId, out var seasonPlaylistId);
            if (!string.IsNullOrEmpty(seasonPlaylistId))
            {
                var playlist = await _api.GetPlaylistAsync(seasonPlaylistId, CancellationToken.None).ConfigureAwait(false);
                if (playlist?.Videos != null)
                {
                    foreach (var video in playlist.Videos)
                    {
                        if ($"{video.Vid}" == id)
                        {
                            return new ScraperEpisode()
                            {
                                Id = id,
                                CommentId = $"{video.Vid},{video.Aid},{video.Duration}"
                            };
                        }
                    }
                }
            }

            return new ScraperEpisode() { Id = id, CommentId = id };
        }

        public override async Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            // commentId format: vid,aid,duration
            var arr = commentId.Split(',');
            if (arr.Length < 2)
            {
                return null;
            }

            var vid = arr[0];
            var aid = arr[1];
            var duration = arr.Length >= 3 ? arr[2].ToInt() : 7200;

            if (string.IsNullOrEmpty(vid) || string.IsNullOrEmpty(aid))
            {
                return null;
            }

            var comments = await _api.GetDanmuContentAsync(vid, aid, duration, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = vid.ToLong();
            danmaku.ChatServer = "api.danmu.tv.sohu.com";
            danmaku.ProviderId = ScraperProviderId;

            foreach (var comment in comments)
            {
                var danmakuText = new ScraperDanmakuText();
                danmakuText.Id = comment.Id;
                danmakuText.Progress = (int)(comment.PlayTime * 1000); // convert seconds to ms
                // Position mapping: scroll=1, top=5, bottom=4
                danmakuText.Mode = comment.Properties switch
                {
                    5 => 5, // top
                    4 => 4, // bottom
                    _ => 1  // default scroll
                };
                danmakuText.MidHash = $"[sohu]{comment.UserId}";
                danmakuText.Content = comment.Content;

                // Parse hex color
                if (!string.IsNullOrEmpty(comment.FontColor))
                {
                    try
                    {
                        danmakuText.Color = System.Convert.ToUInt32(comment.FontColor.TrimStart('#'), 16);
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
            var docs = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var doc in docs)
            {
                var title = doc.AlbumName;
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{doc.PlaylistId}",
                    Name = title,
                    Category = doc.CategoryName,
                    Year = doc.Year,
                    EpisodeSize = doc.TotalCount,
                });
            }
            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var playlist = await _api.GetPlaylistAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (playlist?.Videos == null)
            {
                return list;
            }

            foreach (var video in playlist.Videos)
            {
                list.Add(new ScraperEpisode()
                {
                    Id = $"{video.Vid}",
                    CommentId = $"{video.Vid},{video.Aid},{video.Duration}",
                    Title = video.Name,
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
