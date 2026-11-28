import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Camera,
  ChevronLeft,
  ChevronRight,
  Clock,
  Grid,
  Send,
  Sparkles,
  Trash2,
} from 'lucide-react'
import {
  LOCKET_ALLOWED_REACTIONS,
  locketsApi,
  type LocketMoment,
} from '../../api/lockets'
import { Avatar } from './Avatar'
import { LocketImageEditorModal } from './LocketImageEditorModal'
import { Button } from '../ui/Button'
import { Skeleton } from '../ui/Feedback'
import { timeAgo } from '../../lib/format'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'
import { toast } from '../../stores/toastStore'

interface FlyingEmoji {
  id: number
  emoji: string
  leftPercent: number
}

function formatRemaining(expiresAt: string, t: (key: string, opts?: Record<string, unknown>) => string): string {
  const diffMs = new Date(expiresAt).getTime() - Date.now()
  if (diffMs <= 0) {
    return t('locket.expires_soon')
  }
  const totalMinutes = Math.floor(diffMs / 60_000)
  const hours = Math.floor(totalMinutes / 60)
  if (hours >= 1) {
    return t('locket.remaining_hours', { hours })
  }
  return t('locket.remaining_minutes', { minutes: Math.max(1, totalMinutes) })
}

/**
 * Widget Comink Locket đặt trên Bảng tin: xem ảnh mới nhất (hạn 24h), lọc theo bạn bè,
 * xem lưới, thả cảm xúc bay, trả lời thẳng vào chat 1–1 và mở trình chụp/cắt ảnh 1:1.
 */
