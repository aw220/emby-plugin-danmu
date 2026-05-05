using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Leshi.Entity
{
    public class LeshiDanmuResult
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("data")]
        public List<LeshiDanmuItem> Data { get; set; }
    }

    public class LeshiDanmuItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Time offset in seconds (float).
        /// </summary>
        [JsonPropertyName("currentPoint")]
        public double CurrentPoint { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        /// <summary>
        /// Font color as hex string (e.g. "ffffff").
        /// </summary>
        [JsonPropertyName("fontColor")]
        public string FontColor { get; set; }

        /// <summary>
        /// Font size.
        /// </summary>
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; }

        /// <summary>
        /// Position type: 0=scroll, 1=top, 2=bottom.
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("createTime")]
        public long CreateTime { get; set; }
    }
}
