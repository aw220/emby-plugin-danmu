using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Migu.Entity
{
    public class MiguContentResult
    {
        [JsonPropertyName("body")]
        public MiguContentBody Body { get; set; }
    }

    public class MiguContentBody
    {
        [JsonPropertyName("contId")]
        public string ContId { get; set; }

        [JsonPropertyName("contName")]
        public string ContName { get; set; }

        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channelName")]
        public string ChannelName { get; set; }

        [JsonPropertyName("episodeList")]
        public List<MiguEpisode> EpisodeList { get; set; }

        [JsonPropertyName("totalEpisode")]
        public int TotalEpisode { get; set; }
    }

    public class MiguEpisode
    {
        [JsonPropertyName("contId")]
        public string ContId { get; set; }

        [JsonPropertyName("contName")]
        public string ContName { get; set; }

        [JsonPropertyName("episodeIndex")]
        public int EpisodeIndex { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }
}
