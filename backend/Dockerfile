# Stage 1: Build & publish (.NET 10 SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Sao chép cấu hình SDK và các .csproj trước để tận dụng Docker layer cache khi restore
COPY global.json ./
COPY src/Comink.Domain/Comink.Domain.csproj src/Comink.Domain/
COPY src/Comink.Application/Comink.Application.csproj src/Comink.Application/
COPY src/Comink.Infrastructure/Comink.Infrastructure.csproj src/Comink.Infrastructure/
COPY src/Comink.Api/Comink.Api.csproj src/Comink.Api/

RUN dotnet restore src/Comink.Api/Comink.Api.csproj

# Sao chép toàn bộ mã nguồn và xuất bản bản Release
COPY src/ src/
RUN dotnet publish src/Comink.Api/Comink.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime gọn nhẹ (.NET 10 ASP.NET Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

# Chạy bằng non-root user có sẵn của image .NET chính thức
USER $APP_UID

EXPOSE 8080

# Tự động nhận biến PORT do Railway cấp (mặc định 8080 khi chạy cục bộ)
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet Comink.Api.dll"]
