using SoftwareServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<SoftwareService>();
builder.Services.AddHostedService<GitDaemonService>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Admin 接口 API Key 验证 ──────────────────────────────
var adminKey = builder.Configuration["AdminKey"] ?? "";

app.Use(
    async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/admin"))
        {
            // 从请求头读取 Key
            var key = context.Request.Headers["X-Admin-Key"].FirstOrDefault() ?? "";
            if (key != adminKey)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"message\":\"未授权，请提供正确的管理员密钥\"}"
                );
                return;
            }
        }
        await next();
    }
);

// ────────────────────────────────────────────────────────

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseStaticFiles();

// ── 单实例互斥：防止双击 + 服务（或重复启动）同时运行导致端口/数据冲突 ──
using var singleInstance = new Mutex(initiallyOwned: false, @"Global\SoftwareServerInstance", out _);
bool acquired = false;
try
{
    acquired = singleInstance.WaitOne(TimeSpan.Zero);
}
catch (AbandonedMutexException)
{
    acquired = true; // 上一实例异常退出未释放，本实例接管
}

if (!acquired)
{
    app.Logger.LogWarning("检测到已有 SoftwareServer 实例在运行，本实例退出");
    return;
}

app.Run();
