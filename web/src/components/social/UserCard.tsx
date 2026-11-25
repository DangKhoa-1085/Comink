import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import type { UserSummary } from '../../api/types'
import { Avatar } from './Avatar'
import { RelationshipActions } from './RelationshipActions'

/** Một dòng người dùng kèm nút quan hệ. */
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
    <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
      <Avatar name={user.displayName} url={user.avatarUrl} size="md" />
      <div className="min-w-0 flex-1">
        <Link to={`/users/${user.id}`} className="block truncate text-sm font-semibold hover:text-indigo-600">
          {user.displayName}
        </Link>
        <p className="truncate text-xs text-slate-500 dark:text-slate-400">@{user.userName}</p>
        {user.bio && <p className="truncate text-xs text-slate-500 dark:text-slate-400">{user.bio}</p>}
      </div>

      <RelationshipActions
        userId={user.id}
        relationship={user.relationship}
        incomingRequestId={incomingRequestId}
        onChange={onChange}
      />
      <span className="sr-only">{t('social.friend_add')}</span>
    </div>
  )
}
