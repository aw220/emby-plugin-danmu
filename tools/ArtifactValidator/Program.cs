using System.Reflection;
using System.Runtime.Loader;
using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: ArtifactValidator <plugin-dll-path> [fallback-dependency-dir ...]");
    return 2;
}

var pluginPath = Path.GetFullPath(args[0]);
if (!File.Exists(pluginPath))
{
    Console.Error.WriteLine($"Plugin DLL not found: {pluginPath}");
    return 2;
}

var fallbackDirs = args.Skip(1)
    .Select(Path.GetFullPath)
    .Where(Directory.Exists)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var pluginDir = Path.GetDirectoryName(pluginPath)!;
var resolver = new PluginLoadContext(pluginPath, fallbackDirs);

try
{
    var assembly = resolver.LoadFromAssemblyPath(pluginPath);
    Console.WriteLine($"Loaded: {assembly.FullName}");
    Console.WriteLine($"Path: {pluginPath}");
    Console.WriteLine($"PluginDir: {pluginDir}");
    if (fallbackDirs.Length > 0)
    {
        Console.WriteLine($"FallbackDirs: {string.Join(", ", fallbackDirs)}");
    }

    var pluginType = RequireType(assembly, "Emby.Plugin.Danmu.Plugin");
    var providerType = RequireType(assembly, "Emby.Plugin.Danmu.DanmuSubtitleProvider");
    var leshiApiType = RequireType(assembly, "Emby.Plugin.Danmu.Scrapers.Leshi.LeshiApi");

    var tfm = assembly.GetCustomAttributesData()
        .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? "<unknown>";
    Console.WriteLine($"TargetFramework: {tfm}");

    Console.WriteLine($"PluginType: {pluginType.FullName}");
    Console.WriteLine($"SubtitleProviderType: {providerType.FullName}");
    Console.WriteLine($"LeshiApiType: {leshiApiType.FullName}");

    ValidateDeferredSubtitleResponse(providerType);
    ValidateLeshiEpisodeParsing(leshiApiType);
    ValidateLeshiDanmuParsing(leshiApiType);

    Console.WriteLine("Artifact validation passed.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Artifact validation failed.");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

static Type RequireType(Assembly assembly, string typeName)
{
    return assembly.GetType(typeName, throwOnError: true)!;
}

static void ValidateDeferredSubtitleResponse(Type providerType)
{
    var method = providerType.GetMethod(
        "CreateDeferredSubtitleResponse",
        BindingFlags.Static | BindingFlags.NonPublic);
    if (method == null)
    {
        throw new MissingMethodException(providerType.FullName, "CreateDeferredSubtitleResponse");
    }

    const string message = "后台处理中\n稍后重试";
    var response = method.Invoke(null, new object?[] { message })
        ?? throw new InvalidOperationException("CreateDeferredSubtitleResponse returned null.");

    var responseType = response.GetType();
    var format = responseType.GetProperty("Format")?.GetValue(response)?.ToString();
    var language = responseType.GetProperty("Language")?.GetValue(response)?.ToString();
    var stream = responseType.GetProperty("Stream")?.GetValue(response) as Stream;

    if (format != "ass")
    {
        throw new InvalidOperationException($"Expected subtitle format ass, got {format ?? "<null>"}.");
    }

    if (language != "zh-CN")
    {
        throw new InvalidOperationException($"Expected subtitle language zh-CN, got {language ?? "<null>"}.");
    }

    if (stream == null)
    {
        throw new InvalidOperationException("Deferred subtitle response stream is null.");
    }

    stream.Position = 0;
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
    var content = reader.ReadToEnd();
    if (!content.Contains("[Script Info]", StringComparison.Ordinal) ||
        !content.Contains("后台处理中\\N稍后重试", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Deferred subtitle ASS content missing expected markers.");
    }

    Console.WriteLine("Validated: CreateDeferredSubtitleResponse -> non-empty ASS placeholder.");
}

static void ValidateLeshiEpisodeParsing(Type leshiApiType)
{
    var method = leshiApiType.GetMethod("ParseEpisodes", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(leshiApiType.FullName, "ParseEpisodes");

    const string html = """
<script>
var __INFO__={pageInfo:{type:"tv_details"},video:{pid:"25233",moreVid:"",vid:"774649",url:"https://www.le.com/ptv/vplay/774649.html"},title:"征服"}
</script>
""";

    var result = method.Invoke(null, new object?[] { html }) as System.Collections.IEnumerable
        ?? throw new InvalidOperationException("ParseEpisodes returned null.");

    var first = result.Cast<object>().FirstOrDefault()
        ?? throw new InvalidOperationException("ParseEpisodes returned an empty result.");

    var vid = Convert.ToInt64(first.GetType().GetProperty("Vid")?.GetValue(first));
    var title = first.GetType().GetProperty("Title")?.GetValue(first)?.ToString();

    if (vid != 774649L || title != "第1集")
    {
        throw new InvalidOperationException($"ParseEpisodes unexpected first item: vid={vid}, title={title}.");
    }

    Console.WriteLine("Validated: Leshi ParseEpisodes bootstrap fallback.");
}

static void ValidateLeshiDanmuParsing(Type leshiApiType)
{
    var method = leshiApiType.GetMethod("ParseDanmuItems", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(leshiApiType.FullName, "ParseDanmuItems");

    const string json = """
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

    var result = method.Invoke(null, new object?[] { json }) as System.Collections.IEnumerable
        ?? throw new InvalidOperationException("ParseDanmuItems returned null.");

    var first = result.Cast<object>().FirstOrDefault()
        ?? throw new InvalidOperationException("ParseDanmuItems returned an empty result.");

    var content = first.GetType().GetProperty("Content")?.GetValue(first)?.ToString();
    var fontSize = Convert.ToInt32(first.GetType().GetProperty("FontSize")?.GetValue(first));

    if (content != "幸亏走错了" || fontSize != 25)
    {
        throw new InvalidOperationException($"ParseDanmuItems unexpected first item: content={content}, fontSize={fontSize}.");
    }

    Console.WriteLine("Validated: Leshi ParseDanmuItems nested list shape.");
}

sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string[] _fallbackDirs;

    public PluginLoadContext(string pluginPath, string[] fallbackDirs)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _fallbackDirs = fallbackDirs;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        foreach (var dir in _fallbackDirs)
        {
            var candidate = Path.Combine(dir, $"{assemblyName.Name}.dll");
            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }
        }

        return null;
    }
}
