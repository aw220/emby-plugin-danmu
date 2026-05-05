using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Animeko.Entity
{
    public class BangumiEpisode
    {
        [DataMember(Name = "id")]
        public long Id { get; set; }

        [DataMember(Name = "type")]
        public int Type { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "name_cn")]
        public string NameCn { get; set; }

        [DataMember(Name = "sort")]
        public double Sort { get; set; }

        [DataMember(Name = "ep")]
        public int? Ep { get; set; }

        public string DisplayName => !string.IsNullOrEmpty(NameCn) ? NameCn : Name;
    }
}
