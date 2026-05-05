using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scraper.Maiduidui.ExternalId
{
    public class SeasonExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Maiduidui.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Maiduidui.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "#";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Season || item is Series;
    }
}
