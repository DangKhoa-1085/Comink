# Cập nhật IP cho LiveKit (TURN + node_ip) rồi khởi động lại container.
#
# Vì sao cần: LiveKit chạy trong Docker nên mặc định quảng bá IP container (172.18.x.x) mà
# máy khác không kết nối được; TURN cũng cần domain là IP thật. IP thay đổi mỗi khi đổi mạng
# (hoặc khi bật/tắt Tailscale) → chạy script này là xong.
#
# Cách dùng (từ thư mục gốc repo):
#   powershell -ExecutionPolicy Bypass -File infra/set-lan-ip.ps1              # IP LAN (Wi-Fi)
#   powershell -ExecutionPolicy Bypass -File infra/set-lan-ip.ps1 -Tailscale   # IP Tailscale
#   powershell -ExecutionPolicy Bypass -File infra/set-lan-ip.ps1 -Ip 100.64.0.1
#   powershell -ExecutionPolicy Bypass -File infra/set-lan-ip.ps1 -NoRestart
#
# Test 2 máy qua Tailscale (không phụ thuộc Wi-Fi trường/AP isolation):
#   1) Cài Tailscale trên cả hai máy, đăng nhập cùng một tài khoản.
#   2) Trên máy A: chạy script này với -Tailscale (node_ip = IP tailnet của máy A).
#   3) Máy B mở http://<ip-tailscale-A>:5173 (kèm cờ Chrome cho camera, xem README mục 6c).

param(
    [string]$Ip,
    [switch]$Tailscale,
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$livekitYaml = Join-Path $PSScriptRoot 'livekit.yaml'

if (-not (Test-Path $livekitYaml)) {
    throw "Không tìm thấy $livekitYaml"
}

# Tự dò IP Tailscale (interface có tên chứa "Tailscale", dải 100.64.0.0/10).
if (-not $Ip -and $Tailscale) {
    $Ip = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.InterfaceAlias -like '*Tailscale*' -and $_.IPAddress -like '100.*' } |
        Select-Object -First 1 -ExpandProperty IPAddress

    if (-not $Ip) {
        throw 'Không thấy IP Tailscale. Đã cài Tailscale và đăng nhập chưa? (tailscale status)'
    }
}

# Tự dò IP LAN: ưu tiên adapter đang có default gateway (Wi-Fi/Ethernet thật),
# bỏ qua adapter ảo (WSL, Hyper-V, VPN như Cloudflare WARP).
if (-not $Ip) {
    $candidates = Get-NetIPConfiguration |
        Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
        ForEach-Object {
            $_.IPv4Address | Where-Object { $_.IPAddress -notlike '169.254.*' } |
                Select-Object -ExpandProperty IPAddress
        }

    $Ip = @($candidates) | Select-Object -First 1
}

if (-not $Ip) {
    throw 'Không dò được IP. Truyền tham số -Ip <ip> hoặc -Tailscale.'
}

Write-Host "IP sẽ dùng cho LiveKit: $Ip"

# Đọc/ghi UTF-8 KHÔNG BOM bằng .NET để giữ nguyên tiếng Việt trong file yaml
# (PowerShell 5.1 dùng Get-Content/Set-Content sẽ làm hỏng dấu).
$utf8 = New-Object System.Text.UTF8Encoding($false)
$content = [System.IO.File]::ReadAllText($livekitYaml, $utf8)

$content = [regex]::Replace($content, '(?m)^(\s*domain:\s*).*$', "`${1}$Ip")
$content = [regex]::Replace($content, '(?m)^(\s*node_ip:\s*).*$', "`${1}$Ip")

[System.IO.File]::WriteAllText($livekitYaml, $content, $utf8)

Write-Host 'Đã cập nhật infra/livekit.yaml:'
Select-String -Path $livekitYaml -Pattern 'domain:|node_ip:' | ForEach-Object { "  " + $_.Line.Trim() }

if (-not $NoRestart) {
    Push-Location $root
    try {
        docker restart comink_livekit | Out-Null
        Start-Sleep -Seconds 6

        # `docker logs` ghi ra stderr; tạm hạ ErrorActionPreference để không bị coi là lỗi.
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $log = docker logs comink_livekit --tail 30 2>&1 | Select-String -Pattern 'starting LiveKit server'
        $ErrorActionPreference = $previous

        if ($log) {
            Write-Host 'LiveKit đã khởi động lại:'
            $log | Select-Object -Last 1 | ForEach-Object { "  " + $_.Line.Trim() }
        } else {
            Write-Warning 'Không thấy dòng "starting LiveKit server" — kiểm tra: docker logs comink_livekit'
        }
    } finally {
        Pop-Location
    }
}

Write-Host ''
Write-Host 'Lưu ý: nếu backend đang chạy, không cần khởi động lại backend — nó không lưu IP LAN.'
Write-Host ('URL để máy khác mở: http://' + $Ip + ':5173')
