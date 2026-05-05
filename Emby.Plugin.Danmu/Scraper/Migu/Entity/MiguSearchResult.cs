using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Emby.Plugin.Danmu.Core.Extensions;

namespace Emby.Plugin.Danmu.Scrapers.Migu.Entity
{
    public class MiguSearchResult
    {
        [JsonPropertyName("body")]
        public MiguSearchBody Body { get; set; }
    }

    public class MiguSearchBody
    {
        [JsonPropertyName("contList")]
        public List<MiguSearchItem> ContList { get; set; }
    }

    public class MiguSearchItem
    {
        private static readonly Regex regHtml = new Regex(@"<.+?>", RegexOptions.Compiled);
        private static readonly Regex regYear = new Regex(@"[12][890][0-9][0-9]", RegexOptions.Compiled);

        [JsonPropertyName("contId")]
        public string ContId { get; set; }

        private string _contName = string.Empty;
        [JsonPropertyName("contName")]
        public string ContName
        {
            get { return regHtml.Replace(_contName, ""); }
            set { _contName = value ?? string.Empty; }
        }

        [JsonPropertyName("contType")]
        public string ContType { get; set; }

        /// <summary>
        /// 1=电影, 2=电视剧, 3=综艺, etc.
        /// </summary>
        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channelName")]
        public string ChannelName { get; set; }

        [JsonPropertyName("publishDate")]
        public string PublishDate { get; set; }

        [JsonPropertyName("totalEpisode")]
        public int TotalEpisode { get; set; }

        public string TypeName
        {
            get
            {
                if (!string.IsNullOrEmpty(ChannelName))
                {
                    return ChannelName;
                }
                return ChannelId switch
                {
                    "1" => "电影",
                    "2" => "电视剧",
                    "3" => "综艺",
                    "4" => "动漫",
                    _ => "其他"
                };
            }
        }

        public int? Year
        {
            get
            {
                if (string.IsNullOrEmpty(PublishDate))
                {
                    return null;
                }
                var match = regYear.Match(PublishDate);
                if (match.Success)
                {
                    return match.Value.ToInt();
                }
                return null;
            }
        }
    }
}
