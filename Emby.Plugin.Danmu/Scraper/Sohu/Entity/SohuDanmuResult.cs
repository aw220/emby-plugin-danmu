using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Sohu.Entity
{
    public class SohuDanmuResult
    {
        [JsonPropertyName("info")]
        public SohuDanmuInfo Info { get; set; }

        [JsonPropertyName("comments")]
        public List<SohuDanmuComment> Comments { get; set; }
    }

    public class SohuDanmuInfo
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class SohuDanmuComment
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Playback time in seconds (float)
        /// </summary>
        [JsonPropertyName("playTime")]
        public double PlayTime { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        /// <summary>
        /// Font color in hex string (e.g. "ffffff")
        /// </summary>
        [JsonPropertyName("font_color")]
        public string FontColor { get; set; }

        /// <summary>
        /// Position type: 1=scroll, 5=top, 4=bottom
        /// </summary>
        [JsonPropertyName("proterties")]
        public int Properties { get; set; }

        [JsonPropertyName("createTime")]
        public string CreateTime { get; set; }

        [JsonPropertyName("userId")]
        public string UserId { get; set; }
    }
}
