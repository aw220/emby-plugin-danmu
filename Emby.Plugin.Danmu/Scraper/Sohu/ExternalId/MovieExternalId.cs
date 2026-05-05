using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scrapers.Sohu.ExternalId
{
    /// <inheritdoc />
    public class MovieExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Sohu.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Sohu.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "#";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Movie;
    }
}
