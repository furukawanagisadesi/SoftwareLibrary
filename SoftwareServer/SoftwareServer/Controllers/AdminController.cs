using Microsoft.AspNetCore.Mvc;
using SoftwareServer.Models;
using SoftwareServer.Services;

namespace SoftwareServer.Controllers;

/// <summary>
/// 管理员后台接口（管理网页使用）
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly SoftwareService _service;

    public AdminController(SoftwareService service)
    {
        _service = service;
    }

    // 获取所有软件（后台列表页）
    [HttpGet("software")]
    public IActionResult GetAll()
    {
        return Ok(_service.GetAll());
    }

    // 发布新版本（上传 zip + 填写信息）
    [HttpPost("software/{id}/publish")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Publish(
        string id,
        IFormFile zipFile,
        [FromForm] string version,
        [FromForm] string exeName,
        [FromForm] string? name = null,
        [FromForm] string? description = null
    )
    {
        id = id.ToLower();

        if (zipFile == null || zipFile.Length == 0)
            return BadRequest(new { message = "请上传 zip 文件" });

        if (string.IsNullOrWhiteSpace(version))
            return BadRequest(new { message = "版本号不能为空" });

        if (string.IsNullOrWhiteSpace(exeName))
            return BadRequest(new { message = "ExeName 不能为空" });

        var req = new PublishRequest
        {
            Version = version,
            ExeName = exeName,
            Name = name ?? id,
            Description = description,
        };

        var pkg = await _service.PublishAsync(id, zipFile, req);
        return Ok(pkg);
    }

    // 删除软件
    [HttpDelete("software/{id}")]
    public IActionResult Delete(string id)
    {
        var success = _service.Delete(id);
        if (!success)
            return NotFound(new { message = $"软件 {id} 不存在" });
        return Ok(new { message = "删除成功" });
    }
}
