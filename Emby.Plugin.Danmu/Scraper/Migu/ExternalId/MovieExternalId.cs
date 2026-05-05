using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scrapers.Migu.ExternalId
{
    /// <inheritdoc />
    public class MovieExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Migu.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Migu.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "https://www.miguvideo.com/p/detail/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Movie;
    }
}
