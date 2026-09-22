# AnimeGirlsDownloader

一个基于 WinUI 3 与 .NET 8 的 Windows 图片浏览、下载和上传客户端。项目采用无 MSIX 的自包含部署方式，并启用 Native AOT、裁剪分析和源生成 JSON 序列化。客户端与 [AnimeGirlsCollection](https://github.com/hhwfsl/AnimeGirlsCollection) API 配套使用。

## 功能

- 使用服务端预览 URI 浏览图片，浏览阶段不把原图下载到本地。
- 随机获取图片，支持 SFW、NSFW 与 AI 生成内容筛选。
- 按图片 ID 或空格分隔的标签搜索。
- 将原图加入串行下载队列，在独立下载页面查看进度、取消任务或重试失败任务。
- 复制当前预览图片或原图公开链接到剪贴板。
- 登录、注册、回车登录与令牌自动登录。
- 登录后从服务端同步用户 ID、用户名和头像；头像转换为 PNG 后保存到用户专属目录。
- 以服务端用户 ID 隔离每个账户的设置、头像和下载队列视图。
- 上传页面采用左右布局：左侧预览，右侧管理多个图片/文件夹路径并执行上传。
- 单张图片上传时支持作者、标签、NSFW 和 AI 标记；选择多个来源或文件夹时自动禁用标签编辑。
- 标签输入联想与重复标签检查。
- 启动后在后台静默检查 GitHub Releases；发现新版本时允许用户前往下载。
- 设置页显示当前版本，并支持手动检查更新。
- 支持简体中文、英语和日语界面资源。
- 支持浅色/深色主题、窗口尺寸和用户头像设置。

## 技术栈

- .NET 8 / C# 12
- WinUI 3 / Windows App SDK 1.8
- Native AOT 与 IL trimming
- `System.Text.Json` 源生成序列化
- SkiaSharp 图片解码和 PNG 编码
- `Microsoft.Extensions.DependencyInjection` 应用级依赖注入

## 环境要求

- Windows 10 1809（build 17763）或更高版本。
- .NET 8 SDK 或更高版本。
- 应用所在目录需要具备写入权限，用于保存 `logs` 与 `users`；不要将便携版放入普通用户不可写的系统目录。
- Visual Studio 2022 的“使用 C++ 的桌面开发”工作负载，Native AOT 在 Windows 上需要其链接工具链。
- 如需在 Visual Studio 中开发，建议同时安装“.NET 桌面开发”和 Windows App SDK 相关组件。

## 获取与运行

```powershell
git clone https://github.com/hhwfsl/AnimeGirlsDownloader.git
cd AnimeGirlsDownloader
dotnet restore AnimeGirlsDownloader.csproj -r win-x64 -p:Platform=x64
dotnet build AnimeGirlsDownloader.csproj -c Debug -p:Platform=x64
dotnet run --project AnimeGirlsDownloader.csproj -c Debug -p:Platform=x64
```

应用默认连接 `https://kafuumiaki.top/api/`。浏览和下载需要网络连接；上传、标签联想及账户资料同步需要登录。若部署自己的服务端，请修改 `AppConsts.AnimeGirlsApiEndpoint` 和公开图片链接地址。

## 浏览与下载流程

客户端刻意区分预览资源与原图资源：

1. `POST /api/Image/random` 或 `POST /api/Image/id` 只返回图片元数据、`PreviewUrl` 和 `DownloadUrl`，不返回图片字节数组。
2. 主界面将 `PreviewUrl` 直接交给 WinUI `BitmapImage`；服务端返回尺寸受限、可缓存的 JPEG 预览。
3. “复制图片”复制预览 URI 对应的位图，“复制图片链接”复制公开原图链接。
4. 只有用户点击保存时，任务才进入下载队列；主界面不会自动跳转到下载页面。
5. 队列严格一次下载一个任务。成功后弹出自动关闭的 InfoBar 并移除任务；失败后同样通知、保留任务并继续处理下一项，用户可以稍后重试。
6. 下载按钮角标显示当前用户的队列任务数，超过 99 显示 `99+`；全部任务停止且仍有失败项时显示 `×`。
7. 下载先写入 `.part` 临时文件，完成后释放文件流并原子改名；取消或失败时会清理临时文件。

## Native AOT 发布

`PublishAot` 在项目文件中保持无条件开启，因为 Windows App SDK 会在 NuGet 还原阶段决定是否导入 AOT 编译目标。发布前应按目标架构重新还原：

```powershell
dotnet restore AnimeGirlsDownloader.csproj -r win-x64 -p:Platform=x64 -p:Configuration=Release
dotnet publish AnimeGirlsDownloader.csproj -c Release -r win-x64 -p:Platform=x64 -p:PublishDir=bin\publish\win-x64\ --self-contained true --no-restore
```

发布目录为 `bin\publish\win-x64\`。有效的 Native AOT 产物应包含 `Anime Girls Downloader.exe`，且不包含应用的 `.dll` 或 `coreclr.dll`。WinUI 3 和 SkiaSharp 的原生依赖仍需与 EXE 一起分发，不能只复制应用 EXE。

其他架构可对应替换参数：

| 架构 | Runtime Identifier | Platform |
| --- | --- | --- |
| x64 | `win-x64` | `x64` |
| x86 | `win-x86` | `x86` |
| ARM64 | `win-arm64` | `ARM64` |

## 版本与更新

- 当前应用版本由 `AnimeGirlsDownloader.csproj` 中的 `Version` 属性统一定义。
- 每次发布前都应在 [CHANGELOG.md](CHANGELOG.md) 中新增对应版本及变更内容。
- 启动检查通过 GitHub `releases/latest` 接口异步执行；没有更新或请求失败时保持静默，不影响主界面操作。
- 设置页的“检查更新”用于主动检查，并会明确提示当前已是最新版本或检查失败。
- 发现更新后，程序优先打开适用于 Windows x64 的 Release 资源；找不到合适资源时打开该 Release 页面。

## 项目结构

```text
AnimeGirlsDownloader/
├─ Enums/               业务枚举
├─ Interfaces/          服务边界
├─ Models/              应用模型与设置模型
├─ Requests/            API 请求 DTO
├─ Responses/           API 响应 DTO
├─ Serialization/       AOT 安全的 JSON 源生成上下文
├─ Services/            API、设置、文件与令牌服务
├─ Strings/             多语言资源
├─ Styles/              XAML 主题和控件样式
├─ UserControls/        可复用 WinUI 控件
├─ App.xaml(.cs)        应用启动与依赖组装
├─ MainWindow.xaml(.cs) 主窗口交互
├─ DownloadPage.xaml(.cs) 下载队列与进度页面
├─ LoginPage.xaml(.cs)  登录与注册页面
├─ SettingPage.xaml(.cs) 设置页面
└─ UploadImagePage.xaml(.cs) 上传页面
```

主要边界如下：

- `IAnimeGirlsApiClient` 统一管理 HTTP、认证头、超时和响应反序列化。
- `ISettingService` 管理匿名/用户设置切换、校验与原子写入。
- `ITokenStore` 使用 Windows `LOCAL=user` 数据保护对登录令牌加密。
- `IFileService` 隔离 WinUI 文件选择器和图片编码。
- `IUserSessionService` 同步服务端用户资料并切换用户数据上下文。
- `IDownloadManager` 管理原图下载队列、进度、取消和用户可见性。
- UI 代码只处理界面状态、用户输入和服务调用结果。

## 本地数据

应用采用便携式目录结构，所有运行数据都位于可执行文件所在目录，不会再向 `%LOCALAPPDATA%` 等其他位置写入文件：

```text
应用目录/
├─ Assets/                         随程序分发的静态资源
├─ logs/                           所有用户共享的日志
│  └─ yyyy-MM-dd.log
└─ users/
   ├─ active-user.txt              自动登录所需的当前用户 ID 指针
   ├─ .legacy-token-migrated       防止重复读取旧版全局令牌的迁移标记
   ├─ anonymous/
   │  └─ config.json               未登录状态及应用启动语言设置
   └─ {用户ID}/
      ├─ auth.token                该用户独立的加密登录令牌
      ├─ config.json               该用户独立的设置
      └─ avatar.png                该用户同步到本地的头像
```

新登录令牌会先短暂保存在内存中；取得服务端用户 ID 后才写入对应用户目录。退出登录只删除当前用户的 `auth.token` 和活动用户指针，其他个人设置与头像会保留。

从旧版本首次启动时，应用会读取并复制旧安装目录或 `%LOCALAPPDATA%\AnimeGirlsDownloader` 中的设置、头像和令牌到新的便携式目录。迁移完成后不再向旧目录写入数据。

## 上传规则

- “添加”按钮可以多选图片，也可以添加文件夹；还可将图片与文件夹混合拖入路径列表。
- 文件夹上传会递归查找受支持的图片，重复路径会自动忽略。
- 路径列表使用图标区分图片和文件夹，每项均可独立移除。
- 只有列表中恰好存在一张独立图片时才显示预览并启用标签编辑；批量来源不会把一组标签错误地应用到所有图片。
- 上传者身份完全由服务端 JWT 决定，客户端请求中的显示名称不作为授权依据。

## 开发约定

- 新增需要序列化的 DTO 时，将类型加入 `Serialization/AppJsonSerializerContext.cs`，不要改回反射序列化。
- 不要在请求级别创建 `HttpClient`；统一通过 `IAnimeGirlsApiClient` 使用应用级客户端。
- UI 事件处理器可以使用 `async void`，其他异步方法应返回 `Task`。
- 发布前至少执行一次目标 RID 的 `restore`、Release `build` 和 Native AOT `publish`。
- 发布新版本时同步修改项目 `Version` 和 `CHANGELOG.md`，GitHub Release 标签建议使用 `v主版本.次版本.修订号`。
- 不要提交令牌、设置、日志或 `bin`/`obj` 产物。

## 许可

本项目使用 [MIT License](LICENSE.txt)。
