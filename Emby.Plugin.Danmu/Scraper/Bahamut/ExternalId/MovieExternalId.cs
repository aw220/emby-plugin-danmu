using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scraper.Bahamut.ExternalId
{
    public class MovieExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Bahamut.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Bahamut.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "#";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Movie || item is MediaBrowser.Controller.Entities.TV.Episode;
    }
}
