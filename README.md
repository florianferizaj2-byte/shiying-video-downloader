# 拾影 Shiying

面向 Windows 的轻量、本地优先视频保存工具：粘贴链接，解析标题与画质，选择文件夹，视频直接保存到自己的电脑。

拾影使用 C# / .NET Framework 4.8 承载桌面窗口，使用系统 WebView2 显示 React 界面，并调用 yt-dlp 完成视频解析与下载。程序不经过拾影服务器，不要求注册账号，也不把视频或下载记录上传到云端。

## 功能

- 支持直接粘贴视频链接，也支持粘贴包含链接的整段分享文案。
- 支持 TikTok、抖音、快手、YouTube，以及 yt-dlp 支持的其他站点。
- 解析封面、标题、作者、时长和可用画质后再开始下载。
- 显示下载进度，支持取消、断点续传、重复下载和最近记录。
- 支持系统代理和浏览器导出的 Netscape 格式 `cookies.txt`。
- YouTube 的轻量 JavaScript 支持和高清音画合并组件按需下载，并在使用前校验 SHA-256。
- 提供便携版和当前用户安装版，不捆绑 Electron、Node.js、Python 或浏览器内核。

## 下载使用

请从 [Releases](https://github.com/florianferizaj2-byte/shiying-video-downloader/releases) 下载最新版本：

- `Shiying-*-Portable.zip`：解压后直接运行 `Shiying.exe`。设置、历史记录和浏览器缓存保存在旁边的 `Data` 文件夹。
- `Shiying-*-Setup.exe`：当前用户安装版，支持桌面 / 开始菜单快捷方式和 Windows 卸载入口。

运行要求：

- Windows 10 / 11 x64
- .NET Framework 4.8
- Microsoft Edge WebView2 Runtime

如果系统缺少 WebView2，程序会提示打开微软官方下载页面。发行包目前未做商业代码签名，属于本地 MVP 版本。

### 基本流程

1. 复制一个视频链接，或复制完整的分享文案。
2. 在拾影中粘贴并点击“解析视频”。
3. 选择保存文件夹和画质。
4. 点击“下载到文件夹”。

默认优先使用包含声音的单文件视频。需要更高画质或音画合并时，可在“连接设置”中按需准备 FFmpeg 组件。

## 从源码构建

开发环境需要 Windows x64、Node.js 22 或更高版本，以及可访问依赖下载地址的网络环境。完整的原生构建不需要安装 Visual Studio，脚本使用系统自带的 .NET Framework C# 编译器。

```powershell
npm ci
npm run build:windows
.\build\app\Shiying.exe
```

首次构建会下载并校验固定版本的 WebView2 SDK、yt-dlp 和 QuickJS-ng。构建产物写入 `build/`，这些文件已被 Git 忽略。

常用命令：

```powershell
# 仅启动前端预览。浏览器预览不包含桌面下载后端。
npm run dev

# 运行自动化测试
npm test

# 运行包含公开示例文件下载的集成测试
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Integration

# 生成便携 ZIP 和安装程序
npm run package
```

打包脚本不会覆盖已有的输出目录。如果 `release/` 或 `build/installer-payload/` 中已经有旧产物，请先将其移走后再运行打包命令。发布文件不会进入 Git 历史，而是作为 GitHub Release 附件上传。

## 项目结构

```text
src/                    React 界面与 WebView2 消息桥
native/                 C# 桌面窗口、下载引擎、快手适配和安装器
tests/                  单元、进程、持久化、安装和安全边界测试
scripts/                构建、测试、依赖准备和打包脚本
resources/components.json  可选组件版本、来源和 SHA-256 摘要
resources/licenses/     随发行包分发的第三方许可证
docs/                   设计约定与验证记录
```

应用只允许受信任的本地界面调用系统功能，不开放网络监听端口。下载的文件、设置、历史记录和 cookies 路径均由用户自己的 Windows 环境管理；拾影不会把这些内容发送到自有服务器。使用 cookies 时请妥善保管导出的文件，不要将其提交到仓库或分享给他人。

## 已知限制

- 平台登录、验证码、地区限制、网络策略和网站改版都可能影响解析结果。
- 不支持 DRM、付费墙、正在进行的直播和播放列表下载。
- 快手使用公开分享页数据解析，页面结构变化可能导致适配失效。
- 抖音、快手及其他站点的可用性不保证对所有链接一致。
- 请仅下载你有权保存的内容，并遵守相关平台的服务条款和当地法律。

详细验证范围见 [`docs/VERIFICATION.md`](docs/VERIFICATION.md)，第三方组件说明见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。

## 许可证

项目源代码以 [MIT License](LICENSE) 发布。yt-dlp、WebView2、QuickJS-ng、FFmpeg 及其他依赖保留各自的许可证，详见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。
