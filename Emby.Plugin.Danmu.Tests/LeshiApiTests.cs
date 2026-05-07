using System.Collections.Generic;
using Emby.Plugin.Danmu.Scrapers.Leshi;
using Emby.Plugin.Danmu.Scrapers.Leshi.Entity;
using Xunit;

namespace Emby.Plugin.Danmu.Tests;

public class LeshiApiTests
{
    [Fact]
    public void ParseDanmuItems_ShouldSupportLegacyArrayShape()
    {
        var json = """
        {
          "data": [
            {
              "id": 1,
              "currentPoint": 12.5,
              "content": "legacy",
              "fontColor": "ffffff",
              "fontSize": 30,
              "position": 2,
              "uid": "u1",
              "createTime": 123456
            }
          ]
        }
        """;

        var result = LeshiApi.ParseDanmuItems(json);

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(12.5, result[0].CurrentPoint);
        Assert.Equal("legacy", result[0].Content);
        Assert.Equal("ffffff", result[0].FontColor);
        Assert.Equal(30, result[0].FontSize);
        Assert.Equal(2, result[0].Position);
        Assert.Equal("u1", result[0].Uid);
        Assert.Equal(123456, result[0].CreateTime);
    }

    [Fact]
    public void ParseDanmuItems_ShouldSupportNestedListShapeFromCurrentLeshiApi()
    {
        var json = """
        {
          "code": 200,
          "data": {
            "list": [
              {
                "uid": 342344264,
                "start": 1075.6569999999999,
                "txt": "幸亏走错了",
                "color": "FFFFFF",
                "font": "m",
                "position": 2,
                "addtime": 1777792783,
                "_id": "7777927835276101048"
              }
            ]
          }
        }
        """;

        var result = LeshiApi.ParseDanmuItems(json);

        Assert.Single(result);
        Assert.Equal(7777927835276101048L, result[0].Id);
        Assert.Equal(1075.6569999999999, result[0].CurrentPoint);
        Assert.Equal("幸亏走错了", result[0].Content);
        Assert.Equal("FFFFFF", result[0].FontColor);
        Assert.Equal(25, result[0].FontSize);
        Assert.Equal(2, result[0].Position);
        Assert.Equal("342344264", result[0].Uid);
        Assert.Equal(1777792783L, result[0].CreateTime);
    }

