# Emby Danmu DLL 独立验证

结论：
- 这个 DLL 不能像普通控制台程序一样“独立运行”。它本质上是 Emby 插件，完整下载链路依赖 Emby 宿主提供的 `IApplicationHost`、`ILibraryManager`、`ISubtitleProvider` 调用入口和插件加载生命周期。
- 但可以把“重复装进正式 Emby 再手点验证”降到最低：先在仓库内做单命令离线验证，只在需要最终确认宿主集成时再进入隔离 Emby。

已落地的一键验证命令：

```bash
cd /file/emby-plugin-danmu-enhanced
./scripts/validate-artifact.sh
```

脚本做的事情：
1. 用 `/opt/data/home/.dotnet/dotnet` 构建插件。
2. 运行现有回归测试（包含 `DanmuSubtitleProvider` 和 `LeshiApi` 关键路径）。
3. 用 `tools/ArtifactValidator` 直接反射加载构建产物 `Emby.Plugin.Danmu/bin/Debug/netstandard2.0/Emby.Plugin.Danmu.dll`。
4. 校验以下最关键的“脱离 Emby UI 的可执行契约”：
   - `Emby.Plugin.Danmu.Plugin` 类型存在
   - `Emby.Plugin.Danmu.DanmuSubtitleProvider` 类型存在
   - `Emby.Plugin.Danmu.Scrapers.Leshi.LeshiApi` 类型存在
   - `CreateDeferredSubtitleResponse()` 能返回非空 ASS 占位字幕流
   - `LeshiApi.ParseEpisodes()` 能解析 tv detail bootstrap fallback
   - `LeshiApi.ParseDanmuItems()` 能解析当前乐视 nested list 结构

当前推荐的待安装 DLL：
- `/file/emby-plugin-danmu-enhanced/Emby.Plugin.Danmu/bin/Debug/netstandard2.0/Emby.Plugin.Danmu.dll`

注意：
- `doc/Emby.Plugin.Danmu.dll` 目前不是最新构建产物，不能当作本次验证通过的 DLL 使用。
- 如果后续还要做真正宿主级 RemoteSearch/Subtitles 冒烟，建议额外准备一个隔离 Emby 数据目录，而不是继续污染正式 Emby。
