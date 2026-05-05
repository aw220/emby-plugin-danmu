using System;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Animeko.Entity
{
    public class BangumiSubject
    {
        [DataMember(Name = "id")]
        public long Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "name_cn")]
        public string NameCn { get; set; }

        [DataMember(Name = "type")]
        public int Type { get; set; }

        [DataMember(Name = "date")]
        public string Date { get; set; }

        [DataMember(Name = "eps")]
        public int? Eps { get; set; }

        public string DisplayName => !string.IsNullOrEmpty(NameCn) ? NameCn : Name;

        public int? Year
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(Date))
                    {
                        return null;
                    }

                    return DateTime.Parse(Date).Year;
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
