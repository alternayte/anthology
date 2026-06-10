import { useState } from 'react'
import { cn } from '@/lib/utils'

interface StarRatingProps {
  value: number | null
  onChange?: (rating: number) => void
  readonly?: boolean
  size?: 'sm' | 'md' | 'lg'
  accentClass?: string
}

const sizes = {
  sm: 'w-3.5 h-3.5',
  md: 'w-5 h-5',
  lg: 'w-6 h-6',
}

export function StarRating({
  value,
  onChange,
  readonly = false,
  size = 'md',
  accentClass = 'text-teal',
}: StarRatingProps) {
  const [hoverValue, setHoverValue] = useState<number | null>(null)
  const [justRated, setJustRated] = useState(false)
  const displayValue = hoverValue ?? value ?? 0
  const stars = 5
  const sizeClass = sizes[size]

  function handleClick(starIndex: number, isHalf: boolean) {
    if (readonly || !onChange) return
    const rating = isHalf ? starIndex * 2 - 1 : starIndex * 2
    onChange(rating)
    setJustRated(true)
    setTimeout(() => setJustRated(false), 200)
  }

  function handleMouseMove(starIndex: number, e: React.MouseEvent<HTMLSpanElement>) {
    if (readonly) return
    const rect = e.currentTarget.getBoundingClientRect()
    const isHalf = e.clientX - rect.left < rect.width / 2
    const val = isHalf ? starIndex * 2 - 1 : starIndex * 2
    setHoverValue(val)
  }

  return (
    <div
      className={cn(
        'inline-flex items-center gap-0.5',
        !readonly && 'cursor-pointer',
        justRated && 'animate-[pulse-rating_200ms_ease-out]',
      )}
      onMouseLeave={() => setHoverValue(null)}
      role={readonly ? 'img' : 'slider'}
      aria-label={`Rating: ${value ? value / 2 : 0} out of 5 stars`}
      aria-valuemin={readonly ? undefined : 0}
      aria-valuemax={readonly ? undefined : 10}
      aria-valuenow={readonly ? undefined : (value ?? 0)}
    >
      {Array.from({ length: stars }, (_, i) => {
        const starNumber = i + 1
        const fillAmount = Math.min(Math.max(displayValue - (starNumber - 1) * 2, 0), 2)
        const fillPercent = (fillAmount / 2) * 100

        return (
          <span
            key={starNumber}
            className={cn('relative inline-block', sizeClass)}
            onMouseMove={(e) => handleMouseMove(starNumber, e)}
            onClick={(e) => {
              const rect = e.currentTarget.getBoundingClientRect()
              const isHalf = e.clientX - rect.left < rect.width / 2
              handleClick(starNumber, isHalf)
            }}
            style={{ animationDelay: readonly ? undefined : `${i * 60}ms` }}
          >
            <svg viewBox="0 0 24 24" className={cn('absolute inset-0', sizeClass, 'text-ash')}>
              <path
                d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"
                fill="currentColor"
              />
            </svg>
            <svg
              viewBox="0 0 24 24"
              className={cn('absolute inset-0', sizeClass, accentClass)}
              style={{ clipPath: `inset(0 ${100 - fillPercent}% 0 0)` }}
            >
              <path
                d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"
                fill="currentColor"
              />
            </svg>
          </span>
        )
      })}
    </div>
  )
}
