// Program.cs — Entry point của Comink.Api
using Comink.Api.Endpoints;
using Comink.Api.Hubs;
using Comink.Api.Realtime;
using Comink.Application.Admin.Abstractions;
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Application.Identity.Validators;
using Comink.Infrastructure;
using Comink.Infrastructure.Media;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
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

    // --- Phát sự kiện cuộc gọi (CallIncoming/CallUpdated/CallEnded) ---
    builder.Services.AddScoped<Comink.Application.Calls.Abstractions.ICallNotifier,
        Comink.Api.Realtime.SignalRCallNotifier>();

    // --- Quản trị (Giai đoạn 5): thông báo + ngắt kết nối SignalR khi tài khoản bị khóa ---
    // Sổ đăng ký kết nối phải là singleton: nó sống lâu hơn từng request/hub connection.
    builder.Services.AddSingleton<IUserConnectionRegistry, UserConnectionRegistry>();
    builder.Services.AddScoped<IAccountNotifier, SignalRAccountNotifier>();

    // --- Nhắn tin NC (Giai đoạn 7): chặn spam tín hiệu "đang nhập" phía server ---
    builder.Services.AddSingleton<ITypingThrottle, TypingThrottle>();

    // --- Dọn cuộc gọi đổ chuông quá hạn (30s) — bắt buộc phía server, client có thể đóng tab ---
    builder.Services.AddHostedService<Comink.Api.Realtime.CallRingTimeoutService>();

    // --- OpenAPI / Swagger ---
    // Mốc A: hợp đồng phải đủ ổn định để app Flutter sinh client từ đó.
    // - Khai báo security scheme Bearer JWT (Swagger UI hiện nút Authorize).
    // - Chỉ gắn yêu cầu bảo mật cho endpoint cần đăng nhập (bỏ login/register/refresh/health/webhook).
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token JWT lấy từ /api/auth/login (không kèm tiền tố Bearer)."
            };

            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var requiresAuth = metadata.OfType<IAuthorizeData>().Any();
            var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();

            if (requiresAuth && !allowsAnonymous)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                });
            }

            return Task.CompletedTask;
        });
    });

    // --- SignalR ---
    builder.Services.AddSignalR();

    // --- CORS ---
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebClient", policy =>
        {
            var allowedOrigins = Comink.Api.Common.CorsOriginsParser.Parse(
                builder.Configuration["FrontendUrl"]);
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // cần cho SignalR
        });
    });

    // --- Forwarded Headers (cần khi chạy sau reverse proxy như Railway) ---
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Reverse proxy của nền tảng cloud dùng IP động nên xóa danh sách mặc định (chỉ tin loopback).
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
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
    app.UseForwardedHeaders();
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

    // --- Trạng thái push FCM (Giai đoạn 6) ---
    // Thiếu cấu hình Firebase thì backend vẫn chạy và push bị bỏ qua (KI-06). Log rõ ở đây để
    // biết push đang BẬT hay TẮT, và cảnh báo khi đặt đường dẫn service account mà file không có
    // (trường hợp hay gặp nhất khi cấu hình FCM lần đầu).
    using (var pushScope = app.Services.CreateScope())
    {
        var pushOptions = pushScope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<
                Comink.Infrastructure.Notifications.FirebasePushOptions>>()
            .Value;
        var pushLogger = pushScope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Push.Fcm");

        if (!string.IsNullOrWhiteSpace(pushOptions.ServiceAccountJson))
        {
            pushLogger.LogInformation("Push FCM: BẬT (service account JSON lấy từ cấu hình)");
        }
        else if (!string.IsNullOrWhiteSpace(pushOptions.ServiceAccountPath))
        {
            if (File.Exists(pushOptions.ServiceAccountPath))
            {
                pushLogger.LogInformation(
                    "Push FCM: BẬT (service account: {Path})",
                    pushOptions.ServiceAccountPath);
            }
            else
            {
                pushLogger.LogWarning(
                    "Push FCM: TẮT — không tìm thấy file service account tại {Path}. "
                    + "Kiểm tra Firebase__ServiceAccountPath (hướng dẫn: mobile/README.md mục 3.2)",
                    pushOptions.ServiceAccountPath);
            }
        }
        else
        {
            pushLogger.LogInformation(
                "Push FCM: TẮT — thiếu Firebase__ServiceAccountPath/Firebase__ServiceAccountJson; "
                + "app vẫn chạy nhưng không có thông báo đẩy (hướng dẫn: mobile/README.md mục 3.2)");
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        // Tự động áp dụng migration và seed khi dev (bỏ qua khi dotnet ef chạy design-time)
        if (!EF.IsDesignTime)
        {
            await app.Services.ApplyMigrationsAsync();
        }
    }
    else if (!EF.IsDesignTime)
    {
        // Production: KHÔNG tự chạy migration (AGENTS.md mục 8.2), nhưng đảm bảo role
        // cơ bản (User, Admin) và tài khoản admin đầu tiên (nếu cấu hình AdminSeed) tồn tại.
        await app.Services.SeedEssentialDataAsync();
    }

    // Lỗi đọc body (JSON sai định dạng / sai kiểu / thiếu trường) do framework ném
    // BadHttpRequestException(400) → trả 400 + code VALIDATION_FAILED thay vì 500 INTERNAL_ERROR
    // (xem Common/ExceptionStatusMapper.cs và docs/05_api_contract.md mục 5).
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        StatusCodeSelector = Comink.Api.Common.ExceptionStatusMapper.StatusCodeFor
    });

    // Ghi log mọi lỗi không xử lý được (kèm đường dẫn) trước khi trả ProblemDetails — nếu không,
    // lỗi 500 chỉ hiện ở client dưới dạng "Máy chủ gặp lỗi" mà không có dấu vết phía server.
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("UnhandledException")
                .LogError(exception, "Lỗi không xử lý tại {Method} {Path}", context.Request.Method, context.Request.Path);
            throw;
        }
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
    app.MapCallEndpoints();
    app.MapLiveKitWebhookEndpoints();
    app.MapNotificationEndpoints();

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
