namespace SoftwareServer.Models;

public class SoftwarePackage
{
    public string Id { get; set; } = ""; // 唯一标识，如 "chrome"
    public string Name { get; set; } = ""; // 显示名称，如 "Google Chrome"
    public string Version { get; set; } = ""; // 当前版本，如 "120.0"
    public string? Description { get; set; } = ""; // 描述
    public string ExeName { get; set; } = ""; // 解压后要执行的 exe，如 "chrome.exe"
    public string ZipFileName { get; set; } = ""; // 服务器上的 zip 文件名
    public long FileSize { get; set; } // zip 文件大小（字节）
    public DateTime UpdatedAt { get; set; } // 最后更新时间
}

public class SoftwareListResponse
{
    public List<SoftwarePackage> Software { get; set; } = [];
}

public class PublishRequest
{
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string ExeName { get; set; } = "";
    public string Name { get; set; } = "";
}
