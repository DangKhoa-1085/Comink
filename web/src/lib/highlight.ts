/**
 * Tách văn bản thành các đoạn khớp/không khớp từ khoá tìm kiếm để tô sáng.
 *
 * Tìm kiếm của backend bỏ dấu tiếng Việt (`hop` khớp `họp`), nên phần tô sáng cũng phải
 * so khớp **không dấu, không phân biệt hoa/thường** nhưng vẫn giữ nguyên văn bản gốc để hiển thị.
 */
export interface HighlightSegment {
  text: string
  match: boolean
}

/** Chuẩn hoá một ký tự: bỏ dấu tiếng Việt, hạ chữ thường, `đ` → `d`. */
function normalizeChar(char: string): string {
  return char
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/đ/g, 'd')
}

/** Chuẩn hoá cả chuỗi + giữ bảng ánh xạ vị trí chuẩn hoá → vị trí trong chuỗi gốc. */
function normalizeWithMap(value: string): { normalized: string; map: number[] } {
  let normalized = ''
  const map: number[] = []

  for (let index = 0; index < value.length; ) {
    const codePoint = value.codePointAt(index)
    if (codePoint === undefined) {
      break
    }

    const char = String.fromCodePoint(codePoint)
    for (const unit of normalizeChar(char)) {
      normalized += unit
      map.push(index)
    }
    index += char.length
  }

  return { normalized, map }
}

/** Chia `text` thành các đoạn; đoạn có `match = true` là phần khớp `query`. */
export function splitHighlight(text: string, query: string | null | undefined): HighlightSegment[] {
  const trimmed = (query ?? '').trim()
  if (!text || !trimmed) {
    return [{ text, match: false }]
  }

  const { normalized, map } = normalizeWithMap(text)
  const normalizedQuery = normalizeWithMap(trimmed).normalized
  if (!normalizedQuery) {
    return [{ text, match: false }]
  }

  const segments: HighlightSegment[] = []
  let cursor = 0
  let searchFrom = 0

  while (searchFrom <= normalized.length - normalizedQuery.length) {
    const found = normalized.indexOf(normalizedQuery, searchFrom)
    if (found < 0) {
      break
    }

    const startOriginal = map[found]
    const endNormalized = found + normalizedQuery.length
    const endOriginal = endNormalized < map.length ? map[endNormalized] : text.length

    if (startOriginal > cursor) {
      segments.push({ text: text.slice(cursor, startOriginal), match: false })
    }
    segments.push({ text: text.slice(startOriginal, endOriginal), match: true })
    cursor = endOriginal
    searchFrom = endNormalized
  }

  if (cursor < text.length) {
    segments.push({ text: text.slice(cursor), match: false })
  }

  return segments.length > 0 ? segments : [{ text, match: false }]
}
