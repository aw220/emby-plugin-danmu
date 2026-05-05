using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using Xunit;

namespace Emby.Plugin.Danmu.Tests;

public class DanmuEventPlannerTests
{
    [Fact]
    public void Movie_ItemAdded_OnlyQueuesAdd()
    {
        var events = DanmuEventPlanner.GetItemAddedEvents(new Movie());

        Assert.Equal(new[] { EventType.Add }, events);
    }

    [Fact]
    public void Season_ItemAdded_QueuesAddThenUpdate()
    {
        var events = DanmuEventPlanner.GetItemAddedEvents(new Season());

        Assert.Equal(new[] { EventType.Add, EventType.Update }, events);
    }

    [Fact]
    public void Episode_ItemAdded_OnlyQueuesUpdate()
    {
        var events = DanmuEventPlanner.GetItemAddedEvents(new Episode());

        Assert.Equal(new[] { EventType.Update }, events);
    }

    [Fact]
    public void Series_ItemAdded_OnlyQueuesUpdate()
    {
        var events = DanmuEventPlanner.GetItemAddedEvents(new Series());

        Assert.Equal(new[] { EventType.Update }, events);
    }
}
