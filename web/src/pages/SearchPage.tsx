import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { usersApi } from '../api/users'
import { UserCard } from '../components/social/UserCard'
import { Alert, Skeleton, Spinner } from '../components/ui/Feedback'
import { useIncomingRequestIds } from '../hooks/useIncomingRequestIds'

/** Tìm người dùng — gõ không dấu vẫn khớp (backend dùng unaccent + trigram). */
export function SearchPage() {
  const { t } = useTranslation()
  const [term, setTerm] = useState('')
  const [debounced, setDebounced] = useState('')
  const incomingRequests = useIncomingRequestIds()

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(term.trim()), 350)
    return () => clearTimeout(timer)
  }, [term])

  const searchQuery = useQuery({
    queryKey: ['search-users', debounced],
    queryFn: () => usersApi.search(debounced),
    enabled: debounced.length > 0,
  })

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-xl font-bold text-slate-900 dark:text-slate-100">
          {t('social.search')}
        </h1>
        <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
          {t('social.search_hint')}
        </p>
      </div>

      <div className="flex h-11 items-center gap-2.5 rounded-2xl border border-slate-200 bg-white px-3.5 shadow-xs focus-within:ring-2 focus-within:ring-sky-500 dark:border-slate-800 dark:bg-[#121826]">
        <Search className="h-4 w-4 shrink-0 text-slate-400" />
        <input
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          placeholder={t('social.search_placeholder')}
          maxLength={100}
          className="min-w-0 flex-1 bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400 dark:text-slate-100"
        />
        {searchQuery.isFetching && <Spinner className="text-sky-600 dark:text-sky-400" />}
      </div>

      {debounced.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-slate-300 bg-white/60 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-[#121826]/60 dark:text-slate-400">
          {t('social.search_hint')}
        </p>
      ) : searchQuery.isError ? (
        <Alert variant="error">{t('common.error_title')}</Alert>
      ) : searchQuery.isLoading ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {[1, 2, 3, 4].map((item) => (
            <div
              key={item}
              className="space-y-3 rounded-2xl border border-slate-200/80 bg-white p-4 dark:border-slate-800 dark:bg-[#121826]"
            >
              <div className="flex items-center gap-3">
                <Skeleton className="h-10 w-10 rounded-full" />
                <div className="flex-1 space-y-1.5">
                  <Skeleton className="h-3.5 w-28" />
                  <Skeleton className="h-3 w-20" />
                </div>
              </div>
              <Skeleton className="h-8 w-full" />
            </div>
          ))}
        </div>
      ) : (searchQuery.data?.items.length ?? 0) === 0 ? (
        <p className="rounded-2xl border border-dashed border-slate-300 bg-white/60 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-[#121826]/60 dark:text-slate-400">
          {t('social.search_empty')}
        </p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {searchQuery.data?.items.map((user) => (
            <li key={user.id}>
              <UserCard
                user={user}
                incomingRequestId={incomingRequests.get(user.id)}
                onChange={() => void searchQuery.refetch()}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
