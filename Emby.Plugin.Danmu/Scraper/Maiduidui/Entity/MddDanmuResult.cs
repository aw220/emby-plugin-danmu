using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Maiduidui.Entity
{
    public class MddDanmuResult
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("data")]
        public MddDanmuData Data { get; set; }
    }

    public class MddDanmuData
    {
        [JsonPropertyName("barrages")]
        public List<MddDanmu> Barrages { get; set; }
    }

    public class MddDanmu
    {
        [JsonPropertyName("barrageId")]
        public long BarrageId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        /// <summary>
        /// Time offset in seconds from start of the segment
        /// </summary>
        [JsonPropertyName("timeOffset")]
        public double TimeOffset { get; set; }

        /// <summary>
        /// Danmu mode: 0=scroll, 1=top, 2=bottom
        /// </summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("userId")]
        public string UserId { get; set; }
    }
}
