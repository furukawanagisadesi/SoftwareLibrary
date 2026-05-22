using SoftwareServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<SoftwareService>();
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

app.Run();
