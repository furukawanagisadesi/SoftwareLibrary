using Microsoft.AspNetCore.Mvc;
using SoftwareServer.Services;

namespace SoftwareServer.Controllers;

/// <summary>
/// 客户端调用的接口（SoftwareManager.exe 和 Launcher.exe 使用）
/// </summary>
[ApiController]
[Route("api/software")]
public class SoftwareController : ControllerBase
{
    private readonly SoftwareService _service;

    public SoftwareController(SoftwareService service)
    {
        _service = service;
    }

    // 获取所有软件列表（SoftwareManager 用来展示列表）
    [HttpGet("list")]
    public IActionResult GetList()
    {
        var list = _service.GetAll();
        return Ok(list);
    }

    // 获取单个软件信息（Launcher 用来检查版本）
    [HttpGet("{id}/info")]
    public IActionResult GetInfo(string id)
    {
        var pkg = _service.GetById(id);
        if (pkg == null) return NotFound(new { message = $"软件 {id} 不存在" });
        return Ok(pkg);
    }

    // 下载 zip 包（SoftwareManager 和 Launcher 更新时使用）
    [HttpGet("{id}/download")]
    public IActionResult Download(string id)
    {
        var zipPath = _service.GetZipPath(id);
        if (zipPath == null) return NotFound(new { message = $"软件 {id} 的安装包不存在" });

        var pkg = _service.GetById(id)!;
        var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
        return File(stream, "application/zip", $"{id}_{pkg.Version}.zip");
    }
}
