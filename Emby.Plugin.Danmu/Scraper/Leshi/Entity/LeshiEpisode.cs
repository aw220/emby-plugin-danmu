using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Leshi.Entity
{
    public class LeshiEpisode
    {
        [JsonPropertyName("vid")]
        public long Vid { get; set; }

        [JsonPropertyName("aid")]
        public long Aid { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("subTitle")]
        public string SubTitle { get; set; }

        /// <summary>
        /// Duration in seconds.
        /// </summary>
        public int Duration { get; set; }
    }
}
