using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Renren.Entity
{
    public class RenrenDetailResult
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public RenrenDetailData Data { get; set; }
    }

    public class RenrenDetailData
    {
        public string Id { get; set; }
        public string SeriesId { get; set; }
        public string Title { get; set; }
        public string SeasonName { get; set; }
        public string Year { get; set; }
        public List<RenrenEpisodeItem> Episodes { get; set; }
    }

    public class RenrenEpisodeItem
    {
        public string Id { get; set; }
        public string EpisodeId { get; set; }
        public string Title { get; set; }
        public string EpisodeName { get; set; }
        public int? Sort { get; set; }
    }
}
