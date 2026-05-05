using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scraper.Bahamut.ExternalId
{
    public class SeasonExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => Bahamut.ScraperProviderName;

        /// <inheritdoc />
        public string Key => Bahamut.ScraperProviderId;

        /// <inheritdoc />
        public string UrlFormatString => "https://ani.gamer.com.tw/animeVideo.php?sn={0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Season || item is Series;
        }
    }
}
