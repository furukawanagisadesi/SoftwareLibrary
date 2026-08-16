using Microsoft.AspNetCore.Mvc;
using SoftwareServer.Services;

namespace SoftwareServer.Controllers;

/// <summary>
/// Scoop 私有 Bucket HTTP 端点
/// 用户可通过以下方式使用：
///   scoop install http://server:15000/api/scoop/manifest/rapidee       ← 直接安装
///   scoop install rapidee (需先添加 bucket)                             ← 通过 bucket 安装
///   scoop bucket add myapps http://server:15000/api/scoop              ← 添加 HTTP bucket
/// </summary>
[ApiController]
[Route("api/scoop")]
public class ScoopController : ControllerBase
{
    private readonly SoftwareService _service;

    public ScoopController(SoftwareService service)
    {
        _service = service;
    }

    /// <summary>获取单个 App 的 Scoop Manifest</summary>
    [HttpGet("manifest/{appName}")]
    public IActionResult GetManifest(string appName)
    {
        appName = appName.ToLower().Replace(".json", "");
        if (!SoftwareService.IsValidId(appName))
            return NotFound(new { message = $"软件 {appName} 的 manifest 不存在" });
        var path = _service.GetScoopManifestPath(appName);
        if (path == null)
            return NotFound(new { message = $"软件 {appName} 的 manifest 不存在" });
        return PhysicalFile(path, "application/json");
    }

    /// <summary>
    /// Scoop 原生 HTTP Bucket 兼容端点：
    ///   scoop bucket add myapps http://server:15000/api/scoop
    /// 之后 scoop install rapidee → 自动请求 GET /api/scoop/rapidee.json
    /// </summary>
    [HttpGet("{appName}.json")]
    public IActionResult GetManifestByBucket(string appName)
    {
        return GetManifest(appName);
    }

    /// <summary>列出所有可用 App（名称 + 版本 + 描述）</summary>
    [HttpGet("apps")]
    public IActionResult GetApps()
    {
        var apps = _service.GetScoopApps();
        return Ok(apps);
    }

    /// <summary>
    /// Bucket 包列表（兼容 ScoopInstaller/maintained 分支的 HTTP bucket 探测）
    /// 返回所有可安装的应用名称数组
    /// </summary>
    [HttpGet("packages.json")]
    public IActionResult GetPackages()
    {
        var apps = _service.GetScoopApps();
        return Ok(apps.Select(a => a.Name).ToList());
    }

    /// <summary>Bucket 元信息（兼容 scoop bucket add 探测）</summary>
    [HttpGet("bucket.json")]
    public IActionResult GetBucketInfo()
    {
        var apps = _service.GetScoopApps();
        return Ok(new
        {
            homepage = Request.Scheme + "://" + Request.Host,
            description = "私有软件仓库",
            manifestCount = apps.Count,
        });
    }
}
