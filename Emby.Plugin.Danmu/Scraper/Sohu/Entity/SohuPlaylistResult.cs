using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Sohu.Entity
{
    public class SohuPlaylistResult
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("statusText")]
        public string StatusText { get; set; }

        [JsonPropertyName("data")]
        public SohuPlaylistData Data { get; set; }
    }

    public class SohuPlaylistData
    {
        [JsonPropertyName("playlistId")]
        public long PlaylistId { get; set; }

        [JsonPropertyName("playlistName")]
        public string PlaylistName { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("videos")]
        public List<SohuVideoItem> Videos { get; set; }
    }

    public class SohuVideoItem
    {
        [JsonPropertyName("vid")]
        public long Vid { get; set; }

        [JsonPropertyName("aid")]
        public long Aid { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }
}
