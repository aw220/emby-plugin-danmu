using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Emby.Plugin.Danmu.Scrapers.Xigua.ExternalId
{
    /// <inheritdoc />
    public class SeasonExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Xigua.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Xigua.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "https://www.ixigua.com/video/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Season;
    }
}
