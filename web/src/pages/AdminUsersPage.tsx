import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useInfiniteQuery } from '@tanstack/react-query'
import { Search, ShieldCheck, Users } from 'lucide-react'
import { adminApi } from '../api/admin'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Alert, Card, Spinner } from '../components/ui/Feedback'

const PAGE_SIZE = 10

/** Trang quản trị: danh sách tài khoản, tìm kiếm, phân trang con trỏ (nút "tải thêm"). */
export function AdminUsersPage() {
  const { t } = useTranslation()
  // `searchInput` là giá trị đang gõ; `search` là giá trị đã chốt để gọi API.
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useInfiniteQuery({
    queryKey: ['admin', 'users', search],
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }: { pageParam: string | undefined }) =>
      adminApi.listUsers({ cursor: pageParam, limit: PAGE_SIZE, search: search || undefined }),
    getNextPageParam: (lastPage) => (lastPage.hasMore ? (lastPage.nextCursor ?? undefined) : undefined),
  })

  const users = data?.pages.flatMap((page) => page.items) ?? []

  const statusLabel = (status: string) =>
    ({
      Active: t('admin.status_active'),
      Locked: t('admin.status_locked'),
      Deleted: t('admin.status_deleted'),
    })[status] ?? status

  const statusClass = (status: string) =>
    status === 'Active'
      ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300'
      : 'bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300'

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <ShieldCheck className="h-8 w-8 text-indigo-500" />
        <div>
          <h1 className="text-2xl font-bold">{t('admin.title')}</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">{t('admin.subtitle')}</p>
        </div>
      </div>

      <form
        className="flex gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          setSearch(searchInput.trim())
        }}
      >
        <Input
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder={t('admin.search_placeholder')}
          aria-label={t('common.search')}
        />
        <Button type="submit" variant="outline">
          <Search className="h-4 w-4" />
          {t('common.search')}
        </Button>
      </form>

      {isLoading && (
        <div className="flex items-center justify-center gap-2 py-12 text-slate-500">
          <Spinner className="h-5 w-5" />
          {t('common.loading')}
        </div>
      )}

      {isError && (
        <Alert variant="error">
          <p className="mb-2 font-medium">{t('common.error_title')}</p>
          <p className="mb-3">
            {translateErrorCode(t, error instanceof ApiError ? error.code : 'INTERNAL_ERROR')}
          </p>
          <Button variant="outline" size="sm" onClick={() => void refetch()}>
            {t('common.retry')}
          </Button>
        </Alert>
      )}

      {/* Trạng thái rỗng */}
      {!isLoading && !isError && users.length === 0 && (
        <Card className="flex flex-col items-center gap-2 py-12 text-center text-slate-500">
          <Users className="h-8 w-8 text-slate-300" />
          <p>{t('admin.empty')}</p>
        </Card>
      )}

      {users.length > 0 && (
        <Card className="overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-950/60">
                <tr>
                  <th className="px-4 py-3 font-medium">{t('admin.user')}</th>
                  <th className="px-4 py-3 font-medium">{t('admin.email')}</th>
                  <th className="px-4 py-3 font-medium">{t('admin.display_name')}</th>
                  <th className="px-4 py-3 font-medium">{t('admin.status')}</th>
                  <th className="px-4 py-3 font-medium">{t('admin.created_at')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {users.map((user) => (
                  <tr key={user.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/40">
                    <td className="px-4 py-3 font-medium">@{user.userName}</td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{user.email}</td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{user.displayName}</td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusClass(user.accountStatus)}`}>
                        {statusLabel(user.accountStatus)}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-slate-500">
                      {new Date(user.createdAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {users.length > 0 && (
        <div className="flex flex-col items-center gap-3">
          <p className="text-xs text-slate-500">{t('admin.total_shown', { count: users.length })}</p>
          {hasNextPage && (
            <Button
              variant="outline"
              onClick={() => void fetchNextPage()}
              disabled={isFetchingNextPage}
            >
              {isFetchingNextPage ? (
                <>
                  <Spinner />
                  {t('admin.loading_more')}
                </>
              ) : (
                t('admin.load_more')
              )}
            </Button>
          )}
        </div>
      )}
    </div>
  )
}
