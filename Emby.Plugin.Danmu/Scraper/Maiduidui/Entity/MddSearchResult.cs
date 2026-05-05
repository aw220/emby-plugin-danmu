using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Maiduidui.Entity
{
    public class MddSearchResult
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("data")]
        public MddSearchData Data { get; set; }
    }

    public class MddSearchData
    {
        [JsonPropertyName("videoList")]
        public MddSearchVideoList VideoList { get; set; }
    }

    public class MddSearchVideoList
    {
        [JsonPropertyName("results")]
        public List<MddSearchItem> Results { get; set; }
    }

    public class MddSearchItem
    {
        [JsonPropertyName("vodId")]
        public long VodId { get; set; }

        [JsonPropertyName("vodName")]
        public string VodName { get; set; }

        [JsonPropertyName("vodType")]
        public string VodType { get; set; }

        [JsonPropertyName("year")]
        public string Year { get; set; }

        [JsonPropertyName("episodeNum")]
        public int EpisodeNum { get; set; }
    }
}
