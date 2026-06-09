import { cn } from '@/lib/utils'

const TMDB_BASE = 'https://image.tmdb.org/t/p'

const posterSizes = {
  sm: 'w92',
  md: 'w185',
  lg: 'w342',
  xl: 'w500',
  original: 'original',
} as const

type PosterSize = keyof typeof posterSizes

interface PosterProps {
  path: string | null | undefined
  alt: string
  size?: PosterSize
  className?: string
  aspect?: '2/3' | '1/1' | '3/4'
}

const mediaIcons: Record<string, string> = {
  film: 'M7 4v16l6-5 6 5V4',
  tv_show: 'M4 7h16v10H4zM9 21h6M12 17v4',
  book: 'M4 19.5A2.5 2.5 0 016.5 17H20V4H6.5A2.5 2.5 0 004 6.5v13z',
  game: 'M6 12h4m-2-2v4m6 0h.01M18 12h.01',
  music: 'M9 18V5l12-2v13',
}

function resolveImageSrc(path: string, size: PosterSize): string {
  if (path.startsWith('http')) return path
  return `${TMDB_BASE}/${posterSizes[size]}${path}`
}

export function Poster({ path, alt, size = 'lg', className, aspect = '2/3' }: PosterProps) {
  const src = path ? resolveImageSrc(path, size) : null

  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-md bg-abyss',
        className,
      )}
      style={{ aspectRatio: aspect }}
    >
      {src ? (
        <img
          src={src}
          alt={alt}
          loading="lazy"
          className="absolute inset-0 h-full w-full object-cover"
        />
      ) : (
        <div className="absolute inset-0 flex items-center justify-center">
          <span className="text-[0.6875rem] font-medium text-text-muted text-center px-2 leading-tight">
            {alt}
          </span>
        </div>
      )}
    </div>
  )
}

export function PosterFallback({
  title,
  mediaType,
  className,
}: {
  title: string
  mediaType?: string
  className?: string
}) {
  const iconPath = mediaType ? mediaIcons[mediaType] : undefined

  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-md bg-abyss flex flex-col items-center justify-center gap-2',
        className,
      )}
      style={{ aspectRatio: '2/3' }}
    >
      {iconPath && (
        <svg viewBox="0 0 24 24" className="w-6 h-6 text-ash" fill="none" stroke="currentColor" strokeWidth={1.5}>
          <path d={iconPath} strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      )}
      <span className="text-[0.6875rem] font-medium text-text-muted text-center px-2 leading-tight line-clamp-3">
        {title}
      </span>
    </div>
  )
}
