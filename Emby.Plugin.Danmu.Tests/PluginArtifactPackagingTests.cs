using System;
using System.Linq;
using Emby.Plugin.Danmu;
using Xunit;

namespace Emby.Plugin.Danmu.Tests;

public class PluginArtifactPackagingTests
{
    [Fact]
    public void PluginAssembly_EmbedsGoogleProtobufForStandaloneDeployment()
    {
        var resourceNames = typeof(Plugin).Assembly.GetManifestResourceNames();

        Assert.Contains(
            resourceNames,
            name => name.IndexOf("costura", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.IndexOf("google.protobuf", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