    [Fact]
    public void ParseEpisodes_ShouldExtractProtocolRelativeLinksFromTvDetailPage()
    {
        var html = """
        <div class="show_play first_videolist" id="first_videolist">
          <div class="column_body">
            <div class="j_all_tuwen">
              <div class="show_cnt twxj-1-20" data-state-area="ch=detail&pg=tvdetails&bk=first_episode">
                <div class="col_4" index="0">
                  <dl class="dl_temp">
                    <dd class="d_img">
                      <a href="//www.le.com/ptv/vplay/774649.html" target="_blank" title="征服01">
                        <img alt="征服01">
                      </a>
                    </dd>
                    <dt class="d_tit"><a title="征服01" href="//www.le.com/ptv/vplay/774649.html" target="_blank">第1集</a></dt>
                    <dd class="d_cnt">征服01</dd>
                  </dl>
                </div>
                <div class="col_4" index="1">
                  <dl class="dl_temp">
                    <dd class="d_img">
                      <a href="//www.le.com/ptv/vplay/774645.html" target="_blank" title="征服02">
                        <img alt="征服02">
                      </a>
                    </dd>
                    <dt class="d_tit"><a title="征服02" href="//www.le.com/ptv/vplay/774645.html" target="_blank">第2集</a></dt>
                    <dd class="d_cnt">征服02</dd>
                  </dl>
                </div>
              </div>
            </div>
          </div>
        </div>
        """;

        var result = LeshiApi.ParseEpisodes(html);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            episode =>
            {
                Assert.Equal(774649L, episode.Vid);
                Assert.Equal("第1集", episode.Title);
            },
            episode =>
            {
                Assert.Equal(774645L, episode.Vid);
                Assert.Equal("第2集", episode.Title);
            });
    }

    [Fact]
    public void ParseEpisodes_ShouldFallbackToWholeTvPageWhenScopedSectionMissesEpisodes()
    {
        var html = """
        <div class="show_play first_videolist" id="first_videolist">
          <div class="column_body">
            <div class="j_all_tuwen">
              <div class="show_cnt twxj-1-20">
                <div class="tips">暂无图文选集</div>
              </div>
            </div>
          </div>
        </div>
        <div class="episode_wrap">
          <div class="show_cnt sjxj-1-50" data-tab="episode">
            <div class="col_4" index="0">
              <dl class="dl_temp">
                <dd class="d_img">
                  <a href="//www.le.com/ptv/vplay/77917395.html" target="_blank" title="风云雄霸天下01">
                    <img alt="风云雄霸天下01">
                  </a>
                </dd>
                <dt class="d_tit"><a href="//www.le.com/ptv/vplay/77917395.html" title="风云雄霸天下01" target="_blank">第1集</a></dt>
                <dd class="d_cnt">风云雄霸天下01</dd>
              </dl>
            </div>
            <div class="col_4" index="1">
              <dl class="dl_temp">
                <dd class="d_img">
                  <a href="//www.le.com/ptv/vplay/77917402.html" target="_blank" title="风云雄霸天下02">
                    <img alt="风云雄霸天下02">
                  </a>
                </dd>
                <dt class="d_tit"><a href="//www.le.com/ptv/vplay/77917402.html" title="风云雄霸天下02" target="_blank">第2集</a></dt>
                <dd class="d_cnt">风云雄霸天下02</dd>
              </dl>
            </div>
          </div>
        </div>
        """;

        var result = LeshiApi.ParseEpisodes(html);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            episode =>
            {
                Assert.Equal(77917395L, episode.Vid);
                Assert.Equal("第1集", episode.Title);
            },
            episode =>
            {
                Assert.Equal(77917402L, episode.Vid);
                Assert.Equal("第2集", episode.Title);
            });
    }

    [Fact]
    public void ParseEpisodes_ShouldKeepWorkingForMinifiedTvDetailHtml()
    {
        var html = """
        <div class="show_play first_videolist"id="first_videolist"><div class="column_body"><div class="tuwen_drama j_tuwen_drama"><div class="tuwen_box"><div class="tuwen_box_tit years tv_tvnum"><div class="j_all_tuwen"><div class="show_cnt twxj-1-20"data-state-area="ch=detail&pg=tvdetails&bk=first_episode"><div class="col_4"index="0"><dl class="dl_temp"><dd class="d_img"><a href="//www.le.com/ptv/vplay/774649.html"target="_blank"title="征服01"><img alt="征服01"></a></dd><dt class="d_tit"><a title="征服01"href="//www.le.com/ptv/vplay/774649.html"target="_blank">第1集</a></dt><dd class="d_cnt">征服01</dd></dl></div><div class="col_4"index="1"><dl class="dl_temp"><dd class="d_img"><a href="//www.le.com/ptv/vplay/774645.html"target="_blank"title="征服02"><img alt="征服02"></a></dd><dt class="d_tit"><a title="征服02"href="//www.le.com/ptv/vplay/774645.html"target="_blank">第2集</a></dt><dd class="d_cnt">征服02</dd></dl></div><div class="col_4"index="2"><dl class="dl_temp"><dd class="d_img"><a href="//www.le.com/ptv/vplay/774646.html"target="_blank"title="征服03"><img alt="征服03"></a></dd><dt class="d_tit"><a title="征服03"href="//www.le.com/ptv/vplay/774646.html"target="_blank">第3集</a></dt><dd class="d_cnt">征服03</dd></dl></div><div class="col_4"index="3"><dl class="dl_temp"><dd class="d_img"><a href="//www.le.com/ptv/vplay/774647.html"target="_blank"title="征服04"><img alt="征服04"></a></dd><dt class="d_tit"><a title="征服04"href="//www.le.com/ptv/vplay/774647.html"target="_blank">第4集</a></dt><dd class="d_cnt">征服04</dd></dl></div><div class="col_4"index="4"><dl class="dl_temp"><dd class="d_img"><a href="//www.le.com/ptv/vplay/774648.html"target="_blank"title="征服05"><img alt="征服05"></a></dd><dt class="d_tit"><a title="征服05"href="//www.le.com/ptv/vplay/774648.html"target="_blank">第5集</a></dt><dd class="d_cnt">征服05</dd></dl></div></div></div></div></div></div>
        """;

        var result = LeshiApi.ParseEpisodes(html);

        Assert.Equal(5, result.Count);
        Assert.Equal(774648L, result[4].Vid);
        Assert.Equal("第5集", result[4].Title);
    }

    [Fact]
    public void ParseEpisodes_ShouldFallbackToBootstrapCurrentVideo_WhenEpisodeListIsMissing()
    {
        var html = """
        <script>
        var __INFO__={pageInfo:{type:"tv_details"},video:{pid:"25233",moreVid:"",vid:"774649",url:"https://www.le.com/ptv/vplay/774649.html"},title:"征服"}
        </script>
        """;

        var result = LeshiApi.ParseEpisodes(html);

        var episode = Assert.Single(result);
        Assert.Equal(774649L, episode.Vid);
        Assert.Equal("第1集", episode.Title);
    }
}
