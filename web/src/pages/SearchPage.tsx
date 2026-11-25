import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { usersApi } from '../api/users'
import { UserCard } from '../components/social/UserCard'
import { Alert, Spinner } from '../components/ui/Feedback'
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
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-xl font-bold">{t('social.search')}</h1>

      <div className="mb-4 flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 dark:border-slate-800 dark:bg-slate-900">
        <Search className="h-4 w-4 text-slate-400" />
        <input
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          placeholder={t('social.search_placeholder')}
          maxLength={100}
          className="h-10 min-w-0 flex-1 bg-transparent text-sm outline-none dark:text-slate-100"
        />
        {searchQuery.isFetching && <Spinner />}
      </div>

      {debounced.length === 0 ? (
        <p className="text-sm text-slate-500 dark:text-slate-400">{t('social.search_hint')}</p>
      ) : searchQuery.isError ? (
        <Alert variant="error">{t('common.error_title')}</Alert>
      ) : searchQuery.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-8 text-sm text-slate-500">
          <Spinner /> {t('social.loading')}
        </div>
      ) : (searchQuery.data?.items.length ?? 0) === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500 dark:border-slate-700">
          {t('social.search_empty')}
        </p>
      ) : (
        <ul className="space-y-2">
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
