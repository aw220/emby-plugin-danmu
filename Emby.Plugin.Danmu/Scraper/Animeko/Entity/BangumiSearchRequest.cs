using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Animeko.Entity
{
    public class BangumiSearchRequest
    {
        [DataMember(Name = "keyword")]
        public string Keyword { get; set; }

        [DataMember(Name = "filter")]
        public BangumiSearchFilter Filter { get; set; }
    }

    public class BangumiSearchFilter
    {
        [DataMember(Name = "type")]
        public List<int> Type { get; set; }
    }
}
