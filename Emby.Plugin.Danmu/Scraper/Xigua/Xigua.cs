using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Common.Net;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scrapers.Xigua
{
    public class Xigua : AbstractScraper
    {
        public const string ScraperProviderName = "西瓜视频";
        public const string ScraperProviderId = "XiguaID";

        private readonly XiguaApi _api;

        public Xigua(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Xigua"))
        {
            _api = new XiguaApi(logManager, httpClient);
        }

        public override int DefaultOrder => 7;

        public override bool DefaultEnable => true;

        public override string Name => "西瓜视频";

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(video.Title);
                if (score < 0.7)
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = video.Id,
                    Name = video.Title,
                    Category = video.Category ?? "视频",
                    Year = video.Year,
                    EpisodeSize = 1, // Xigua videos are typically single videos
                });
            }

            return list;
        }

        public override async Task<string?> SearchMediaId(BaseItem item)
        {
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                var title = video.Title;

                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.LogDebug("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score: {2}", title, searchName, score);
                    continue;
                }

                // 检测年份是否一致
                var itemPubYear = item.ProductionYear ?? 0;
                if (itemPubYear > 0 && video.Year.HasValue && video.Year.Value > 0 && itemPubYear != video.Year.Value)
                {
                    log.LogDebug("[{0}] 发行年份不一致，忽略处理. year: {1} emby: {2}", title, video.Year, itemPubYear);
                    continue;
                }

                return video.Id;
            }

            return null;
        }

        public override async Task<ScraperMedia?> GetMedia(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // Xigua videos are single items (not series), so each video is its own media
            var media = new ScraperMedia();
            media.Id = id;
            media.CommentId = id;
            media.ProviderId = ScraperProviderId;
            media.Episodes.Add(new ScraperEpisode() { Id = id, CommentId = id });

            return media;
        }

        public override async Task<ScraperEpisode?> GetMediaEpisode(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return new ScraperEpisode() { Id = id, CommentId = id };
        }

        public override async Task<ScraperDanmaku?> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            var comments = await _api.GetDanmuContentAsync(commentId, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = commentId.ToLong();
            danmaku.ChatServer = "ib.snssdk.com";
            danmaku.ProviderId = ScraperProviderId;

            foreach (var comment in comments)
            {
                var danmakuText = new ScraperDanmakuText();
                danmakuText.Id = comment.DanmakuId;
                danmakuText.Progress = comment.Position;
                // Map danmu mode: default scroll=1, bottom=4, top=5
                danmakuText.Mode = comment.Mode switch
                {
                    4 => 4, // bottom
                    5 => 5, // top
                    _ => 1  // scroll (default)
                };
                danmakuText.MidHash = $"[xigua]{comment.UserId}";
                danmakuText.Content = comment.Content;
                danmakuText.Ctime = comment.CreateTime;

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
                list.Add(new ScraperSearchInfo()
                {
                    Id = video.Id,
                    Name = video.Title,
                    Category = video.Category ?? "视频",
                    Year = video.Year,
                    EpisodeSize = 1,
                });
            }
            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            if (!string.IsNullOrEmpty(id))
            {
                list.Add(new ScraperEpisode() { Id = id, CommentId = id, Title = "视频" });
            }
            return list;
        }

        public override async Task<ScraperDanmaku?> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }
    }
}
