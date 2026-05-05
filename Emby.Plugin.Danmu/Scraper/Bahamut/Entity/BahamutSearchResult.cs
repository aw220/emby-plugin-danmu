using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Bahamut.Entity
{
    public class BahamutSearchResult
    {
        public BahamutSearchData Data { get; set; }
    }

    public class BahamutSearchData
    {
        public List<BahamutAnime> Anime { get; set; }
    }

    public class BahamutAnime
    {
        public string Title { get; set; }
        public long Video_sn { get; set; }
        public string Cover { get; set; }
        public string Info { get; set; }
    }
}
