using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Maiduidui.Entity
{
    public class MddEpisodeResult
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("data")]
        public List<MddEpisodeGroup> Data { get; set; }
    }

    public class MddEpisodeGroup
    {
        [JsonPropertyName("sactionName")]
        public string SactionName { get; set; }

        [JsonPropertyName("vodSactionItems")]
        public List<MddEpisode> VodSactionItems { get; set; }
    }

    public class MddEpisode
    {
        [JsonPropertyName("itemId")]
        public long ItemId { get; set; }

        [JsonPropertyName("itemName")]
        public string ItemName { get; set; }

        [JsonPropertyName("vodId")]
        public long VodId { get; set; }

        [JsonPropertyName("itemNum")]
        public int ItemNum { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }
}
