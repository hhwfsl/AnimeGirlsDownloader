# AnimeGirlsDownloader

AnimeGirlsDownloader 是一个基于 WinUI 3 和 .NET 8 的 Windows 图片浏览、下载与上传客户端。

## 功能

- 通过服务端预览地址浏览图片，仅在用户选择下载时获取原图。
- 支持随机浏览、图片 ID 搜索及标签搜索。
- 支持 SFW、NSFW 和 AI 生成内容筛选。
- 串行下载队列、实时进度、取消及失败重试。
- 支持复制预览图片和图片链接。
- 支持登录、注册、令牌自动登录，以及账号名称和头像的跨设备同步。
- 支持在上传头像前进行圆形裁剪，可拖动定位并使用滚轮、触控板或触摸手势缩放。
- 以用户 ID 隔离账户设置、令牌和头像数据。
- 支持图片与文件夹混合批量上传，以及单图标签编辑。
- 支持简体中文、英语和日语。
- 支持浅色与深色主题。
- 启动时后台检查 GitHub Releases，并支持在设置页手动检查更新和应用内自动安装。

## 系统要求

- Windows 10 1809（build 17763）或更高版本。
- x64 处理器。
- 应用目录需要具有写入权限。

发布包采用 Native AOT 自包含部署，不需要单独安装 .NET Runtime。

## 下载与运行

1. 从 [Releases](https://github.com/hhwfsl/AnimeGirlsDownloader/releases/latest) 下载最新的 `win-x64.zip` 文件。
2. 将压缩包完整解压到普通用户可写的目录。
3. 运行 `Anime Girls Downloader.exe`。

不要直接在压缩包内运行程序，也不要只复制 EXE；发布目录中的原生依赖和资源文件必须与程序一同保留。

## 使用说明

- 主界面可随机获取图片，也可输入图片 ID 或以空格分隔的标签进行搜索。
- 点击下载按钮会将原图加入下载队列，不会中断当前浏览。
- 下载页面可查看队列进度、取消任务或重试失败任务。
- 登录后可上传图片、修改并同步账号资料，以及使用用户专属设置；未登录时无法进入上传页面。
- 上传页面支持同时选择多个图片和文件夹；只有单独选择一张图片时才能编辑标签。
- 设置页面可切换主题、语言、默认下载目录并检查版本更新。语言设置在重新启动应用后生效。
- 确认更新后，ZIP 更新包会进入普通下载队列；下载完成后应用会校验并解压更新包，覆盖程序文件并自动重启。更新过程不会覆盖用户数据和日志。

## 本地数据

应用采用便携式数据目录，运行数据保存在程序所在目录：

```text
AnimeGirlsDownloader/
├─ Assets/                 应用资源
├─ logs/                   公共日志
├─ .update/                自动更新临时文件（更新完成后自动清理）
└─ users/
   ├─ active-user.txt      当前登录用户
   ├─ anonymous/           未登录用户设置
   └─ {用户ID}/
      ├─ auth.token        加密登录令牌
      ├─ config.json       用户设置
      └─ avatar.png        用户头像
```

请勿提交或公开 `users`、`logs` 目录中的内容。

## 从源代码构建

需要 .NET 8 SDK、Visual Studio 2022 C++ 桌面工具链和 Windows SDK。

```powershell
dotnet restore AnimeGirlsDownloader.csproj -r win-x64 -p:Platform=x64 -p:Configuration=Release
dotnet publish AnimeGirlsDownloader.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true --no-restore
```

## 版本

项目采用[语义化版本](https://semver.org/lang/zh-CN/)。版本变更记录见 [CHANGELOG.md](CHANGELOG.md)。

## 许可证

本项目基于 [MIT License](LICENSE.txt) 发布。
