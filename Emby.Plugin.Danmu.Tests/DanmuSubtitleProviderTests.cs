using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Emby.Plugin.Danmu.Tests;

public class DanmuSubtitleProviderTests
{
    [Fact]
    public async Task GetSubtitles_WhenRequestAlreadyPending_ReturnsDeferredSubtitleResponse()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("pending-request", true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

        var provider = CreateProvider(cache);

        var response = await provider.GetSubtitles("pending-request", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("ass", response.Format);
        Assert.Equal("zh-CN", response.Language);
        Assert.NotNull(response.Stream);

        response.Stream.Position = 0;
        using var reader = new StreamReader(response.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("[Script Info]", content);
        Assert.Contains("弹幕任务已在后台处理中", content);
    }

    [Fact]
    public async Task CreateDeferredSubtitleResponse_ShouldEmbedReadableAssMessage()
    {
        var response = DanmuSubtitleProvider.CreateDeferredSubtitleResponse("后台处理中\n稍后重试");

        Assert.NotNull(response);
        Assert.Equal("ass", response.Format);
        Assert.NotNull(response.Stream);

        response.Stream.Position = 0;
        using var reader = new StreamReader(response.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("Dialogue:", content);
        Assert.Contains("后台处理中\\N稍后重试", content);
    }

    private static DanmuSubtitleProvider CreateProvider(IMemoryCache memoryCache)
    {
#pragma warning disable SYSLIB0050
        var provider = (DanmuSubtitleProvider)FormatterServices.GetUninitializedObject(typeof(DanmuSubtitleProvider));
#pragma warning restore SYSLIB0050

        SetField(provider, "_logger", new TestLogger());
        SetField(provider, "_memoryCache", memoryCache);
        SetField(provider, "_pendingDanmuDownloadExpiredOption", new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return provider;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private sealed class TestLogger : ILogger
    {
        public void Info(string message, params object[] paramList) { }
        public void Info(ReadOnlyMemory<char> message) { }
        public void Error(string message, params object[] paramList) { }
        public void Error(ReadOnlyMemory<char> message) { }
        public void Warn(string message, params object[] paramList) { }
        public void Warn(ReadOnlyMemory<char> message) { }
        public void Debug(string message, params object[] paramList) { }
        public void Debug(ReadOnlyMemory<char> message) { }
        public void Fatal(string message, params object[] paramList) { }
        public void FatalException(string message, Exception exception, params object[] paramList) { }
        public void ErrorException(string message, Exception exception, params object[] paramList) { }
        public void LogMultiline(string message, LogSeverity severity, StringBuilder additionalContent) { }
        public void Log(LogSeverity severity, string message, params object[] paramList) { }
        public void Log(LogSeverity severity, ReadOnlyMemory<char> message) { }
    }
}
