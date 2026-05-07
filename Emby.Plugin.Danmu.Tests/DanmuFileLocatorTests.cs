using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emby.Plugin.Danmu.Core;
using Xunit;

namespace Emby.Plugin.Danmu.Tests;

public class DanmuFileLocatorTests
{
    [Fact]
    public void BuildDownloadScopeKey_UsesItemAndProviderOnly()
    {
        var key1 = DanmuFileLocator.BuildDownloadScopeKey("123", "BilibiliID");
        var key2 = DanmuFileLocator.BuildDownloadScopeKey("123", "BilibiliID");
        var key3 = DanmuFileLocator.BuildDownloadScopeKey("123", "YoukuID");

        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
    }

    [Fact]
    public void BuildDownloadScopeKey_IsStableAcrossDifferentCommentIdsForSameItemAndProvider()
    {
        var commentIds = new[] { "ep_id:12345", "aid:1,cid:2", "tvId:999" };

        var keys = commentIds
            .Select(_ => DanmuFileLocator.BuildDownloadScopeKey("same-item", "BilibiliID"))
            .Distinct()
            .ToList();

        Assert.Single(keys);
        Assert.Equal("same-item_BilibiliID", keys[0]);
    }

    [Fact]
    public void EnsureDanmuDirectory_CreatesDanmuDirectoryOnly()
    {
        using var dir = new TempDir();

        var danmuDirectory = DanmuFileLocator.EnsureDanmuDirectory(dir.Path);

        Assert.Equal(Path.Combine(dir.Path, DanmuFileLocator.DanmuDirectoryName), danmuDirectory);
        Assert.True(Directory.Exists(danmuDirectory));
        Assert.False(File.Exists(Path.Combine(danmuDirectory, ".ignore")));
        Assert.False(File.Exists(Path.Combine(danmuDirectory, ".embyignore")));
    }

    [Fact]
    public void FindBestExistingDanmuFile_PrefersEnabledProviderFileInDanmuDirectory()
    {
        using var dir = new TempDir();
        var danmuDirectory = DanmuFileLocator.EnsureDanmuDirectory(dir.Path);
        var fileNameWithoutExtension = "Episode01";
        var preferred = Path.Combine(danmuDirectory, fileNameWithoutExtension + "_YoukuID.xml");
        var other = Path.Combine(danmuDirectory, fileNameWithoutExtension + "_BilibiliID.xml");
        File.WriteAllText(other, "bili");
        File.WriteAllText(preferred, "youku");

        var result = DanmuFileLocator.FindBestExistingDanmuFile(
            dir.Path,
            fileNameWithoutExtension,
            new[] { "YoukuID" });

        Assert.Equal(preferred, result);
    }

    [Fact]
    public void FindBestExistingDanmuFile_FallsBackToDisabledProviderFile_WhenAllSourcesDisabled()
    {
        using var dir = new TempDir();
        var danmuDirectory = DanmuFileLocator.EnsureDanmuDirectory(dir.Path);
        var fileNameWithoutExtension = "Episode02";
        var existing = Path.Combine(danmuDirectory, fileNameWithoutExtension + "_BilibiliID.xml");
        File.WriteAllText(existing, "bili");

        var result = DanmuFileLocator.FindBestExistingDanmuFile(
            dir.Path,
            fileNameWithoutExtension,
            Array.Empty<string>());

        Assert.Equal(existing, result);
    }

    [Fact]
    public void FindBestExistingDanmuFile_FallsBackToLegacyRootFile()
    {
        using var dir = new TempDir();
        var fileNameWithoutExtension = "EpisodeLegacy";
        var existing = Path.Combine(dir.Path, fileNameWithoutExtension + "_BilibiliID.xml");
        File.WriteAllText(existing, "legacy");

        var result = DanmuFileLocator.FindBestExistingDanmuFile(
            dir.Path,
            fileNameWithoutExtension,
            Array.Empty<string>());

        Assert.Equal(existing, result);
    }

    [Fact]
    public void FindBestExistingDanmuFile_FallsBackToDefaultXml()
    {
        using var dir = new TempDir();
        var danmuDirectory = DanmuFileLocator.EnsureDanmuDirectory(dir.Path);
        var fileNameWithoutExtension = "Episode03";
        var existing = Path.Combine(danmuDirectory, fileNameWithoutExtension + ".xml");
        File.WriteAllText(existing, "default");

        var result = DanmuFileLocator.FindBestExistingDanmuFile(
            dir.Path,
            fileNameWithoutExtension,
            Array.Empty<string>());

        Assert.Equal(existing, result);
    }

    [Fact]
    public void GetDanmuXmlFilePath_UsesLegacyDirectoryWhenEnabled()
    {
        using var dir = new TempDir();

        var result = DanmuFileLocator.GetDanmuXmlFilePath(dir.Path, "Episode04", "YoukuID", useLegacyDirectory: true);

        Assert.Equal(Path.Combine(dir.Path, "Episode04_YoukuID.xml"), result);
    }

    [Fact]
    public void GetAssFilePath_DefaultsToDanmuSubDirectory()
    {
        using var dir = new TempDir();

        var result = DanmuFileLocator.GetAssFilePath(dir.Path, "Episode05", "YoukuID");

        Assert.Equal(Path.Combine(dir.Path, DanmuFileLocator.DanmuDirectoryName, "Episode05.chs[YoukuID_danmu].ass"), result);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "danmu-tests-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
