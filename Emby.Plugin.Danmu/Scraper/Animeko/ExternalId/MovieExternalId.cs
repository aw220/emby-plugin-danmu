using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scraper.Animeko.ExternalId
{
    public class MovieExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Animeko.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Animeko.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "https://bgm.tv/subject/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Movie || item is MediaBrowser.Controller.Entities.TV.Episode;
    }
}
