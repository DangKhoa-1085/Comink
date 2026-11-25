import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Globe2, Lock, MessageCircle, Pencil, Repeat2, Share2, ThumbsUp, Trash2, Users } from 'lucide-react'
import { commentsApi } from '../../api/comments'
import { postsApi } from '../../api/posts'
import type { Comment, Post, Visibility } from '../../api/types'
import { Button } from '../ui/Button'
import { Alert, Spinner } from '../ui/Feedback'
import { Avatar } from './Avatar'
import { timeAgo } from '../../lib/format'
import { toast } from '../../stores/toastStore'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'

const visibilityIcon = {
  Public: Globe2,
  Friends: Users,
  OnlyMe: Lock,
} as const

/** Hiển thị một bài viết kèm thích / bình luận / chia sẻ. */
export function PostCard({ post, onChange }: { post: Post; onChange: () => void }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [showComments, setShowComments] = useState(false)
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(post.content ?? '')
  const [draftVisibility, setDraftVisibility] = useState<Visibility>(post.visibility)

  const updateMutation = useMutation({
    mutationFn: () =>
      postsApi.update(post.id, { content: draft.trim() || null, visibility: draftVisibility }),
    onSuccess: () => {
      setEditing(false)
      toast.success(t('social.post_updated'))
      onChange()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const likeMutation = useMutation({
    mutationFn: () => (post.likedByMe ? postsApi.unlike(post.id) : postsApi.like(post.id)),
    onSuccess: onChange,
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const shareMutation = useMutation({
    mutationFn: () => postsApi.create({ sharedPostId: post.id, visibility: 'Friends' }),
    onSuccess: () => {
      toast.success(t('social.shared'))
      onChange()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const deleteMutation = useMutation({
    mutationFn: () => postsApi.remove(post.id),
    onSuccess: () => {
      toast.success(t('social.post_deleted'))
      onChange()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const VisibilityIcon = visibilityIcon[post.visibility]

  return (
    <article className="mb-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <header className="mb-3 flex items-start gap-3">
        <Avatar name={post.author.displayName} url={post.author.avatarUrl} size="md" />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-slate-800 dark:text-slate-100">
            {post.author.displayName}
          </p>
          <p className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
            <span className="truncate">@{post.author.userName}</span>
            <span aria-hidden="true">·</span>
            <span>{timeAgo(post.createdAt, i18n.language)}</span>
            <VisibilityIcon
              className="h-3 w-3"
              aria-label={t(`social.visibility_${post.visibility.toLowerCase()}`)}
            />
          </p>
        </div>
        {post.isMine && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              if (window.confirm(t('social.confirm_delete_post'))) {
                deleteMutation.mutate()
              }
            }}
            disabled={deleteMutation.isPending}
            aria-label="delete-post"
            title={t('social.delete_post')}
          >
            <Trash2 className="h-4 w-4 text-rose-500" />
          </Button>
        )}
      </header>

      {editing ? (
        <div className="mb-3">
          <textarea
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            rows={3}
            maxLength={5000}
            className="w-full resize-none rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm outline-none focus:border-indigo-400 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
          />
          <div className="mt-2 flex items-center gap-2">
            <select
              value={draftVisibility}
              onChange={(event) => setDraftVisibility(event.target.value as Visibility)}
              className="h-8 rounded-lg border border-slate-300 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200"
              aria-label={t('social.visibility')}
            >
              <option value="Public">{t('social.visibility_public')}</option>
              <option value="Friends">{t('social.visibility_friends')}</option>
              <option value="OnlyMe">{t('social.visibility_onlyme')}</option>
            </select>
            <Button size="sm" onClick={() => updateMutation.mutate()} disabled={updateMutation.isPending}>
              {updateMutation.isPending ? t('common.saving') : t('common.save')}
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setEditing(false)}>
              {t('common.cancel')}
            </Button>
          </div>
        </div>
      ) : (
        post.content && (
          <p className="mb-3 whitespace-pre-wrap break-words text-sm text-slate-700 dark:text-slate-200">
            {post.content}
          </p>
        )
      )}

      {post.media.length > 0 && (
        <div
          className={
            post.media.length === 1
              ? 'mb-3'
              : post.media.length === 2
                ? 'mb-3 grid grid-cols-2 gap-2'
                : 'mb-3 grid grid-cols-3 gap-2'
          }
        >
          {post.media.map((item) => (
            <img
              key={item.id}
              src={item.thumbnailUrl ?? item.url}
              alt=""
              className={
                post.media.length === 1
                  ? 'max-h-96 w-full rounded-xl object-cover'
                  : 'h-32 w-full rounded-lg object-cover'
              }
            />
          ))}
        </div>
      )}

      {post.sharedPost && (
        <div className="mb-3 rounded-xl border border-slate-200 p-3 dark:border-slate-700">
          <p className="mb-1 text-xs font-semibold text-slate-600 dark:text-slate-300">
            {post.sharedPost.author.displayName}
          </p>
          {post.sharedPost.content && (
            <p className="whitespace-pre-wrap break-words text-sm text-slate-600 dark:text-slate-300">
              {post.sharedPost.content}
            </p>
          )}
          {post.sharedPost.media.length > 0 && (
            <img
              src={post.sharedPost.media[0]?.thumbnailUrl ?? post.sharedPost.media[0]?.url}
              alt=""
              className="mt-2 max-h-64 w-full rounded-lg object-cover"
            />
          )}
        </div>
      )}

      <footer className="flex items-center gap-1 border-t border-slate-100 pt-2 text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => likeMutation.mutate()}
          disabled={likeMutation.isPending}
          className={post.likedByMe ? 'text-indigo-600 dark:text-indigo-400' : undefined}
        >
          <ThumbsUp className="h-4 w-4" />
          {post.likeCount}
        </Button>

        <Button variant="ghost" size="sm" onClick={() => setShowComments((value) => !value)}>
          <MessageCircle className="h-4 w-4" />
          {post.commentCount}
        </Button>

        <Button
          variant="ghost"
          size="sm"
          onClick={() => {
            if (window.confirm(t('social.confirm_share'))) {
              shareMutation.mutate()
            }
          }}
          disabled={shareMutation.isPending}
        >
          <Repeat2 className="h-4 w-4" />
          {post.shareCount}
        </Button>

        {post.isMine && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setDraft(post.content ?? '')
              setDraftVisibility(post.visibility)
              setEditing((value) => !value)
            }}
            aria-label="edit-post"
            title={t('social.edit_post')}
            className={editing ? 'text-indigo-600 dark:text-indigo-400' : undefined}
          >
            <Pencil className="h-4 w-4" />
          </Button>
        )}

        <span className="ml-auto flex items-center gap-1">
          <Share2 className="h-3 w-3" />
          {t(`social.visibility_${post.visibility.toLowerCase()}`)}
        </span>
      </footer>

      {showComments && (
        <CommentSection
          post={post}
          onChange={() => {
            queryClient.invalidateQueries({ queryKey: ['comments', post.id] })
            onChange()
          }}
        />
      )}
    </article>
  )
}

function CommentSection({ post, onChange }: { post: Post; onChange: () => void }) {
  const { t, i18n } = useTranslation()
  const [draft, setDraft] = useState('')
  const [replyTo, setReplyTo] = useState<Comment | null>(null)

  const commentsQuery = useQuery({
    queryKey: ['comments', post.id],
    queryFn: () => commentsApi.list(post.id),
  })

  const createMutation = useMutation({
    mutationFn: () => commentsApi.create(post.id, draft.trim(), replyTo?.id),
    onSuccess: () => {
      setDraft('')
      setReplyTo(null)
      toast.success(t('social.comment_added'))
      onChange()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  return (
    <div className="mt-3 border-t border-slate-100 pt-3 dark:border-slate-800">
      {commentsQuery.isLoading ? (
        <div className="flex items-center gap-2 text-xs text-slate-500">
          <Spinner /> {t('social.loading')}
        </div>
      ) : commentsQuery.isError ? (
        <Alert variant="error">{t('common.error')}</Alert>
      ) : (commentsQuery.data?.items.length ?? 0) === 0 ? (
        <p className="text-xs text-slate-400">{t('social.comments_empty')}</p>
      ) : (
        <ul className="space-y-3">
          {commentsQuery.data?.items.map((comment) => (
            <CommentItem
              key={comment.id}
              comment={comment}
              onChange={onChange}
              onReply={() => setReplyTo(comment)}
            />
          ))}
        </ul>
      )}

      <div className="mt-3 flex items-center gap-2">
        <input
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          placeholder={replyTo ? t('social.reply_to', { name: replyTo.author.displayName }) : t('social.comment_placeholder')}
          maxLength={2000}
          className="h-9 min-w-0 flex-1 rounded-lg border border-slate-200 bg-slate-50 px-3 text-sm outline-none focus:border-indigo-400 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
        />
        {replyTo && (
          <Button variant="ghost" size="sm" onClick={() => setReplyTo(null)}>
            {t('common.cancel')}
          </Button>
        )}
        <Button
          size="sm"
          onClick={() => draft.trim() && createMutation.mutate()}
          disabled={createMutation.isPending || draft.trim().length === 0}
        >
          {t('social.send')}
        </Button>
      </div>
      <p className="mt-1 text-[11px] text-slate-400">{timeAgo(post.createdAt, i18n.language)}</p>
    </div>
  )
}

function CommentItem({
  comment,
  onChange,
  onReply,
}: {
  comment: Comment
  onChange: () => void
  onReply: () => void
}) {
  const { t, i18n } = useTranslation()
  const [showReplies, setShowReplies] = useState(false)

  const repliesQuery = useQuery({
    queryKey: ['replies', comment.id],
    queryFn: () => commentsApi.replies(comment.id),
    enabled: showReplies,
  })

  const deleteMutation = useMutation({
    mutationFn: () => commentsApi.remove(comment.id),
    onSuccess: onChange,
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  return (
    <li className="text-sm">
      <div className="flex items-start gap-2">
        <Avatar name={comment.author.displayName} url={comment.author.avatarUrl} size="sm" />
        <div className="min-w-0 flex-1">
          <p className="text-xs font-semibold text-slate-700 dark:text-slate-200">
            {comment.author.displayName}
            <span className="ml-2 font-normal text-slate-400">{timeAgo(comment.createdAt, i18n.language)}</span>
          </p>
          <p className="whitespace-pre-wrap break-words text-slate-700 dark:text-slate-200">{comment.content}</p>
          <div className="mt-1 flex items-center gap-3 text-xs text-slate-500">
            <button type="button" className="hover:text-indigo-600" onClick={onReply}>
              {t('social.reply')}
            </button>
            <button type="button" className="hover:text-rose-600" onClick={() => deleteMutation.mutate()}>
              {t('social.delete_comment')}
            </button>
            {comment.replyCount > 0 && (
              <button type="button" className="hover:text-indigo-600" onClick={() => setShowReplies((v) => !v)}>
                {t('social.view_replies', { count: comment.replyCount })}
              </button>
            )}
          </div>

          {showReplies && (
            <ul className="mt-2 space-y-2 border-l border-slate-200 pl-3 dark:border-slate-700">
              {repliesQuery.isLoading ? (
                <li className="text-xs text-slate-400">{t('social.loading')}</li>
              ) : (
                repliesQuery.data?.items.map((reply) => (
                  <li key={reply.id}>
                    <p className="text-xs font-semibold text-slate-700 dark:text-slate-200">
                      {reply.author.displayName}
                    </p>
                    <p className="whitespace-pre-wrap break-words text-slate-700 dark:text-slate-200">
                      {reply.content}
                    </p>
                  </li>
                ))
              )}
            </ul>
          )}
        </div>
      </div>
    </li>
  )
}
