using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Renren.Entity
{
    public class RenrenTvSearchResult
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public RenrenTvSearchData Data { get; set; }
    }

    public class RenrenTvSearchData
    {
        public List<RenrenTvSearchItem> Items { get; set; }
    }

    public class RenrenTvSearchItem
    {
        public string Id { get; set; }
        public string SeriesId { get; set; }
        public string Title { get; set; }
        public string SeasonName { get; set; }
        public string Category { get; set; }
        public string Year { get; set; }
        public int? EpisodeNum { get; set; }
        public string Cover { get; set; }
    }
}
