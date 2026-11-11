# 墨龙 Codex 视频启动器 v1.3.0

[English summary](#english-summary) · [下载最新版](https://github.com/ratgathirufhag66-gif/InkDragon-Codex-Launcher/releases/latest)

Windows 10/11 x64 非官方 Codex 启动器。它会全屏播放 `media/startup.mp4`，同时在后台启动 Codex；视频结束时淡出并将焦点交给客户端。

启动器会从当前用户的 Windows 包注册表动态查找最新版 `ChatGPT.exe`，直接执行真实客户端；随后再通过 Windows 应用激活接口请求显示窗口。它不依赖可能随更新失效的开始菜单快捷方式或写死的版本目录。

每次播放都会向 Windows 发送激活请求：Codex 已关闭时会启动；已打开时只会激活现有窗口，不会创建重复实例。

视频结束后，启动器会等待真实的 ChatGPT/Codex 主窗口出现，再隐藏动画并完成交接；如果客户端冷启动较慢，会显示“正在启动 Codex…”，最长等待时间由 `HandoffWaitSeconds` 控制。

为了避免露出 Codex 自己的加载画面，主窗口还必须连续稳定且可响应达到 `ReadyStabilitySeconds`（默认 6 秒）。按 `Esc`、空格或回车跳过时则不等待稳定期，会立即隐藏动画并把控制权交给 Codex。

## 使用

1. 完整解压 ZIP，保留 `media` 文件夹。
2. 双击 `InkDragonCodex.exe`。
3. 动画期间按 `Esc`、空格或回车可跳过。
4. 只预览视频、不启动 Codex：双击 `Preview.cmd`。
5. 创建桌面入口：右键 `Install-Shortcut.ps1`，选择“使用 PowerShell 运行”。

## 画面和声音

- 默认保持视频完整比例，屏幕比例不同时可能出现黑边。
- 如需铺满屏幕，将 `launcher.ini` 中的 `FillScreen=false` 改为 `true`，画面边缘可能被裁切。
- `Volume` 范围为 0–1；设为 0 可静音。
- `FadeSeconds` 控制结尾淡出的时长。

## Codex 启动标识

如果 Codex 无法启动，在 PowerShell 执行：

```powershell
Get-StartApps | Where-Object { $_.Name -match 'Codex|ChatGPT' }
```

将返回的 `AppID` 写入 `launcher.ini` 的 `AppLaunch=` 后面。

日志位于 `%LOCALAPPDATA%\InkDragonCodex\launcher.log`。本程序不会修改 Codex 安装文件。

社区灵感来源及独立实现声明见 `ACKNOWLEDGEMENTS.md`。

## 从源码构建

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

构建使用 Windows 自带的 .NET Framework 4.x/WPF 编译器。

## 更换启动视频

用新的 MP4 替换 `media/startup.mp4` 即可，无需重新编译。推荐使用 H.264 视频和 AAC 音频。如果使用其他文件名，请同步修改 `launcher.ini` 中的 `Video=`。

## 许可

启动器代码与文档采用 MIT 许可证。`media/startup.mp4` 不包含在 MIT 许可证中，详见 `THIRD_PARTY_NOTICES.md`。社区灵感来源见 `ACKNOWLEDGEMENTS.md`。

## English summary

InkDragon Codex Launcher is an unofficial Windows 10/11 x64 launcher that plays a replaceable local MP4 while starting the installed ChatGPT/Codex desktop client in the background.

It resolves the current packaged `ChatGPT.exe`, requests Windows application activation, and hands off only after the real ChatGPT window has remained responsive and stable. Press Esc, Space, or Enter to skip immediately.

Download the ready-to-run ZIP from [GitHub Releases](https://github.com/ratgathirufhag66-gif/InkDragon-Codex-Launcher/releases/latest). Replace `media/startup.mp4` to use a different animation without rebuilding.

This is an independent community project and is not affiliated with or endorsed by OpenAI or Microsoft.
