using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scrapers.Xigua.Entity
{
    public class XiguaDanmuResult
    {
        [JsonPropertyName("data")]
        public XiguaDanmuData Data { get; set; }

        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }
    }

    public class XiguaDanmuData
    {
        [JsonPropertyName("danmaku_list")]
        public List<XiguaComment> DanmakuList { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }
}
