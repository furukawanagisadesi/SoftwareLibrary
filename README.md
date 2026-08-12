# SoftwareLibrary

一个基于 .NET 8 的企业软件分发与管理系统：集中托管软件安装包，并原生生成 [Scoop](https://scoop.sh) manifest，供本机 / 局域网 / GitHub 上的机器直接通过 `scoop install` 安装与升级。

## 🏗️ 系统架构

```
     管理员（管理后台/API）
            │ 上传 zip + 元信息
            ▼
┌───────────────────────────────┐
│      SoftwareServer (.NET 8)  │   Web 服务 :15000
│  ┌─────────────────────────┐  │
│  │ packages/{id}/{ver}/    │  │   zip 存储
│  │ software-list.json      │  │   元数据
│  │ scoop/bucket/*.json     │  │   Scoop Manifest（git 仓库）
│  └─────────────────────────┘  │
│  GitDaemonService (git://)     │   自动拉起 git daemon :9418
│  自动 git commit                │   发布/改版/删除后自动提交
└───────────────────────────────┘
            │ git://  /  http://
            ▼
   Scoop 客户端 → scoop install <app>
```

当前仓库中，**SoftwareServer 为活跃维护组件**（已部署、实测运行）；Bootstrap / SoftwareManager / Updater 为早期 WinForms 客户端组件，保留在 `Backup/` 作为历史参考。

## 📁 目录结构

```
SoftwareLibrary/
├── SoftwareServer/            # 服务端（活跃，本文档主体）
│   └── SoftwareServer/        # ASP.NET Core Web API 项目
│       ├── Controllers/       # AdminController / SoftwareController / ScoopController
│       ├── Models/            # SoftwarePackage / PublishRequest
│       ├── Services/          # SoftwareService / GitDaemonService
│       └── wwwroot/admin/     # 管理后台前端（单文件 index.html）
├── Backup/                    # 历史 WinForms 客户端（仅供参考）
│   ├── Bootstrap/             # 启动器
│   ├── SoftwareManager/       # 客户端管理器
│   └── Updater/               # 更新器
├── SoftwareLibrary.slnx
├── .github/workflows/
├── .gitignore
└── README.md
```

## 🔧 技术栈

- **语言 / 框架**：C# 12，.NET 8（ASP.NET Core Web API）
- **存储**：文件系统（无数据库），元数据为 `software-list.json`（JSON）
- **前端**：Vue 风格原生 JS 单文件（`wwwroot/admin/index.html`）
- **接口文档**：Swagger（开发环境 `/swagger`）
- **Scoop 集成**：manifest 生成 + git bucket + git daemon
- **压缩格式**：ZIP

## 🚀 快速开始

### 环境要求
- Windows 10/11，.NET 8.0 SDK
- 已安装 `git`（Server 会自动拉起 `git daemon`，并在发布时执行自动 commit）

### 构建与运行

```bash
cd SoftwareServer/SoftwareServer
dotnet build -c Release
dotnet run -c Release          # 或直接运行 bin\Release\net8.0\SoftwareServer.exe
```

服务默认监听 `http://0.0.0.0:15000`：
- 管理后台：`http://127.0.0.1:15000/admin/index.html`
- Swagger（开发环境）：`http://127.0.0.1:15000/swagger`

### 后台两个端口
| 端口 | 协议 | 用途 |
|---|---|---|
| 15000 | HTTP | 管理后台 + 下载 + Scoop manifest API |
| 9418  | git:// | 局域网 Scoop bucket（`git://<IP>:9418/scoop`） |

## ⚙️ 配置说明（appsettings.json）

```json
{
  "AdminKey": "admin",                        // 管理后台密钥（请求头 X-Admin-Key）
  "Storage": { "PackagesDir": "..." },        // 软件包根目录（含 packages/scoop git 仓库）
  "GitDaemon": {                              // git daemon 自动拉起
    "Enabled": true,
    "Port": 9418,
    "BasePath": "..."                         // 必须是 Windows 原生路径（如 F:\...\packages）
  },
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:15000" } } },
  "ServerUrl": "http://localhost:15000"       // 写入 manifest 的下载地址（远端消费需改成本机可达地址）
}
```

> **注意**：`ServerUrl` 会写入 Scoop manifest 的 `url` 字段。若仅本机使用可保持 `localhost`；局域网/公网消费需改为 `http://<服务器IP>:15000` 并执行 `POST /api/admin/scoop/regenerate` 批量重建 manifest，之后确认 git 仓库已自动 commit。

配置 `Scoop:AutoCommit`（默认 `true`）控制发布/改版/删除后是否自动 git commit。

## 📦 Scoop 集成（两条消费路径）

### A. git bucket（推荐，支持 `scoop update`）
Server 启动时自动拉起 git daemon，并将发布的 manifest 写入 `packages/scoop`（一个 git 仓库），随后自动 `git commit`：

```bash
scoop bucket add myapps git://192.168.16.52:9418/scoop
scoop install myapps/<app>
scoop update myapps           # 注意：update 传 bucket 名不是 app 名
```

> `scoop bucket add` 只认 git 仓库 URL；用 HTTP URL 会 git clone 失败。

### B. 直接 URL 安装（不能 update）
```bash
scoop install http://<IP>:15000/api/scoop/manifest/<app>
```

### 自动 git commit
- **发布 / 批量发布** → `publish <id> v<version>`
- **修改版本号 / 信息** → `update <id> v<version>`
- **删除软件** → `delete <id>`
- **重建全部 manifest** → `regenerate N manifests`

修改后的 manifest 会立刻 commit，远端 `git clone`/`scoop update` 拉到最新。

## 📊 API 清单

### 公共接口
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/software/list` | 软件列表 |
| GET | `/api/software/{id}/info` | 单个软件信息 |
| GET | `/api/software/{id}/download` | 下载 zip（管理员接口外无鉴权） |

### Scoop 接口
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/scoop/manifest/{app}` | 单个 manifest（直接安装用） |
| GET | `/api/scoop/{app}.json` | HTTP bucket 兼容端点 |
| GET | `/api/scoop/apps` | 应用列表（名称+版本+描述） |
| GET | `/api/scoop/packages.json` | 可安装应用名称数组 |
| GET | `/api/scoop/bucket.json` | bucket 元信息 |

### 管理员接口（需请求头 `X-Admin-Key`）
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/admin/software` | 后台软件列表 |
| POST | `/api/admin/software/{id}/publish` | 上传 zip + 发布（multipart form） |
| POST | `/api/admin/software/batch` | 批量发布（文件名=软件ID，默认 1.0） |
| PUT | `/api/admin/software/{id}` | 改信息/改版本号（自动迁移版本目录） |
| DELETE | `/api/admin/software/{id}` | 删除（含 manifest + 目录） |
| POST | `/api/admin/scoop/regenerate` | 重建全部 manifest（ServerUrl 变更后调用） |

## 🖥️ 管理后台使用

1. 打开 `http://127.0.0.1:15000/admin/index.html`，输入密钥（`X-Admin-Key` 对应值，默认 `admin`）
2. **发布 / 更新**：填写 ID、名称、版本号、EXE 文件名，选择 ZIP 上传；重复发布即覆盖当前版本
3. **修改版本号**：在列表点「编辑」修改版本号，Server 自动迁移目录（不要求重新上传包）
4. **批量上传**：文件名即软件 ID，版本默认 1.0，上传后在列表编辑完善名称/EXE
5. 发布/删除后，Server 自动 commit，局域网 Scoop 客户端立即拉到新版本

## 🛠️ 部署指南

### 生产部署
```bash
dotnet publish -c Release -o ./publish
```
将产物拷贝到部署目录（如 `F:\Applications\SoftwareServer`），**不要覆盖已配置的 `appsettings.json`** 与 `packages/` 数据目录，然后运行 `SoftwareServer.exe`。

> 部署陷阱：源代码目录与部署目录是两个不同位置，运行时的真实配置以**部署目录**的 `appsettings.json` 为准。

### 局域网消费端
```bash
git remote add / set-url origin git://192.168.16.52:9418/scoop   # 已 clone 的分支
scoop bucket add myapps git://192.168.16.52:9418/scoop            # 新装
```

## ⚠️ 已知注意事项

- **ServiceUrl**：远端下载依赖 `ServerUrl` 可达；目前默认 `localhost`，公网/局域网消费需改为服务器地址后 regenerate。
- **下载接口无鉴权**：`/api/software/{id}/download` 未做权限校验，公网暴露前需处理。
- **git daemon 停止**：Server 优雅关闭时按端口清理 git daemon；强制结束进程（任务管理器）会使 daemon 成为孤儿进程，直到下次优雅关闭才被回收。
- **HTTP bucket 端点**：`bucket.json` / `packages.json` 等 Http 端点标准 scoop 不消费，仅对自定义客户端有意义（标准 scoop 需用 git bucket 路径）。

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request。

## 📄 License

见仓库根目录 [LICENSE](./LICENSE)。