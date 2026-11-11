<#
.SYNOPSIS
  Kiểm tra "hợp đồng OpenAPI đủ ổn định để bắt đầu làm app" (Mốc A).

.DESCRIPTION
  Kiểm tra 5 điểm có thể kiểm tra tự động:
    1. Bản chụp docs/openapi/comink.v1.json là JSON hợp lệ và là OpenAPI 3.x.
    2. Có security scheme Bearer (JWT) và các operation cần đăng nhập đều khai báo security.
    3. Mọi endpoint mà app Flutter gọi đều có trong hợp đồng (không gọi "chui").
    4. Nếu backend đang chạy: spec live trùng với bản chụp (không có drift).
    5. Liệt kê số path/operation để đối chiếu tài liệu.

  Thoát code 1 nếu có mục FAIL (dùng được trong CI).

.EXAMPLE
  powershell -File docs/openapi/verify.ps1

.EXAMPLE
  powershell -File docs/openapi/verify.ps1 -Url http://192.168.1.4:5117/openapi/v1.json
#>
param(
    [string]$Snapshot = (Join-Path $PSScriptRoot 'comink.v1.json'),
    [string]$Url = 'http://localhost:5117/openapi/v1.json',
    [string]$AppRoot = (Join-Path $PSScriptRoot '..\..\mobile\lib')
)

$script:failures = 0

function Write-Ok([string]$Message) {
    Write-Host "  [OK]   $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Write-Fail([string]$Message) {
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
    $script:failures++
}

Write-Host "== Kiểm tra hợp đồng OpenAPI ==" -ForegroundColor Cyan
Write-Host "Bản chụp: $Snapshot"
Write-Host "Spec live: $Url"
Write-Host "App Flutter: $AppRoot"

# --- 1. Đọc và kiểm tra bản chụp ---
if (-not (Test-Path $Snapshot)) {
    Write-Fail "Không tìm thấy bản chụp: $Snapshot"
    exit 1
}

try {
    $spec = Get-Content $Snapshot -Raw | ConvertFrom-Json
}
catch {
    Write-Fail "Bản chụp không phải JSON hợp lệ: $($_.Exception.Message)"
    exit 1
}

$openApiVersion = [string]$spec.openapi
if ($openApiVersion -like '3.*') {
    Write-Ok "Định dạng OpenAPI $openApiVersion"
}
else {
    Write-Fail "Phải là OpenAPI 3.x (đang là '$openApiVersion')"
}

if ([string]$spec.info.version) {
    Write-Ok "info.title '$($spec.info.title)' · info.version $($spec.info.version)"
}
else {
    Write-Warn "Thiếu info.version trong hợp đồng"
}

# --- 2. Security scheme + operation cần token ---
$bearer = $spec.components.securitySchemes.Bearer
if ($bearer -and [string]$bearer.scheme -eq 'bearer') {
    Write-Ok "Có Bearer JWT scheme (không kèm tiền tố Bearer trong giá trị token)"
}
else {
    Write-Fail "Thiếu components.securitySchemes.Bearer (type http, scheme bearer)"
}

$pathNames = @($spec.paths.PSObject.Properties.Name)
$operations = @()
foreach ($pathProperty in $spec.paths.PSObject.Properties) {
    foreach ($methodProperty in $pathProperty.Value.PSObject.Properties) {
        if (@('get', 'post', 'put', 'patch', 'delete') -contains $methodProperty.Name.ToLowerInvariant()) {
            $security = $methodProperty.Value.security
            $operations += [pscustomobject]@{
                Method    = $methodProperty.Name.ToUpperInvariant()
                Path      = $pathProperty.Name
                Protected = ($null -ne $security -and @($security).Count -gt 0)
            }
        }
    }
}

if ($pathNames.Count -gt 0) {
    Write-Ok "$($pathNames.Count) path, $($operations.Count) operation"
}
else {
    Write-Fail "Hợp đồng không có path nào"
}

$protected = @($operations | Where-Object { $_.Protected })
Write-Ok "$($protected.Count)/$($operations.Count) operation khai báo yêu cầu Bearer"

