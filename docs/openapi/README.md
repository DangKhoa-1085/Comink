# Hợp đồng OpenAPI của Comink

`comink.v1.json` là **bản chụp hợp đồng API** dùng cho app Flutter (sinh client, kiểm tra DTO)
và làm mốc so sánh khi backend thay đổi. File được sinh tự động, **không sửa tay**.

## Thông tin

| Hạng mục | Giá trị |
|---|---|
| Nguồn | `Comink.Api` (Minimal API) |
| Định dạng | OpenAPI 3.1 (JSON) |
| Số endpoint hiện tại | **48** |
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
