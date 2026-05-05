using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Renren.Entity
{
    public class RenrenSearchResult
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public RenrenSearchData Data { get; set; }
    }

    public class RenrenSearchData
    {
        public List<RenrenSearchItem> Items { get; set; }
    }

    public class RenrenSearchItem
    {
        public string Id { get; set; }
        public string SeriesId { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Year { get; set; }
        public int? EpisodeCount { get; set; }
        public string Cover { get; set; }
    }
}