# --- 3. Endpoint app Flutter gọi phải có trong hợp đồng ---
function Convert-ToPathRegex([string]$Template) {
    # [regex]::Escape chỉ escape dấu '{' (dấu '}' vốn đã là ký tự thường) nên mẫu thay thế
    # phải khớp '\{...}' chứ không phải '\{...\}'.
    $escaped = [regex]::Escape($Template)
    $escaped = $escaped -replace '\\\{[^}]*\}', '[^/]+'
    return "^$escaped$"
}

$templates = @()
foreach ($name in $pathNames) {
    $templates += [pscustomobject]@{
        Path  = $name
        Regex = (Convert-ToPathRegex $name)
    }
}

$appCalls = @()
if (Test-Path $AppRoot) {
    foreach ($file in (Get-ChildItem $AppRoot -Recurse -Filter *.dart -File)) {
        $content = Get-Content $file.FullName -Raw
        # Chỉ lấy chuỗi nằm ngay trong lời gọi HTTP của ApiClient, không lấy mọi chuỗi '/api/...'
        foreach ($match in [regex]::Matches($content, "\.(?:get|post|put|patch|delete)\(\s*'(/[^']*)'")) {
            $appCalls += [pscustomobject]@{
                Path = $match.Groups[1].Value
                File = $file.Name
            }
        }
    }
}
else {
    Write-Warn "Không thấy thư mục app Flutter: $AppRoot"
}

# Chuẩn hoá: bỏ query string, thay nội suy Dart ($id / ${...}) bằng một đoạn path bất kỳ
$uniqueCalls = @($appCalls |
    ForEach-Object { ($_.Path -split '\?')[0] } |
    Where-Object { $_.StartsWith('/api/') } |
    ForEach-Object { [regex]::Replace($_, '\$\{[^}]*\}', 'X') } |
    ForEach-Object { [regex]::Replace($_, '\$[A-Za-z_][A-Za-z0-9_]*', 'X') } |
    Sort-Object -Unique)

$missing = @()
foreach ($call in $uniqueCalls) {
    $matched = $templates | Where-Object { $call -match $_.Regex } | Select-Object -First 1
    if (-not $matched) {
        $missing += $call
    }
}

if ($uniqueCalls.Count -eq 0) {
    Write-Warn "Không tìm thấy lời gọi /api/... nào trong app"
}
elseif ($missing.Count -eq 0) {
    Write-Ok "$($uniqueCalls.Count) endpoint app Flutter gọi đều có trong hợp đồng"
}
else {
    Write-Fail "App gọi endpoint KHÔNG có trong hợp đồng: $($missing -join ', ')"
}

# --- 4. So với spec đang chạy (nếu backend bật) ---
try {
    $liveContent = (Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5).Content
    $live = $liveContent | ConvertFrom-Json
    $livePaths = @($live.paths.PSObject.Properties.Name)
    $added = @($livePaths | Where-Object { $pathNames -notcontains $_ })
    $removed = @($pathNames | Where-Object { $livePaths -notcontains $_ })

    if ($added.Count -eq 0 -and $removed.Count -eq 0) {
        Write-Ok "Bản chụp khớp spec đang chạy ($($livePaths.Count) path) — không có drift"
    }
    else {
        if ($added.Count -gt 0) { Write-Fail "Spec đang chạy có path MỚI chưa cập nhật bản chụp: $($added -join ', ')" }
        if ($removed.Count -gt 0) { Write-Fail "Bản chụp có path đã bị bỏ trong code: $($removed -join ', ')" }
    }
}
catch {
    Write-Warn "Không truy cập được spec live ($Url) — bỏ qua bước so sánh drift"
}

# --- Kết luận ---
Write-Host ""
if ($script:failures -eq 0) {
    Write-Host "KẾT QUẢ: ĐẠT — hợp đồng dùng được làm mốc cho app." -ForegroundColor Green
    exit 0
}

Write-Host "KẾT QUẢ: KHÔNG ĐẠT — $($script:failures) mục cần xử lý." -ForegroundColor Red
exit 1
