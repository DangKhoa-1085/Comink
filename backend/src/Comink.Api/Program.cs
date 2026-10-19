// Program.cs — Entry point của Comink.Api
using Comink.Api.Endpoints;
using Comink.Api.Hubs;
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Application.Identity.Validators;
using Comink.Infrastructure;
using Comink.Infrastructure.Media;
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

    // --- Phát sự kiện thời gian thực cho chat (cài đặt SignalR nằm ở tầng Api) ---
    builder.Services.AddScoped<Comink.Application.Messaging.Abstractions.IChatNotifier,
        Comink.Api.Realtime.SignalRChatNotifier>();

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
        // Gắn `code` + `title` cho lỗi do framework sinh (401/403/404/… và 400 khi body JSON hỏng)
        // theo đúng một nguồn duy nhất là `code` — xem ApiResults.ApplyProblemDefaults.
        options.CustomizeProblemDetails = context =>
            Comink.Api.Common.ApiResults.ApplyProblemDefaults(context.ProblemDetails);
    });

    var app = builder.Build();

    // --- Middleware pipeline ---
    app.UseSerilogRequestLogging();

    // --- Phục vụ tệp lưu cục bộ khi dev (phương án B: chỉ bật khi KHÔNG dùng Cloudinary) ---
    using (var mediaScope = app.Services.CreateScope())
    {
        var mediaOptions = mediaScope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaStorageOptions>>().Value;

        if (!mediaOptions.UseCloudinary
            && mediaScope.ServiceProvider.GetRequiredService<IMediaStorage>() is LocalDiskMediaStorage localStorage)
        {
            Directory.CreateDirectory(localStorage.RootPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(localStorage.RootPath),
                RequestPath = mediaOptions.PublicBaseUrl
            });
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        // Tự động áp dụng migration và seed khi dev
        await app.Services.ApplyMigrationsAsync();
    }

    // Lỗi đọc body (JSON sai định dạng / sai kiểu / thiếu trường) do framework ném
    // BadHttpRequestException(400) → trả 400 + code VALIDATION_FAILED thay vì 500 INTERNAL_ERROR
    // (xem Common/ExceptionStatusMapper.cs và docs/05_api_contract.md mục 5).
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        StatusCodeSelector = Comink.Api.Common.ExceptionStatusMapper.StatusCodeFor
    });
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
    app.MapFriendshipEndpoints();
    app.MapFollowEndpoints();
    app.MapPostEndpoints();
    app.MapCommentEndpoints();
    app.MapMediaEndpoints();
    app.MapMessagingEndpoints();

    // --- Hub SignalR ---
    // Group hội thoại được thêm khi kết nối (ChatHub.OnConnectedAsync); phát sự kiện qua IChatNotifier.
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
