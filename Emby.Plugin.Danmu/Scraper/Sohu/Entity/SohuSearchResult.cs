using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Emby.Plugin.Danmu.Core.Extensions;


namespace Emby.Plugin.Danmu.Scrapers.Sohu.Entity
{
    public class SohuSearchResult
    {
        [JsonPropertyName("response")]
        public SohuSearchResponse Response { get; set; }
    }

    public class SohuSearchResponse
    {
        [JsonPropertyName("numFound")]
        public int NumFound { get; set; }

        [JsonPropertyName("docs")]
        public List<SohuSearchDoc> Docs { get; set; }
    }

    public class SohuSearchDoc
    {
        private static readonly Regex regHtml = new Regex(@"<.+?>", RegexOptions.Compiled);
        private static readonly Regex regYear = new Regex(@"[12][890][0-9][0-9]", RegexOptions.Compiled);

        [JsonPropertyName("albumId")]
        public long AlbumId { get; set; }

        [JsonPropertyName("vid")]
        public long Vid { get; set; }

        [JsonPropertyName("aid")]
        public long Aid { get; set; }

        private string _albumName = string.Empty;
        [JsonPropertyName("albumName")]
        public string AlbumName
        {
            get => regHtml.Replace(_albumName ?? string.Empty, "");
            set => _albumName = value;
        }

        [JsonPropertyName("tvName")]
        public string TvName { get; set; }

        [JsonPropertyName("categoryName")]
        public string CategoryName { get; set; }

        [JsonPropertyName("year")]
        public string YearStr { get; set; }

        [JsonPropertyName("playlistId")]
        public long PlaylistId { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        public string Id => AlbumId > 0 ? AlbumId.ToString() : PlaylistId.ToString();

        [JsonIgnore]
        public int? Year
        {
            get
            {
                if (string.IsNullOrEmpty(YearStr))
                {
                    return null;
                }
                var match = regYear.Match(YearStr);
                if (match.Success)
                {
                    return match.Value.ToInt();
                }
                return null;
            }
        }
    }
}
