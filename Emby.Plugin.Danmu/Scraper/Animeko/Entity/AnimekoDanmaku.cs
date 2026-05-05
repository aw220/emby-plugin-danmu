using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Animeko.Entity
{
    public class AnimekoDanmakuResult
    {
        [DataMember(Name = "danmakuList")]
        public List<AnimekoDanmakuItem> DanmakuList { get; set; }
    }

    public class AnimekoDanmakuItem
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "playTimeMs")]
        public long PlayTimeMs { get; set; }

        [DataMember(Name = "senderId")]
        public string SenderId { get; set; }

        [DataMember(Name = "location")]
        public string Location { get; set; }

        [DataMember(Name = "text")]
        public string Text { get; set; }

        [DataMember(Name = "color")]
        public int Color { get; set; }
    }
}
