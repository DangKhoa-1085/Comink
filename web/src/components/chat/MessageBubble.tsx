import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Check,
  Copy,
  CornerUpRight,
  MoreHorizontal,
  Pencil,
  Pin,
  PinOff,
  Reply,
  RotateCcw,
  SmilePlus,
} from 'lucide-react'
import type { ChatMessage } from '../../api/conversations'
import { timeAgo } from '../../lib/format'
import { REACTIONS } from '../../lib/reactions'

/** Cửa sổ sửa/thu hồi — phải khớp `MessageActionRules.ActionWindowHours` ở backend. */
const ACTION_WINDOW_MS = 24 * 60 * 60 * 1000

interface MessageBubbleProps {
  message: ChatMessage
  isMine: boolean
  /** Tin đang được ghim trong hội thoại (NC Ưu tiên 2). */
  isPinned: boolean
  /** Tên người gửi — chỉ hiện trong nhóm. */
  senderName?: string
  showSender: boolean
  /** "Đã gửi"/"Đã đọc"/"Đã xem bởi …" — chỉ truyền cho tin cuối của chính mình. */
  readStatus?: string
  readStatusTitle?: string
  highlighted: boolean
  language: string
  onReply: (message: ChatMessage) => void
  onEdit: (message: ChatMessage) => void
  onRecall: (message: ChatMessage) => void
  onReact: (message: ChatMessage, reaction: string) => void
  onJumpTo: (messageId: string) => void
  onPin: (message: ChatMessage) => void
  onUnpin: (message: ChatMessage) => void
  onForward: (message: ChatMessage) => void
}

/**
 * Một bong bóng tin nhắn kèm thao tác (NC Ưu tiên 2): trích dẫn phản hồi, nhãn chuyển tiếp,
 * cảm xúc, menu phản hồi/sao chép/sửa/thu hồi, trạng thái "đã chỉnh sửa"/"đã thu hồi".
 */
