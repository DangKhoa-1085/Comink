import { useQuery } from '@tanstack/react-query'
import { friendshipsApi } from '../api/friendships'

/**
 * Bản đồ `userId → id lời mời` đang chờ tôi phản hồi.
 * Kết quả tìm kiếm không chứa id lời mời, nên cần bản đồ này để bấm "Chấp nhận" được.
 */
export function useIncomingRequestIds() {
  const query = useQuery({
    queryKey: ['friendships', 'incoming'],
    queryFn: () => friendshipsApi.incoming(),
    staleTime: 15_000,
  })

  const map = new Map<string, string>()
  for (const friendship of query.data?.items ?? []) {
    map.set(friendship.user.id, friendship.id)
  }

  return map
}
