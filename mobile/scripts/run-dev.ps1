<#
.SYNOPSIS
  Chạy app Comink trên thiết bị Android thật với cấu hình trong config/dev.json.

.DESCRIPTION
  - Đọc API_BASE_URL + Firebase từ config/dev.json (không chứa secret trong script).
  - Nếu API_BASE_URL là 127.0.0.1/localhost: tự chạy `adb reverse` cho cả
    cổng `5117` (Backend/SignalR) và `7880` (LiveKit) để điện thoại gọi qua cáp USB
    (không cần LAN, không cần mở tường lửa).
  - Kiểm tra /health trước khi chạy app để báo lỗi sớm.

.EXAMPLE
  .\scripts\run-dev.ps1
  .\scripts\run-dev.ps1 -Device R58M45SH22R
  .\scripts\run-dev.ps1 -CheckOnly
  .\scripts\run-dev.ps1 -BuildApk
#>
[CmdletBinding()]
param(
    [string]$Device = '',
    [switch]$Lan,
    [switch]$CheckOnly,
    [switch]$BuildApk
)

$ErrorActionPreference = 'Stop'
$mobileDir = Split-Path -Parent $PSScriptRoot
Set-Location $mobileDir

$configPath = Join-Path $mobileDir 'config\dev.json'
if (-not (Test-Path $configPath)) {
    throw "Missing config/dev.json. Copy config/dev.example.json to config/dev.json, then fill API_BASE_URL and Firebase values."
}

$config = Get-Content $configPath -Raw | ConvertFrom-Json
$apiBase = [string]$config.API_BASE_URL
if ([string]::IsNullOrWhiteSpace($apiBase)) {
    throw 'API_BASE_URL is missing in config/dev.json.'
}
$apiBase = $apiBase.TrimEnd('/')

$usesLoopback = $apiBase -match '127\.0\.0\.1|localhost'
if ($Lan -and $usesLoopback) {
    Write-Warning "API_BASE_URL is $apiBase. For LAN mode set it to http://<LAN-IP>:5117 in config/dev.json."
}

# --- Chọn thiết bị ---
$deviceLines = @(adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '^\S+\s+device\s*$' })
$serials = @($deviceLines | ForEach-Object { ($_ -split '\s+')[0] })
if ($serials.Count -eq 0) {
    throw 'No Android device found. Connect it via USB, enable USB debugging, then check `adb devices` (state must be `device`).'
}

$serial = $Device
if ([string]::IsNullOrWhiteSpace($serial)) {
    $serial = $serials[0]
    if ($serials.Count -gt 1) {
        Write-Host "Multiple devices found; using $serial. Pass -Device <serial> to choose another." -ForegroundColor Yellow
    }
}
elseif ($serials -notcontains $serial) {
    throw "Device '$serial' not found. Connected devices: $($serials -join ', ')"
}
Write-Host "Device: $serial" -ForegroundColor Green

# --- USB: chuyển cổng 5117 (API/SignalR) và 7880 (LiveKit) của điện thoại về máy tính ---
if ($usesLoopback) {
    adb -s $serial reverse tcp:5117 tcp:5117 | Out-Null
    adb -s $serial reverse tcp:7880 tcp:7880 | Out-Null
    Write-Host 'adb reverse: device tcp:5117, tcp:7880 -> host (chạy lại mỗi khi cắm cáp/khởi động lại máy)' -ForegroundColor Green
}

# --- Kiểm tra backend bằng đúng URL app sẽ dùng ---
try {
    $health = Invoke-RestMethod -Uri "$apiBase/health" -TimeoutSec 5
    Write-Host "Backend OK: $apiBase/health ($($health.status))" -ForegroundColor Green
}
catch {
    throw "Backend is not reachable at $apiBase/health. Start it from the repo root: dotnet run --project backend/src/Comink.Api (must listen on 0.0.0.0:5117 for LAN mode)."
}

if ($CheckOnly) {
    Write-Host 'Check-only finished.' -ForegroundColor Green
    return
}

if ($BuildApk) {
    flutter build apk --debug --dart-define-from-file=config/dev.json
    return
}

# --- Chạy app ---
flutter run -d $serial --dart-define-from-file=config/dev.json
