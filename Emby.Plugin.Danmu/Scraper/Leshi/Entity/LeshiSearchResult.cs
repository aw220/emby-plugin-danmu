using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Emby.Plugin.Danmu.Scrapers.Leshi.Entity
{
    /// <summary>
    /// Represents a search result item parsed from Leshi HTML search page.
    /// The data-info attribute contains JS object literal with fields like:
    /// {pid:'73868',type:'tv',keyWord:'甄嬛传2012电视剧',total:'76',vidEpisode:'1-1578861,...'}
    /// </summary>
    public class LeshiSearchItem
    {
        private static readonly Regex _yearRegex = new Regex(@"(\d{4})", RegexOptions.Compiled);
        private static readonly Regex _nameRegex = new Regex(@"^(.+?)(\d{4})", RegexOptions.Compiled);
        private static readonly Regex _htmlTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        [JsonPropertyName("pid")]
        public string Pid { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("keyWord")]
        public string KeyWord { get; set; }

        [JsonPropertyName("total")]
        public string Total { get; set; }

        [JsonPropertyName("vidEpisode")]
        public string VidEpisode { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        /// <summary>
        /// 从keyWord或name中提取标题（去掉年份和类型后缀）
        /// 例如 "甄嬛传2012电视剧" -> "甄嬛传"
        /// </summary>
        [JsonIgnore]
        public string Title
        {
            get
            {
                // 优先用name字段（subject类型的结果有name）
                if (!string.IsNullOrEmpty(Name))
                {
                    return _htmlTagRegex.Replace(Name, "");
                }
                if (string.IsNullOrEmpty(KeyWord))
                {
                    return string.Empty;
                }
                var match = _nameRegex.Match(KeyWord);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
                return KeyWord;
            }
        }

        [JsonIgnore]
        public string ContentType => Type ?? Category ?? string.Empty;

        [JsonIgnore]
        public string CategoryName
        {
            get
            {
                switch (ContentType?.ToLower())
                {
                    case "tv":
                        return "电视剧";
                    case "movie":
                        return "电影";
                    case "cartoon":
                    case "comic":
                        return "动漫";
                    case "zongyi":
                        return "综艺";
                    default:
                        return ContentType ?? string.Empty;
                }
            }
        }

        [JsonIgnore]
        public int EpisodeCount
        {
            get
            {
                if (int.TryParse(Total, out var count))
                {
                    return count;
                }
                return 0;
            }
        }

        [JsonIgnore]
        public int? YearInt
        {
            get
            {
                if (string.IsNullOrEmpty(KeyWord))
                {
                    return null;
                }
                var match = _yearRegex.Match(KeyWord);
                if (match.Success && int.TryParse(match.Value, out var y) && y > 1900 && y < 2100)
                {
                    return y;
                }
                return null;
            }
        }
    }
}
