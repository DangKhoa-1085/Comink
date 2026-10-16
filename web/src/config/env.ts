/**
 * Cấu hình môi trường.
 * Web dev gọi API qua proxy của Vite (mặc định `/api`), nên không cần URL tuyệt đối.
 */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''
