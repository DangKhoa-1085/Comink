/**
 * Gọi API media — `/api/media` (docs/05_api_contract.md mục 7.6).
 *
 * - Có khoá Cloudinary: xin chữ ký rồi **tải trực tiếp** lên Cloudinary (không gửi token của Comink đi).
 * - Dev không có khoá: backend cấp endpoint `/api/media/local-upload`, tải tệp lên backend.
 */
import axios from 'axios'
import { http } from '../lib/http'
import type { LocalUploadResult, MediaAsset, MediaSignature, RegisterMediaPayload } from './types'

export const mediaApi = {
  async getSignature(context: 'Post' | 'Message' | 'Avatar' | 'Cover' | 'Album'): Promise<MediaSignature> {
    const { data } = await http.post<MediaSignature>('/api/media/signature', {
      context,
      resourceType: 'Image',
    })
    return data
  },

  /** Tải tệp lên nơi lưu trữ và trả về thông tin để đăng ký. */
  async upload(signature: MediaSignature, file: File): Promise<LocalUploadResult> {
    if (signature.provider === 'Cloudinary') {
      const form = new FormData()
      form.append('file', file)
      form.append('api_key', signature.apiKey)
      form.append('timestamp', String(signature.timestamp))
      form.append('signature', signature.signature)
      form.append('folder', signature.folder)
      if (signature.uploadPreset) {
        form.append('upload_preset', signature.uploadPreset)
      }

      // Dùng axios trực tiếp để KHÔNG gửi access token của Comink tới Cloudinary.
      const { data } = await axios.post<{
        public_id: string
        secure_url: string
        bytes: number
      }>(signature.uploadUrl, form)

      return {
        provider: 'Cloudinary',
        providerPublicId: data.public_id,
        url: data.secure_url,
        mimeType: file.type,
        sizeBytes: data.bytes,
      }
    }

    const form = new FormData()
    form.append('file', file)
    const { data } = await http.post<LocalUploadResult>(signature.uploadUrl, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async register(payload: RegisterMediaPayload): Promise<MediaAsset> {
    const { data } = await http.post<MediaAsset>('/api/media', payload)
    return data
  },
}
