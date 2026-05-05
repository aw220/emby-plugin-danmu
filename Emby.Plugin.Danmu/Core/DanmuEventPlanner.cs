using System.Collections.Generic;
using Emby.Plugin.Danmu.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Core
{
    public static class DanmuEventPlanner
    {
        public static IReadOnlyList<EventType> GetItemAddedEvents(BaseItem item)
        {
            if (item is Movie)
            {
                return new[] { EventType.Add };
            }

            if (item is Season)
            {
                return new[] { EventType.Add, EventType.Update };
            }

            if (item is Episode || item is Series)
            {
                return new[] { EventType.Update };
            }

            return new EventType[0];
        }
    }
}
