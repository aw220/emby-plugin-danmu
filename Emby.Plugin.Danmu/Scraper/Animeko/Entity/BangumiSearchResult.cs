using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Animeko.Entity
{
    public class BangumiSearchResult
    {
        [DataMember(Name = "total")]
        public int Total { get; set; }

        [DataMember(Name = "limit")]
        public int Limit { get; set; }

        [DataMember(Name = "offset")]
        public int Offset { get; set; }

        [DataMember(Name = "data")]
        public List<BangumiSubject> Data { get; set; }
    }
}
