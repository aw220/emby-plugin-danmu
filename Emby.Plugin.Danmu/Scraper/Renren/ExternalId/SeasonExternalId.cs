using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scraper.Renren.ExternalId
{
    public class SeasonExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Renren.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Renren.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "#";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Season || item is Series;
        }
    }
}
