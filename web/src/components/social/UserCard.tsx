import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import type { UserSummary } from '../../api/types'
import { Avatar } from './Avatar'
import { RelationshipActions } from './RelationshipActions'

/** Một thẻ người dùng kèm nút quan hệ, co giãn sạch trên cả Mobile lẫn Desktop. */
export function UserCard({
  user,
  incomingRequestId,
  onChange,
}: {
  user: UserSummary
  incomingRequestId?: string
  onChange?: () => void
}) {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col justify-between gap-3 overflow-hidden rounded-2xl border border-slate-200/90 bg-white p-4 shadow-xs dark:border-slate-800 dark:bg-[#121826]">
      <div className="flex min-w-0 items-start gap-3">
        <Avatar name={user.displayName} url={user.avatarUrl} size="md" />
        <div className="min-w-0 flex-1 space-y-0.5">
          <Link
            to={`/users/${user.id}`}
            className="block truncate text-sm font-bold text-slate-900 hover:text-sky-600 dark:text-slate-100 dark:hover:text-sky-400"
          >
            {user.displayName}
          </Link>
          <p className="truncate text-xs text-slate-500 dark:text-slate-400">@{user.userName}</p>
          {user.bio && (
            <p className="line-clamp-2 pt-0.5 text-xs leading-relaxed text-slate-500 dark:text-slate-400">
              {user.bio}
            </p>
          )}
        </div>
      </div>

      <div className="border-t border-slate-100 pt-2.5 dark:border-slate-800">
        <RelationshipActions
          userId={user.id}
          relationship={user.relationship}
          incomingRequestId={incomingRequestId}
          onChange={onChange}
        />
      </div>
      <span className="sr-only">{t('social.friend_add')}</span>
    </div>
  )
}
