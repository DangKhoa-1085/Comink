// Program.cs — Entry point của Comink.Api
using Comink.Api.Endpoints;
using Comink.Api.Hubs;
using Comink.Application.Common;
using Comink.Application.Identity.Validators;
using Comink.Infrastructure;
using FluentValidation;
using Serilog;

// --- Cấu hình Serilog sớm để bắt lỗi khởi động ---
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    // --- Nạp biến môi trường từ file .env ---
    // Thư mục làm việc khi `dotnet run` là thư mục project (backend/src/Comink.Api),
    // nên phải dò ngược lên các thư mục cha để tìm .env ở gốc repository.
    // File .env là tuỳ chọn: thiếu file thì dùng appsettings/user-secrets.
    // Biến môi trường đã có sẵn trong hệ điều hành KHÔNG bị ghi đè.
    static string? FindEnvFile(string startDirectory, int maxLevels = 6)
    {
        var dir = new DirectoryInfo(startDirectory);
        for (var i = 0; i < maxLevels && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    var envFilePath = FindEnvFile(Directory.GetCurrentDirectory());
    if (envFilePath is not null)
    {
        DotNetEnv.Env.Load(envFilePath);
        Log.Information("Đã nạp cấu hình từ {EnvFile}", envFilePath);
    }
    else
    {
        Log.Information("Không tìm thấy file .env — dùng appsettings và user-secrets");
    }

    var builder = WebApplication.CreateBuilder(args);

    // --- Serilog ---
    builder.Host.UseSerilog((context, services, config) =>
    {
        config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    });

    // --- Infrastructure (DbContext, Identity, JWT, Redis, v.v.) ---
    builder.Services.AddInfrastructure(builder.Configuration);

    // --- Validator của FluentValidation (đặt ở tầng Application) ---
    builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

    // --- OpenAPI / Swagger ---
    builder.Services.AddOpenApi();

    // --- SignalR ---
    builder.Services.AddSignalR();

    // --- CORS ---
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebClient", policy =>
        {
            var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:5173";
            policy.WithOrigins(frontendUrl)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // cần cho SignalR
        });
    });

    // --- ProblemDetails cho error handling thống nhất ---
    // Lỗi do framework sinh (401 chưa xác thực, 403 thiếu quyền, 404 không tìm thấy, 405…)
    // không đi qua ApiResults nên thiếu trường `code`; bổ sung tại đây để client luôn dịch
    // được sang vi/en (docs/05_api_contract.md mục 5). Lỗi nghiệp vụ đã tự đặt `code`
    // trong ApiResults nên được giữ nguyên.
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            if (context.ProblemDetails.Extensions.ContainsKey("code"))
            {
                return;
            }

            var code = context.ProblemDetails.Status switch
            {
                StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
                StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
                StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
                StatusCodes.Status404NotFound => ErrorCodes.NotFound,
                StatusCodes.Status500InternalServerError => ErrorCodes.InternalError,
                _ => null
            };

            if (code is not null)
            {
                context.ProblemDetails.Extensions["code"] = code;
            }
        };
    });

    var app = builder.Build();

    // --- Middleware pipeline ---
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        // Tự động áp dụng migration và seed khi dev
        await app.Services.ApplyMigrationsAsync();
    }

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseCors("AllowWebClient");
    app.UseAuthentication();
    app.UseAuthorization();

    // --- Health check endpoint ---
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
       .WithName("HealthCheck")
       .WithTags("System");

    // --- Endpoint nghiệp vụ (vertical slice theo nhóm tính năng) ---
    app.MapAuthEndpoints();
    app.MapUserEndpoints();
    app.MapAdminEndpoints();

    // --- Hub SignalR (Giai đoạn 1 chỉ cần xác thực được bằng token) ---
    app.MapHub<ChatHub>("/hub/chat");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Ứng dụng khởi động thất bại");
}
finally
{
    Log.CloseAndFlush();
}

// Cần cho integration tests
public partial class Program { }
