/**
 * Schema kiểm tra dữ liệu ở client.
 * Ràng buộc phải khớp validator của backend (docs/05_api_contract.md mục 2 và 3);
 * kiểm tra ở client chỉ để phản hồi nhanh, backend vẫn là nơi chặn thật (AGENTS.md mục 9.4).
 */
import { z } from 'zod'

const userNamePattern = /^[A-Za-z0-9._\-@]+$/

export const registerSchema = z.object({
  userName: z
    .string()
    .min(3, 'validation.username_min')
    .max(32, 'validation.username_max')
    .regex(userNamePattern, 'validation.username_charset'),
  email: z.string().min(1, 'validation.email_required').max(256).email('validation.email_invalid'),
  displayName: z.string().min(1, 'validation.display_name_required').max(64, 'validation.display_name_max'),
  password: z
    .string()
    .min(8, 'validation.password_min')
    .regex(/[A-Z]/, 'validation.password_upper')
    .regex(/[a-z]/, 'validation.password_lower')
    .regex(/[0-9]/, 'validation.password_digit')
    .regex(/[^a-zA-Z0-9]/, 'validation.password_special'),
})

export const loginSchema = z.object({
  emailOrUserName: z.string().min(1, 'validation.identifier_required').max(256),
  password: z.string().min(1, 'validation.password_required'),
})

export const profileSchema = z.object({
  displayName: z
    .string()
    .min(1, 'validation.display_name_required')
    .max(64, 'validation.display_name_max'),
  bio: z.string().max(300, 'validation.bio_max').optional(),
  location: z.string().max(100, 'validation.location_max').optional(),
  // `<input type="date">` trả chuỗi rỗng khi chưa chọn.
  dateOfBirth: z
    .string()
    .optional()
    .refine((value) => !value || !Number.isNaN(Date.parse(value)), 'validation.date_invalid')
    .refine((value) => !value || Date.parse(value) <= Date.now(), 'validation.date_future'),
})

export type RegisterFormValues = z.infer<typeof registerSchema>
export type LoginFormValues = z.infer<typeof loginSchema>
export type ProfileFormValues = z.infer<typeof profileSchema>
