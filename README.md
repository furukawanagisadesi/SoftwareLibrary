# SoftwareLibrary

一个基于 .NET 8 的软件分发和管理系统，支持软件的集中管理、自动更新和一键部署。

## 🏗️ 系统架构

本系统由四个核心组件构成：

### 1. Bootstrap 启动器
- **功能**：系统的入口程序，负责初始化环境和启动更新流程
- **主要职责**：
  - 创建必要的目录结构
  - 检查并下载最新的 Updater 程序
  - 启动软件管理器
- **技术特点**：WinForms 应用，轻量级启动逻辑

### 2. SoftwareServer 服务端
- **功能**：软件包管理和 API 服务
- **核心技术**：ASP.NET Core Web API
- **主要特性**：
  - RESTful API 接口
  - 软件包上传和管理
  - 版本控制和分发
  - Swagger 文档支持
- **默认端口**：192.168.16.52:15000

### 3. SoftwareManager 软件管理器
- **功能**：用户界面客户端，用于浏览和管理软件
- **主要功能**：
  - 软件列表展示
  - 安装、卸载、更新操作
  - 进度显示和状态监控
  - 本地软件记录管理
- **技术栈**：WinForms + HttpClient

### 4. Updater 更新器
- **功能**：负责软件的实际下载和更新过程
- **核心能力**：
  - 增量更新检测
  - ZIP 包解压部署
  - 桌面快捷方式创建
  - 运行中软件自动关闭

## 📁 项目结构

```
SoftwareLibrary/
├── Bootstrap/           # 启动器项目
├── SoftwareManager/     # 软件管理器客户端
├── SoftwareServer/      # 服务端API
├── Updater/            # 更新器组件
└── README.md          # 项目文档
```

## 🔧 技术栈

- **开发语言**：C# 12
- **框架版本**：.NET 8.0
- **前端框架**：Windows Forms
- **后端框架**：ASP.NET Core Web API
- **数据格式**：JSON
- **压缩格式**：ZIP

## 🚀 快速开始

### 环境要求
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 或 VS Code

### 构建项目

```bash
# 克隆项目
git clone <repository-url>
cd SoftwareLibrary

# 恢复 NuGet 包
dotnet restore

# 构建所有项目
dotnet build
```

### 运行服务端

```bash
cd SoftwareServer
dotnet run
```

服务将启动在 `http://192.168.16.52:15000`

### 运行客户端

```bash
# 运行启动器（推荐）
cd Bootstrap
dotnet run

# 或直接运行软件管理器
cd SoftwareManager
dotnet run
```

## ⚙️ 配置说明

### 服务端配置 (appsettings.json)

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://192.168.16.52:15000"
      }
    }
  }
}
```

### 客户端配置

客户端会自动生成配置文件，主要包含：
- 服务端地址
- 本地安装目录
- 软件仓库路径

## 📊 API 接口

### 软件管理接口

| 接口 | 方法 | 描述 |
|------|------|------|
| `/api/software/list` | GET | 获取所有软件列表 |
| `/api/software/{id}/info` | GET | 获取指定软件信息 |
| `/api/software/{id}/download` | GET | 下载软件安装包 |

### 管理员接口

| 接口 | 方法 | 描述 |
|------|------|------|
| `/admin/index.html` | GET | 管理后台页面 |

## 🛠️ 部署指南

### 生产环境部署

1. **构建发布版本**
```bash
dotnet publish -c Release -o ./publish
```

2. **配置服务端**
```bash
# 修改 appsettings.Production.json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://your-server-ip:port"
      }
    }
  }
}
```

3. **运行服务**
```bash
cd publish
dotnet SoftwareServer.dll
```

### 客户端部署

1. 将 Bootstrap.exe 分发给最终用户
2. 用户首次运行时会自动下载完整套件
3. 后续通过 Updater 自动更新

## 🐛 故障排除

### 常见问题

1. **无法连接到服务器**
   - 检查网络连接
   - 验证服务端地址配置
   - 确认防火墙设置

2. **更新失败**
   - 查看 error.log 文件
   - 检查磁盘空间
   - 确认权限设置

3. **软件启动失败**
   - 检查安装路径权限
   - 验证 .NET 运行时
   - 查看 launch.log 日志

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

## 📞 联系方式

如有问题请提交 Issue 或联系项目维护者。