export function LocketWidget() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [mode, setMode] = useState<'live' | 'grid'>('live')
  const [editorOpen, setEditorOpen] = useState(false)
  const [selectedAuthorId, setSelectedAuthorId] = useState<string>('')
  const [activeIndex, setActiveIndex] = useState(0)
  const [replyText, setReplyText] = useState('')
  const [lastConversationId, setLastConversationId] = useState<string | null>(null)
  const [flyingEmojis, setFlyingEmojis] = useState<FlyingEmoji[]>([])

  const locketsQuery = useQuery({
    queryKey: ['lockets'],
    queryFn: () => locketsApi.list({ limit: 40 }),
    staleTime: 30_000,
  })

  const allMoments = useMemo(
    () => locketsQuery.data?.items ?? [],
    [locketsQuery.data?.items],
  )

  // Danh sách tác giả duy nhất để lọc nhanh ngay trong widget.
  const authorOptions = useMemo(() => {
    const map = new Map<string, string>()
    for (const item of allMoments) {
      if (!map.has(item.author.id)) {
        map.set(item.author.id, item.isMine ? t('locket.you') : item.author.displayName)
      }
    }
    return [...map.entries()].map(([id, name]) => ({ id, name }))
  }, [allMoments, t])

  const filteredMoments = useMemo(() => {
    if (!selectedAuthorId) {
      return allMoments
    }
    return allMoments.filter((item) => item.author.id === selectedAuthorId)
  }, [allMoments, selectedAuthorId])

  const safeIndex = activeIndex < filteredMoments.length ? activeIndex : 0
  const currentMoment: LocketMoment | undefined = filteredMoments[safeIndex]

  const refreshLockets = () => {
    void queryClient.invalidateQueries({ queryKey: ['lockets'], refetchType: 'all' })
  }

  const spawnFlyingEmoji = (emoji: string) => {
    const base = Date.now()
    const batch: FlyingEmoji[] = Array.from({ length: 4 }).map((_, index) => ({
      id: base + index,
      emoji,
      leftPercent: 25 + Math.random() * 50,
    }))
    setFlyingEmojis((current) => [...current, ...batch])
    setTimeout(() => {
      const ids = new Set(batch.map((item) => item.id))
      setFlyingEmojis((current) => current.filter((item) => !ids.has(item.id)))
    }, 950)
  }

  const reactMutation = useMutation({
    mutationFn: ({ momentId, emoji }: { momentId: string; emoji: string }) =>
      locketsApi.react(momentId, emoji),
    onSuccess: (_updated, variables) => {
      spawnFlyingEmoji(variables.emoji)
      refreshLockets()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const replyMutation = useMutation({
    mutationFn: ({ momentId, content }: { momentId: string; content: string }) =>
      locketsApi.reply(momentId, content),
    onSuccess: (res) => {
      setReplyText('')
      setLastConversationId(res.conversationId)
      toast.success(t('locket.reply_sent'))
      void queryClient.invalidateQueries({ queryKey: ['conversations'] })
      void queryClient.invalidateQueries({ queryKey: ['messages', res.conversationId] })
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (momentId: string) => locketsApi.remove(momentId),
    onSuccess: () => {
      toast.success(t('locket.deleted'))
      refreshLockets()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const goPrev = () => {
    if (filteredMoments.length <= 1) {
      return
    }
    setLastConversationId(null)
    setActiveIndex((safeIndex - 1 + filteredMoments.length) % filteredMoments.length)
  }

  const goNext = () => {
    if (filteredMoments.length <= 1) {
      return
    }
    setLastConversationId(null)
    setActiveIndex((safeIndex + 1) % filteredMoments.length)
  }

  return (
    <section className="space-y-3 rounded-2xl border border-slate-200/90 bg-white p-4 shadow-xs dark:border-slate-800 dark:bg-[#121826]">
      <div className="space-y-2">
        <div className="flex items-center justify-between gap-2">
          <div className="flex min-w-0 items-center gap-1.5">
            <span className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-md bg-gradient-to-tr from-sky-500 to-amber-400 text-white">
              <Sparkles className="h-3 w-3" />
            </span>
            <h2 className="whitespace-nowrap text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-200">
              {t('locket.title')}
            </h2>
          </div>
          <span className="shrink-0 rounded-full bg-amber-500/15 px-2 py-0.5 text-[10px] font-semibold text-amber-600 dark:text-amber-300">
            24h
          </span>
        </div>

        <div className="grid grid-cols-3 gap-1 rounded-xl border border-slate-200 bg-slate-50 p-1 text-[11px] dark:border-slate-700 dark:bg-[#1A2234]">
          <button
            type="button"
            onClick={() => setMode('live')}
            className={`whitespace-nowrap rounded-lg px-2 py-1 font-semibold transition ${
              mode === 'live'
                ? 'bg-sky-600 text-white'
                : 'text-slate-500 hover:text-slate-800 dark:text-slate-300'
            }`}
          >
            {t('locket.tab_live')}
          </button>
          <button
            type="button"
            onClick={() => setEditorOpen(true)}
            className="whitespace-nowrap rounded-lg px-2 py-1 font-semibold text-sky-600 transition hover:bg-sky-50 dark:text-sky-400 dark:hover:bg-sky-500/15"
          >
            {t('locket.tab_post')}
          </button>
          <button
            type="button"
            onClick={() => setMode('grid')}
            className={`inline-flex items-center justify-center gap-1 whitespace-nowrap rounded-lg px-2 py-1 font-semibold transition ${
              mode === 'grid'
                ? 'bg-sky-600 text-white'
                : 'text-slate-500 hover:text-slate-800 dark:text-slate-300'
            }`}
            title={t('locket.tab_grid')}
          >
            <Grid className="h-3 w-3 shrink-0" />
            <span>{t('locket.tab_grid')}</span>
          </button>
        </div>
      </div>

      {authorOptions.length > 1 && (
        <div className="flex gap-1.5 overflow-x-auto pb-0.5 text-[11px]">
          <button
            type="button"
            onClick={() => {
              setSelectedAuthorId('')
              setActiveIndex(0)
            }}
            className={`shrink-0 rounded-full px-2.5 py-0.5 font-medium transition ${
              selectedAuthorId === ''
                ? 'bg-sky-600 text-white'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300'
            }`}
          >
            {t('locket.filter_all')}
          </button>
          {authorOptions.map((author) => (
            <button
              key={author.id}
              type="button"
              onClick={() => {
                setSelectedAuthorId(author.id)
                setActiveIndex(0)
              }}
              className={`shrink-0 rounded-full px-2.5 py-0.5 font-medium transition ${
                selectedAuthorId === author.id
                  ? 'bg-sky-600 text-white'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300'
              }`}
            >
              {author.name}
            </button>
          ))}
        </div>
      )}

      {locketsQuery.isLoading ? (
        <Skeleton className="aspect-square w-full rounded-2xl" />
      ) : locketsQuery.isError ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50/70 p-3 text-center text-xs text-rose-600 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-300">
          <p className="mb-2">{t('locket.load_error')}</p>
          <Button variant="outline" size="sm" onClick={() => void locketsQuery.refetch()}>
            {t('common.retry')}
          </Button>
        </div>
      ) : filteredMoments.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-slate-300 bg-slate-50/70 p-5 text-center dark:border-slate-700 dark:bg-[#1A2234]/60">
          <span className="flex h-10 w-10 items-center justify-center rounded-full bg-sky-500/15 text-sky-500">
            <Camera className="h-5 w-5" />
          </span>
          <p className="text-xs font-semibold text-slate-700 dark:text-slate-200">
            {t('locket.empty_title')}
          </p>
          <p className="text-[11px] text-slate-500 dark:text-slate-400">
            {t('locket.empty_hint')}
          </p>
          <Button size="sm" onClick={() => setEditorOpen(true)}>
            <Camera className="h-3.5 w-3.5" />
            {t('locket.tab_post')}
          </Button>
        </div>
      ) : mode === 'grid' ? (
        <div className="grid grid-cols-3 gap-1.5">
          {filteredMoments.map((moment, idx) => (
            <button
              key={moment.id}
              type="button"
              onClick={() => {
                setActiveIndex(idx)
                setMode('live')
              }}
              className="group relative aspect-square overflow-hidden rounded-xl border border-slate-200 bg-slate-900 dark:border-slate-700"
            >
              <img
                src={moment.imageUrl}
                alt={moment.caption ?? moment.author.displayName}
                className="h-full w-full object-cover transition duration-200 group-hover:scale-105"
              />
              <span className="absolute inset-x-1 bottom-1 truncate rounded bg-black/55 px-1 py-0.5 text-[9px] font-medium text-white backdrop-blur-xs">
                {moment.isMine ? t('locket.you') : moment.author.displayName}
              </span>
            </button>
          ))}
        </div>
      ) : currentMoment ? (
        <div className="space-y-2.5">
          <div className="relative aspect-square w-full overflow-hidden rounded-2xl border border-slate-200 bg-[#080B14] dark:border-slate-700">
            <img
              src={currentMoment.imageUrl}
              alt={currentMoment.caption ?? currentMoment.author.displayName}
              className="h-full w-full object-cover"
            />

            {/* Thanh thông tin tác giả + thời hạn 24h + nút xóa của chủ sở hữu */}
            <div className="absolute inset-x-2 top-2 flex items-center justify-between gap-2">
              <div className="flex min-w-0 items-center gap-1.5 rounded-full bg-black/50 py-0.5 pl-1 pr-2.5 text-white backdrop-blur-sm">
                <Avatar
                  name={currentMoment.author.displayName}
                  url={currentMoment.author.avatarUrl}
                  size="sm"
                />
                <span className="truncate text-[11px] font-semibold">
                  {currentMoment.isMine ? t('locket.you') : currentMoment.author.displayName}
                </span>
                <span className="shrink-0 text-[10px] text-slate-200/80">
                  · {timeAgo(currentMoment.createdAt, i18n.language)}
                </span>
              </div>

              <div className="flex items-center gap-1">
                <span className="inline-flex items-center gap-0.5 rounded-full bg-black/50 px-2 py-0.5 text-[10px] font-medium text-amber-200 backdrop-blur-sm">
                  <Clock className="h-2.5 w-2.5" />
                  {formatRemaining(currentMoment.expiresAt, t)}
                </span>
                {currentMoment.isMine && (
                  <button
                    type="button"
                    onClick={() => {
                      if (window.confirm(t('locket.delete_confirm'))) {
                        deleteMutation.mutate(currentMoment.id)
                      }
                    }}
                    disabled={deleteMutation.isPending}
                    className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-black/55 text-rose-300 backdrop-blur-sm transition hover:bg-rose-600 hover:text-white"
                    title={t('social.delete_post')}
                    aria-label={t('social.delete_post')}
                  >
                    <Trash2 className="h-3 w-3" />
                  </button>
                )}
              </div>
            </div>

            {filteredMoments.length > 1 && (
              <>
                <button
                  type="button"
                  onClick={goPrev}
                  aria-label="prev-locket"
                  className="absolute left-1.5 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-full bg-black/50 text-white backdrop-blur-sm transition hover:bg-black/75"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
                <button
                  type="button"
                  onClick={goNext}
                  aria-label="next-locket"
                  className="absolute right-1.5 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-full bg-black/50 text-white backdrop-blur-sm transition hover:bg-black/75"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
              </>
            )}

            {/* Caption pill nổi ở đáy ảnh */}
            {currentMoment.caption && (
              <div className="pointer-events-none absolute inset-x-3 bottom-3 flex justify-center">
                <span className="max-w-full truncate rounded-full bg-black/60 px-3.5 py-1 text-xs font-medium text-white backdrop-blur-md">
                  {currentMoment.caption}
                </span>
              </div>
            )}

            {/* Lớp hiển thị emoji bay khi vừa thả cảm xúc */}
            <div className="pointer-events-none absolute inset-0 overflow-hidden">
              {flyingEmojis.map((item) => (
                <span
                  key={item.id}
                  style={{ left: `${item.leftPercent}%` }}
                  className="absolute bottom-8 animate-bounce text-xl drop-shadow"
                >
                  {item.emoji}
                </span>
              ))}
            </div>
          </div>

          {/* Hàng thả cảm xúc + chỉ số thứ tự ảnh */}
          <div className="flex items-center justify-between gap-1">
            <div className="flex flex-wrap items-center gap-1">
              {LOCKET_ALLOWED_REACTIONS.map((emoji) => {
                const summary = currentMoment.reactions.find((r) => r.emoji === emoji)
                const active = Boolean(summary?.reactedByMe)
                return (
                  <button
                    key={emoji}
                    type="button"
                    disabled={reactMutation.isPending}
                    onClick={() => reactMutation.mutate({ momentId: currentMoment.id, emoji })}
                    className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition hover:scale-105 ${
                      active
                        ? 'border-sky-400 bg-sky-50 text-sky-700 dark:border-sky-500/60 dark:bg-sky-500/20 dark:text-sky-200'
                        : 'border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-700 dark:bg-[#1A2234] dark:text-slate-200'
                    }`}
                  >
                    <span>{emoji}</span>
                    {summary && summary.count > 0 && (
                      <span className="text-[10px] font-semibold tabular-nums">
                        {summary.count}
                      </span>
                    )}
                  </button>
                )
              })}
            </div>

            {filteredMoments.length > 1 && (
              <span className="text-[10px] font-semibold text-slate-400 tabular-nums">
                {safeIndex + 1}/{filteredMoments.length}
              </span>
            )}
          </div>

          {/* Ô trả lời nhanh vào đoạn chat 1–1 (chỉ hiện với Locket của bạn bè) */}
          {!currentMoment.isMine && (
            <form
              onSubmit={(event) => {
                event.preventDefault()
                const content = replyText.trim()
                if (!content || replyMutation.isPending) {
                  return
                }
                replyMutation.mutate({ momentId: currentMoment.id, content })
              }}
              className="flex items-center gap-1.5"
            >
              <input
                type="text"
                maxLength={1000}
                value={replyText}
                onChange={(event) => setReplyText(event.target.value)}
                placeholder={t('locket.reply_placeholder')}
                className="h-8 min-w-0 flex-1 rounded-full border border-slate-200 bg-slate-50 px-3 text-xs outline-none focus:border-sky-400 dark:border-slate-700 dark:bg-[#1A2234] dark:text-slate-100"
              />
              <button
                type="submit"
                disabled={!replyText.trim() || replyMutation.isPending}
                className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-sky-600 text-white transition hover:bg-sky-500 disabled:opacity-50"
                aria-label={t('social.send')}
              >
                <Send className="h-3.5 w-3.5" />
              </button>
            </form>
          )}

          {lastConversationId && (
            <button
              type="button"
              onClick={() => navigate(`/messages/${lastConversationId}`)}
              className="text-[11px] font-semibold text-sky-600 hover:underline dark:text-sky-400"
            >
              → {t('locket.open_chat')}
            </button>
          )}
        </div>
      ) : null}

      <LocketImageEditorModal
        open={editorOpen}
        onClose={() => setEditorOpen(false)}
        onCreated={() => {
          setSelectedAuthorId('')
          setActiveIndex(0)
          setMode('live')
          refreshLockets()
        }}
      />
    </section>
  )
}
