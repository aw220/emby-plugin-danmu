using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Migu.Entity
{
    public class MiguDanmuResult
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("data")]
        public MiguDanmuData Data { get; set; }
    }

    public class MiguDanmuData
    {
        [JsonPropertyName("list")]
        public List<MiguDanmuItem> List { get; set; }
    }

    public class MiguDanmuItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Barrage content text
        /// </summary>
        [JsonPropertyName("body")]
        public string Body { get; set; }

        /// <summary>
        /// Time offset in seconds
        /// </summary>
        [JsonPropertyName("timeOffset")]
        public double TimeOffset { get; set; }

        /// <summary>
        /// User ID or nickname
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        /// <summary>
        /// Color in hex string, e.g. "#FFFFFF"
        /// </summary>
        [JsonPropertyName("color")]
        public string Color { get; set; }

        /// <summary>
        /// Position type: 0=scroll, 1=top, 2=bottom
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }
    }
}
