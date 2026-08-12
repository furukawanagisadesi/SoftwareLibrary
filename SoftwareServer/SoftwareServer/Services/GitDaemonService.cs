using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;

namespace SoftwareServer.Services;

/// <summary>
/// 启动时自动拉起 git daemon，为局域网内 scoop bucket 提供 git:// 协议访问。
/// 配置节: GitDaemon { Enabled, Port, BasePath }
/// 启动前检查: git 是否可用、端口是否已被占用（已在运行则跳过）。
/// </summary>
public class GitDaemonService : IHostedService, IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<GitDaemonService> _logger;
    private Process? _process;

    public GitDaemonService(IConfiguration config, ILogger<GitDaemonService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = _config.GetValue("GitDaemon:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("GitDaemon 未启用（GitDaemon:Enabled=false），跳过启动");
            return Task.CompletedTask;
        }

        try
        {
            // ── 检查 1: git 是否可用 ──────────────────────────────
            var gitPath = FindGitPath();
            if (string.IsNullOrEmpty(gitPath))
            {
                _logger.LogWarning("未找到 git 可执行文件，git daemon 无法启动");
                return Task.CompletedTask;
            }

            // ── 检查 2: 端口是否已被占用（git daemon 是否已在运行）──
            var port = _config.GetValue("GitDaemon:Port", 9418);
            if (IsPortInUse(port))
            {
                _logger.LogInformation("端口 {Port} 已被占用，git daemon 已在运行，跳过启动", port);
                return Task.CompletedTask;
            }

            // ── 检查 3: base path 是否存在 ────────────────────────
            var basePath = _config["GitDaemon:BasePath"];
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            {
                _logger.LogWarning("GitDaemon:BasePath 无效或不存在的目录: {Path}", basePath);
                return Task.CompletedTask;
            }

            // ── 启动 git daemon ───────────────────────────────────
            StartGitDaemon(gitPath, basePath, port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "git daemon 启动检查/启动过程发生异常");
        }

        return Task.CompletedTask;
    }

    private void StartGitDaemon(string gitPath, string basePath, int port)
    {
        var psi = new ProcessStartInfo
        {
            FileName = gitPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        // 注意: Windows 原生路径（不能用 MSYS 的 /f/... 写法）
        psi.ArgumentList.Add("daemon");
        psi.ArgumentList.Add("--reuseaddr");
        psi.ArgumentList.Add("--base-path=" + basePath);
        psi.ArgumentList.Add("--export-all");
        psi.ArgumentList.Add("--port=" + port);

        try
        {
            _process = new Process { StartInfo = psi };
            _process.Start();
            _logger.LogInformation(
                "git daemon 已启动: {Git} daemon --base-path={Base} --port={Port}",
                gitPath, basePath, port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "git daemon 启动失败（路径: {Git}）", gitPath);
        }
    }

    /// <summary>查找 git 可执行文件（git.exe / git），供 git daemon 与自动提交共用</summary>
    public static string? FindGitPath()
    {
        // 1. 常见安装路径
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "git.exe"),
            "git.exe",
            "git",
        };

        foreach (var c in candidates)
        {
            try
            {
                if (File.Exists(c))
                    return c;
            }
            catch { /* 忽略非法路径 */ }
        }

        // 2. 尝试通过 PATH 解析（在 bash/git-bash 环境里也能找到）
        try
        {
            var psi = new ProcessStartInfo("where", "git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var outLine = p.StandardOutput.ReadLine();
                p.WaitForExit(3000);
                if (!string.IsNullOrWhiteSpace(outLine) && File.Exists(outLine.Trim()))
                    return outLine.Trim();
            }
        }
        catch { /* where 不可用则忽略 */ }

        return null;
    }

    /// <summary>检查 TCP 端口是否已被监听（判定 git daemon 是否在运行）</summary>
    private static bool IsPortInUse(int port)
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return listeners.Any(l => l.Port == port);
        }
        catch
        {
            return false;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // 1. 结束由本进程直接启动的 git.exe（其子进程链带头拉起的 git-daemon.exe）
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _logger.LogInformation("停止 git daemon 进程树 (PID {Pid})", _process.Id);
                TryKillTree(_process);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "结束 git daemon 进程树 (PID {Pid}) 失败", _process.Id);
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }

        // 2. 兜底：结束监听 GitDaemon 端口上的真实进程（git-daemon.exe）。
        //    原因：git.exe 是原生 Windows 程序，而其实际 daemon 进程是
        //    git-daemon.exe（git.exe 的子进程），且历史上出现过 SoftwareServer
        //    重启后端口被旧 daemon 占用、当前实例 _process 为 null 而杀不掉的孤儿进程。
        //    按端口精确定位并只处理 git 相关进程，避免误杀其他程序。
        var port = _config.GetValue("GitDaemon:Port", 9418);
        var daemonPid = FindPidListeningOn(port);
        if (daemonPid <= 0)
            return Task.CompletedTask;

        try
        {
            using var daemonProc = Process.GetProcessById(daemonPid);
            var name = daemonProc.ProcessName;
            if (name.StartsWith("git", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("停止 git daemon (PID {Pid}, {Name})", daemonPid, name);
                TryKillTree(daemonProc);
            }
            else
            {
                _logger.LogInformation("端口 {Port} 上的进程 {Name} (PID {Pid}) 非 git daemon，跳过", port, name, daemonPid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止 git daemon (PID {Pid}) 失败", daemonPid);
        }

        return Task.CompletedTask;
    }

    /// <summary>杀死整个进程树并等待退出；失败时抛出，由调用方记录日志</summary>
    private static void TryKillTree(Process proc)
    {
        proc.Kill(entireProcessTree: true);
        proc.WaitForExit(3000);
    }

    /// <summary>查找监听指定端口的进程 PID（netstat -ano 解析，兼容中文/英文系统）</summary>
    private static int FindPidListeningOn(int port)
    {
        try
        {
            var psi = new ProcessStartInfo("netstat", "-ano")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null)
                return 0;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);

            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    continue;
                // 形如: TCP  0.0.0.0:9418  0.0.0.0:0  LISTENING  16332
                if (!parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!parts[1].EndsWith(":" + port, StringComparison.Ordinal))
                    continue;
                if (int.TryParse(parts[4], out var pid) && pid > 0)
                    return pid;
            }
        }
        catch
        {
            // netstat 不可用则忽略
        }
        return 0;
    }

    public void Dispose()
    {
        _process?.Dispose();
    }
}
