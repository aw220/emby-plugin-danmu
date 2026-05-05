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

namespace Emby.Plugin.Danmu.Scraper.Bahamut
{
    public class Bahamut : AbstractScraper
    {
        public const string ScraperProviderName = "巴哈姆特";
        public const string ScraperProviderId = "BahamutID";

        private readonly BahamutApi _api;

        public Bahamut(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Bahamut"))
        {
            _api = new BahamutApi(logManager, jsonSerializer, httpClient);
        }

        public override int DefaultOrder => 5;

        public override bool DefaultEnable => true;

        public override string Name => ScraperProviderName;

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var searchName = this.NormalizeSearchName(item.Name);
            var animes = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var anime in animes)
            {
                var year = ExtractYear(anime.Info);
                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{anime.Video_sn}",
                    Name = anime.Title,
                    Category = "动漫",
                    Year = year,
                    EpisodeSize = 0,
                });
            }

            return list;
        }

        public override async Task<string> SearchMediaId(BaseItem item)
        {
            var searchName = this.NormalizeSearchName(item.Name);
            var animes = await _api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var anime in animes)
            {
                var title = anime.Title;

                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.Info("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score: {2}", title, searchName, score);
                    continue;
                }

                // 检测年份是否一致
                var itemPubYear = item.ProductionYear ?? 0;
                var pubYear = ExtractYear(anime.Info);
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.Info("[{0}] 发行年份不一致，忽略处理. bahamut：{1} emby: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return $"{anime.Video_sn}";
            }

            return null;
        }

        public override async Task<ScraperMedia> GetMedia(BaseItem item, string id)
        {
            var videoSn = id.ToLong();
            if (videoSn <= 0)
            {
                return null;
            }

            var videoData = await _api.GetVideoAsync(videoSn, CancellationToken.None).ConfigureAwait(false);
            if (videoData == null)
            {
                log.Info("[{0}]获取不到视频信息：id={1}", this.Name, videoSn);
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var media = new ScraperMedia();
            media.Id = id;
            media.ProviderId = this.ProviderId;

            var episodes = _api.ExtractEpisodes(videoData);
            if (isMovieItemType && episodes.Count > 0)
            {
                media.CommentId = $"{episodes[0].VideoSn}";
            }

            foreach (var ep in episodes)
            {
                media.Episodes.Add(new ScraperEpisode()
                {
                    Id = $"{ep.VideoSn}",
                    CommentId = $"{ep.VideoSn}",
                    Title = $"第{ep.Episode}集",
                });
            }

            return media;
        }

        public override async Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            if (isMovieItemType)
            {
                var videoData = await _api.GetVideoAsync(id.ToLong(), CancellationToken.None).ConfigureAwait(false);
                var episodes = _api.ExtractEpisodes(videoData);
                if (episodes == null || episodes.Count <= 0)
                {
                    return null;
                }

                return new ScraperEpisode() { Id = id, CommentId = $"{episodes[0].VideoSn}" };
            }
            else
            {
                var videoSn = id.ToLong();
                if (videoSn <= 0)
                {
                    return null;
                }

                return new ScraperEpisode() { Id = id, CommentId = id };
            }
        }

        public override async Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId)
        {
            var videoSn = commentId.ToLong();
            if (videoSn <= 0)
            {
                return null;
            }

            var danmuList = await _api.GetDanmuAsync(videoSn, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = videoSn;
            danmaku.ChatServer = "api.gamer.com.tw";
            danmaku.ProviderId = ScraperProviderId;

            // position mapping: 0=scroll(1), 1=top(5), 2=bottom(4)
            foreach (var d in danmuList)
            {
                var danmakuText = new ScraperDanmakuText();
                danmakuText.Id = d.Sn;
                // time is in tenths of seconds, convert to milliseconds
                danmakuText.Progress = d.Time * 100;
                danmakuText.Mode = ConvertPositionToMode(d.Position);
                danmakuText.Color = ParseColor(d.Color);
                danmakuText.MidHash = "[bahamut]";
                danmakuText.Content = d.Text;

                danmaku.Items.Add(danmakuText);
            }

            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }

        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var list = new List<ScraperSearchInfo>();
            log.Info("SearchForApi={0}", keyword);
            var animes = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var anime in animes)
            {
                var year = ExtractYear(anime.Info);
                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{anime.Video_sn}",
                    Name = anime.Title,
                    Category = "动漫",
                    Year = year,
                    EpisodeSize = 0,
                });
            }

            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var videoSn = id.ToLong();
            if (videoSn <= 0)
            {
                return list;
            }

            var videoData = await _api.GetVideoAsync(videoSn, CancellationToken.None).ConfigureAwait(false);
            if (videoData == null)
            {
                return list;
            }

            var episodes = _api.ExtractEpisodes(videoData);
            foreach (var ep in episodes)
            {
                list.Add(new ScraperEpisode()
                {
                    Id = $"{ep.VideoSn}",
                    CommentId = $"{ep.VideoSn}",
                    Title = $"第{ep.Episode}集",
                });
            }

            return list;
        }

        public override async Task<ScraperDanmaku> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }

        /// <summary>
        /// 从 info 字符串中提取年份
        /// </summary>
        private int? ExtractYear(string info)
        {
            if (string.IsNullOrEmpty(info))
            {
                return null;
            }

            var match = System.Text.RegularExpressions.Regex.Match(info, @"(\d{4})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
            {
                return year;
            }

            return null;
        }

        /// <summary>
        /// 巴哈姆特弹幕位置转换为标准模式
        /// 0=滚动(1), 1=顶部(5), 2=底部(4)
        /// </summary>
        private int ConvertPositionToMode(int position)
        {
            switch (position)
            {
                case 0: return 1; // 滚动弹幕
                case 1: return 5; // 顶部弹幕
                case 2: return 4; // 底部弹幕
                default: return 1;
            }
        }

        /// <summary>
        /// 解析颜色字符串（如 "#FFFFFF"）为 uint
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