export function MessageBubble({
  message,
  isMine,
  isPinned,
  senderName,
  showSender,
  readStatus,
  readStatusTitle,
  highlighted,
  language,
  onReply,
  onEdit,
  onRecall,
  onReact,
  onJumpTo,
  onPin,
  onUnpin,
  onForward,
}: MessageBubbleProps) {
  const { t } = useTranslation()
  const [menuOpen, setMenuOpen] = useState(false)
  const [pickerOpen, setPickerOpen] = useState(false)
  const [copied, setCopied] = useState(false)
  // Thời điểm render lần đầu — không gọi Date.now() trực tiếp trong render (quy tắc purity).
  const [mountedAt] = useState(() => Date.now())
  const rootRef = useRef<HTMLLIElement | null>(null)

  const isDeleted = message.deletedAt !== null
  const withinWindow = mountedAt - Date.parse(message.createdAt) <= ACTION_WINDOW_MS
  const isUserContent = message.type !== 'System' && message.type !== 'Call'
  const canModify = isMine && !isDeleted && withinWindow && isUserContent
  const canInteract = !isDeleted && message.type !== 'System'
  const canCopy = !isDeleted && (message.content ?? '').length > 0
  const replyTo = message.replyTo

  // Đóng menu khi bấm ra ngoài hoặc nhấn Esc.
  useEffect(() => {
    if (!menuOpen) {
      return
    }

    const onPointerDown = (event: MouseEvent | TouchEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setMenuOpen(false)
        setPickerOpen(false)
      }
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuOpen(false)
        setPickerOpen(false)
      }
    }

    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('touchstart', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('touchstart', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [menuOpen])

  const closeMenu = () => {
    setMenuOpen(false)
    setPickerOpen(false)
  }

  const copyContent = async () => {
    if (!message.content) {
      return
    }

    try {
      await navigator.clipboard.writeText(message.content)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      // Trình duyệt chặn clipboard (không phải HTTPS): bỏ qua, người dùng vẫn chọn được văn bản.
    }
    closeMenu()
  }

  const bubbleTone = isMine
    ? 'rounded-2xl rounded-br-sm bg-indigo-600 px-3 py-2 text-sm text-white'
    : 'rounded-2xl rounded-bl-sm bg-slate-100 px-3 py-2 text-sm text-slate-800 dark:bg-slate-800 dark:text-slate-100'

  return (
    <li
      ref={rootRef}
      id={`message-${message.id}`}
      className={isMine ? 'flex justify-end' : 'flex justify-start'}
    >
      <div className={isMine ? 'flex max-w-[75%] flex-col items-end' : 'flex max-w-[75%] flex-col items-start'}>
        {!isMine && showSender && senderName && (
          <span className="mb-0.5 text-[11px] font-medium text-slate-500 dark:text-slate-400">
            {senderName}
          </span>
        )}

        <div className={isMine ? 'flex flex-row-reverse items-end gap-1' : 'flex items-end gap-1'}>
          <div
            className={`${bubbleTone} transition-shadow ${
              highlighted ? 'ring-2 ring-amber-400 ring-offset-2 dark:ring-offset-slate-900' : ''
            }`}
          >
            {replyTo && (
              <button
                type="button"
                onClick={() => {
                  if (!replyTo.isDeleted) {
                    onJumpTo(replyTo.messageId)
                  }
                }}
                disabled={replyTo.isDeleted}
                className={
                  isMine
                    ? 'mb-1 block w-full rounded-lg border-l-2 border-white/60 bg-white/10 px-2 py-1 text-left text-[11px] text-white/90 disabled:cursor-default'
                    : 'mb-1 block w-full rounded-lg border-l-2 border-indigo-400 bg-white/70 px-2 py-1 text-left text-[11px] text-slate-600 dark:bg-slate-900/40 dark:text-slate-300'
                }
              >
                <span className="block truncate font-semibold">{replyTo.senderDisplayName}</span>
                <span className="line-clamp-2">
                  {replyTo.isDeleted
                    ? t('chat.recalled')
                    : replyTo.contentPreview || t('chat.image_preview')}
                </span>
              </button>
            )}

            {message.forwardedFrom && (
              <p
                className={`mb-1 flex items-center gap-1 text-[11px] italic ${
                  isMine ? 'text-indigo-100' : 'text-slate-500 dark:text-slate-400'
                }`}
              >
                <CornerUpRight className="h-3 w-3 shrink-0" />
                {t('chat.forwarded_from', { name: message.forwardedFrom.senderDisplayName })}
              </p>
            )}

            {isDeleted ? (
              <p className={`italic ${isMine ? 'text-indigo-100' : 'text-slate-400'}`}>
                {t('chat.recalled')}
              </p>
            ) : (
              <>
                {message.attachments.length > 0 && (
                  <div className="mb-1 space-y-1">
                    {message.attachments.map((attachment) => (
                      <img
                        key={attachment.mediaId}
                        src={attachment.thumbnailUrl ?? attachment.url}
                        alt=""
                        className="max-h-60 rounded-lg object-cover"
                      />
                    ))}
                  </div>
                )}
                {message.content && (
                  <p className="whitespace-pre-wrap break-words">{message.content}</p>
                )}
              </>
            )}

            <span
              className={
                isMine ? 'mt-1 block text-[10px] text-indigo-200' : 'mt-1 block text-[10px] text-slate-400'
              }
            >
              {timeAgo(message.createdAt, language)}
              {message.editedAt && !isDeleted ? ` · ${t('chat.edited')}` : ''}
            </span>
          </div>

          <div className="relative">
            {canInteract && (
              <button
                type="button"
                onClick={() => {
                  setMenuOpen((open) => !open)
                  setPickerOpen(false)
                }}
                aria-label={t('chat.message_actions')}
                aria-haspopup="menu"
                aria-expanded={menuOpen}
                className="rounded-full p-1 text-slate-400 opacity-60 transition hover:bg-slate-100 hover:text-slate-600 focus:opacity-100 dark:hover:bg-slate-800 dark:hover:text-slate-200"
              >
                <MoreHorizontal className="h-4 w-4" />
              </button>
            )}

            {menuOpen && canInteract && (
              <div
                role="menu"
                className={`absolute bottom-full z-30 mb-1 w-48 overflow-hidden rounded-xl border border-slate-200 bg-white p-1 text-sm text-slate-700 shadow-lg dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 ${
                  isMine ? 'right-0' : 'left-0'
                }`}
              >
                {pickerOpen ? (
                  <div className="flex items-center justify-between gap-0.5 px-1 py-1">
                    {REACTIONS.map((reaction) => (
                      <button
                        key={reaction.key}
                        type="button"
                        onClick={() => {
                          onReact(message, reaction.key)
                          closeMenu()
                        }}
                        aria-label={t(`chat.reaction_${reaction.key}`)}
                        className="rounded-full p-1 text-lg transition hover:scale-110 hover:bg-slate-100 dark:hover:bg-slate-800"
                      >
                        {reaction.emoji}
                      </button>
                    ))}
                  </div>
                ) : (
                  <>
                    <MenuItem
                      icon={<Reply className="h-4 w-4" />}
                      label={t('chat.reply')}
                      onClick={() => {
                        onReply(message)
                        closeMenu()
                      }}
                    />
                    <MenuItem
                      icon={<SmilePlus className="h-4 w-4" />}
                      label={t('chat.react')}
                      onClick={() => setPickerOpen(true)}
                    />
                    <MenuItem
                      icon={<CornerUpRight className="h-4 w-4" />}
                      label={t('chat.forward')}
                      onClick={() => {
                        onForward(message)
                        closeMenu()
                      }}
                    />
                    <MenuItem
                      icon={isPinned ? <PinOff className="h-4 w-4" /> : <Pin className="h-4 w-4" />}
                      label={isPinned ? t('chat.unpin') : t('chat.pin')}
                      onClick={() => {
                        if (isPinned) {
                          onUnpin(message)
                        } else {
                          onPin(message)
                        }
                        closeMenu()
                      }}
                    />
                    {canCopy && (
                      <MenuItem
                        icon={copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                        label={copied ? t('chat.copied') : t('chat.copy')}
                        onClick={() => void copyContent()}
                      />
                    )}
                    {canModify && (
                      <MenuItem
                        icon={<Pencil className="h-4 w-4" />}
                        label={t('chat.edit')}
                        onClick={() => {
                          onEdit(message)
                          closeMenu()
                        }}
                      />
                    )}
                    {canModify && (
                      <MenuItem
                        icon={<RotateCcw className="h-4 w-4" />}
                        label={t('chat.recall')}
                        onClick={() => {
                          onRecall(message)
                          closeMenu()
                        }}
                        danger
                      />
                    )}
                  </>
                )}
              </div>
            )}
          </div>
        </div>

        {!isDeleted && message.reactions.length > 0 && (
          <div className="mt-1 flex flex-wrap gap-1">
            {message.reactions.map((reaction) => (
              <button
                key={reaction.reaction}
                type="button"
                onClick={() => onReact(message, reaction.reaction)}
                aria-pressed={reaction.reactedByMe}
                className={
                  reaction.reactedByMe
                    ? 'flex items-center gap-0.5 rounded-full border border-indigo-300 bg-indigo-50 px-2 py-0.5 text-[11px] text-indigo-700 dark:border-indigo-700 dark:bg-indigo-950 dark:text-indigo-200'
                    : 'flex items-center gap-0.5 rounded-full border border-slate-200 bg-white px-2 py-0.5 text-[11px] text-slate-600 hover:border-slate-300 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300'
                }
              >
                <span>{reaction.emoji}</span>
                <span>{reaction.count}</span>
              </button>
            ))}
          </div>
        )}

        {readStatus && (
          <span
            className="mt-0.5 block max-w-full truncate text-[10px] text-slate-400"
            title={readStatusTitle}
          >
            {readStatus}
          </span>
        )}
      </div>
    </li>
  )
}

interface MenuItemProps {
  icon: ReactNode
  label: string
  onClick: () => void
  danger?: boolean
}

function MenuItem({ icon, label, onClick, danger = false }: MenuItemProps) {
  return (
    <button
      type="button"
      role="menuitem"
      onClick={onClick}
      className={`flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition ${
        danger
          ? 'text-rose-600 hover:bg-rose-50 dark:text-rose-400 dark:hover:bg-rose-950'
          : 'hover:bg-slate-100 dark:hover:bg-slate-800'
      }`}
    >
      {icon}
      {label}
    </button>
  )
}
