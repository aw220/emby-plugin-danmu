using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Bahamut.Entity
{
    public class BahamutDanmuResult
    {
        public BahamutDanmuData Data { get; set; }
    }

    public class BahamutDanmuData
    {
        public List<BahamutDanmu> Danmu { get; set; }
    }

    public class BahamutDanmu
    {
        public long Sn { get; set; }
        public int Time { get; set; }
        public int Position { get; set; }
        public string Color { get; set; }
        public string Text { get; set; }
    }
}
