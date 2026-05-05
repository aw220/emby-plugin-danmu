using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scraper.Animeko.ExternalId
{
    public class SeasonExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Animeko.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Animeko.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "https://bgm.tv/subject/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Season || item is Series;
        }
    }
}
