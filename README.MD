# Emby.Plugin.Danmu

这是一个基于 Emby 的弹幕插件二开版本，当前仓库用于持续维护和修复。

## 2026-05-07更新
1. 修复最新运行日志暴露的弹幕源异常处理问题：腾讯 / 西瓜源在搜索失败时降级为空结果，不再把单源故障升级成队列异常。
2. 修复乐视剧集解析与字幕返回契约相关问题。
3. 取消在剧集 `danmu/` 目录中自动创建 `.ignore` 和 `.embyignore` 的行为。
4. 构建后会自动同步最新插件 DLL 到 `doc/Emby.Plugin.Danmu.dll`，方便直接分发和验证。

## 2025-04-23更新
1. 修复爱奇艺弹幕下载问题
2. 支持更新和扫描任务

## 2024-07-26更新
1. 支持弹幕搜索
2. 支持新增媒体自动下载

## 支持弹幕网站
目前只支持以下站点，后续有空再更新
```
弹弹play、爱奇艺、腾讯、优酷
```
设置方式一样和jellyfin一样 [参考](https://github.com/cxfksword/jellyfin-plugin-danmu) ，但是api接口基本没有支持



## 支持的api操作
```shell
## 刷新某个媒体的弹幕
/api/danmu/{媒体id}?option=Refresh&X-Emby-Token=xxx
## 获取媒体对应json格式弹幕
/api/danmu/{媒体id}?option=GetJsonById&X-Emby-Token=xxx

```

## 下载链接
[Emby.Plugin.Danmu.dll](https://github.com/aw220/emby-plugin-danmu/releases/tag/1.0.0) <p/>
[Emby.AndroidTV.2.1.14.弹幕.apk](https://github.com/aw220/emby-plugin-danmu/releases/tag/1.0.0)

## 遗留问题
1. 配置暂不支持顺序调整

### 预览

<img src="./doc/image/设置页面.jpg"  width="720px" />
<img src="./doc/image/tv-danmu.jpg"  width="720px" />

<img src="./doc/image/弹幕搜索.png"  width="720px" />
