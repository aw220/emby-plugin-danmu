using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Renren.Entity
{
    public class RenrenDanmuResult
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public RenrenDanmuData Data { get; set; }
    }

    public class RenrenDanmuData
    {
        public List<RenrenDanmuItem> Danmaku { get; set; }
    }

    public class RenrenDanmuItem
    {
        /// <summary>
        /// 弹幕ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 弹幕内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 弹幕时间点（毫秒）
        /// </summary>
        public long Time { get; set; }

        /// <summary>
        /// 弹幕类型: 0=滚动, 1=顶部, 2=底部
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// 弹幕颜色
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; }
    }
}
