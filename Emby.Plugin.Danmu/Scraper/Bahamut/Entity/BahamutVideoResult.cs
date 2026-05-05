using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Bahamut.Entity
{
    public class BahamutVideoResult
    {
        public BahamutVideoData Data { get; set; }
    }

    public class BahamutVideoData
    {
        public BahamutVideo Video { get; set; }
        public BahamutAnimeDetail Anime { get; set; }
    }

    public class BahamutVideo
    {
        public string Title { get; set; }
        public double Rating { get; set; }
    }

    public class BahamutAnimeDetail
    {
        public string Title { get; set; }
        public string SeasonStart { get; set; }
        public Dictionary<string, List<BahamutEpisode>> Episodes { get; set; }
    }

    public class BahamutEpisode
    {
        public int Episode { get; set; }
        public long VideoSn { get; set; }
    }
}
