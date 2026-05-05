using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Xigua.Entity
{
    public class XiguaComment
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("danmaku_id")]
        public long DanmakuId { get; set; }

        /// <summary>
        /// Position in milliseconds
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }

        /// <summary>
        /// Danmu mode: 1=scroll, 4=bottom, 5=top
        /// </summary>
        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("create_time")]
        public long CreateTime { get; set; }
    }
}
