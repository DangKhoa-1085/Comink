import { useEffect, useRef } from 'react'

/**
 * Gọi `onLoadMore` khi phần tử sentinel lọt vào khung nhìn — dùng cho "phân trang cuộn".
 * Không gọi khi đang tải hoặc đã hết dữ liệu.
 */
export function useInfiniteScroll(onLoadMore: () => void, canLoadMore: boolean) {
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  const callbackRef = useRef(onLoadMore)

  // Cập nhật ref trong effect (không gán giá trị ref lúc render — quy tắc của React Compiler).
  useEffect(() => {
    callbackRef.current = onLoadMore
  }, [onLoadMore])

  useEffect(() => {
    const element = sentinelRef.current
    if (!element || !canLoadMore) {
      return
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          callbackRef.current()
        }
      },
      { rootMargin: '200px' },
    )

    observer.observe(element)
    return () => observer.disconnect()
  }, [canLoadMore])

  return sentinelRef
}
