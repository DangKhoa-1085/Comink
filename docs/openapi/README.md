# Hợp đồng OpenAPI của Comink

`comink.v1.json` là **bản chụp hợp đồng API** dùng cho app Flutter (sinh client, kiểm tra DTO)
và làm mốc so sánh khi backend thay đổi. File được sinh tự động, **không sửa tay**.

## Thông tin

| Hạng mục | Giá trị |
|---|---|
| Nguồn | `Comink.Api` (Minimal API) |
| Định dạng | OpenAPI 3.1 (JSON) |
| Số endpoint hiện tại | **49** |
| Xác thực | `securitySchemes.Bearer`: HTTP bearer, JWT |
| Endpoint công khai | `/api/auth/login`, `/api/auth/register`, `/api/auth/refresh`, `/health`, webhook LiveKit (không có trong danh sách client) |
| Endpoint cần token | Có `security: [{ "Bearer": [] }]` trong operation tương ứng |

## Cách tái sinh

Chạy backend ở môi trường Development:

```powershell
cd backend
dotnet run --project src/Comink.Api
```

Sau đó (terminal khác):

```powershell
Invoke-WebRequest http://localhost:5117/openapi/v1.json -OutFile docs/openapi/comink.v1.json
# hoặc: curl.exe -s http://localhost:5117/openapi/v1.json -o docs/openapi/comink.v1.json
```

> Nếu đổi endpoint/DTO: cập nhật `docs/05_api_contract.md` **trước**, rồi code, rồi tái sinh file này.

## Dùng cho app Flutter

- Base URL khi dev:
  - Android **emulator**: `http://10.0.2.2:5117`
  - Thiết bị thật cùng LAN: `http://<ip-máy-chạy-backend>:5117` (cần mở firewall; gọi thoại/video cần HTTPS hoặc cờ Chrome — xem README mục 6d)
- Gửi token: `Authorization: Bearer <access_token>`.
- Access token hết hạn sau 15 phút; dùng `POST /api/auth/refresh` với refresh token đã lưu an toàn.
- SignalR hub `/hub/chat` truyền token qua query `access_token` (không phải header) — xem `docs/05_api_contract.md` mục 8.7.

## Kiểm tra "hợp đồng đủ ổn định để làm app" (Mốc A)

Tiêu chí Mốc A ghi *"Hợp đồng API (OpenAPI) đủ ổn định để bắt đầu làm app"*. Cụ thể hoá thành 7 điểm, trong đó **4 điểm kiểm tra tự động** bằng script:

```powershell
# Chỉ kiểm tra bản chụp + app (không cần backend)
powershell -ExecutionPolicy Bypass -File docs/openapi/verify.ps1

# Kiểm tra thêm: spec đang chạy có trùng bản chụp không (phát hiện drift)
powershell -ExecutionPolicy Bypass -File docs/openapi/verify.ps1 -Url http://localhost:5117/openapi/v1.json
```

| # | Tiêu chí | Cách kiểm tra |
|---|---|---|
| 1 | Bản chụp là OpenAPI **3.1** hợp lệ, sinh từ code (không sửa tay) | Script (mục 1) |
| 2 | Có **Bearer JWT** và endpoint cần đăng nhập đều khai báo `security` | Script (mục 2) — hiện 54/58 operation |
| 3 | **Mọi endpoint app Flutter gọi đều có trong hợp đồng** (không gọi "chui") | Script (mục 3) — hiện 37 endpoint của app |
| 4 | **Không drift**: spec đang chạy trùng bản chụp | Script (mục 4) — hiện 49 path khớp |
| 5 | DTO/response đúng thực tế: đã chạy thật các luồng chính (HTTP 12/12, app Chrome, app máy thật) | Thủ công — xem `docs/tasks/phase6_mobile_cb.md` mục 5 |
| 6 | Quy ước thay đổi: sửa API → cập nhật `docs/05_api_contract.md` → tái sinh bản chụp; **thay đổi breaking phải tăng `info.version`** | Quy trình (AGENTS mục 12) |
| 7 | Không còn endpoint mới dự kiến đổi trong 2 tuần tới | Quyết định của nhóm |

Script thoát code `1` khi có mục FAIL → dùng được trong CI (khi CI chạy trên nhánh tính năng — xem KI-12).

> **Lưu ý kỹ thuật:** `verify.ps1` chứa tiếng Việt và được lưu **UTF-8 có BOM** để Windows PowerShell 5.1 đọc đúng. Nếu sửa file mà mất BOM, lưu lại ở dạng UTF-8 có BOM (hoặc chạy bằng `pwsh` 7).

