import { useState } from 'react'
import { cn } from '@/lib/utils'

const TMDB_BASE = 'https://image.tmdb.org/t/p'

function resolveSrc(path: string, size: string): string {
  if (path.startsWith('http')) return path
  return `${TMDB_BASE}/${size}${path}`
}

export function BackdropHero({
  backdropPath,
  posterPath,
}: {
  backdropPath: string | null | undefined
  posterPath: string | null | undefined
}) {
  const [loaded, setLoaded] = useState(false)
  const src = backdropPath
    ? resolveSrc(backdropPath, 'w1280')
    : posterPath
      ? resolveSrc(posterPath, 'w780')
      : null

  if (!src) {
    return <div className="absolute inset-x-0 top-0 h-[420px] bg-abyss" aria-hidden />
  }

  return (
    <div className="absolute inset-x-0 top-0 h-[420px] overflow-hidden" aria-hidden>
      <img
        src={src}
        alt=""
        onLoad={() => setLoaded(true)}
        className={cn(
          'h-full w-full object-cover object-top transition-opacity duration-500',
          loaded ? 'opacity-100' : 'opacity-0',
          !backdropPath && 'scale-110 blur-2xl brightness-[0.55] saturate-[1.15]',
        )}
      />
      <div className="absolute inset-0 bg-gradient-to-b from-void/40 via-void/65 to-void" />
      <div className="absolute inset-0 bg-gradient-to-r from-void/45 to-transparent" />
    </div>
  )
}
