import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function getErrorMessage(error: unknown): string {
  if (error && typeof error === 'object') {
    if ('detail' in error && typeof (error as Record<string, unknown>).detail === 'string')
      return (error as { detail: string }).detail
    if ('message' in error && typeof (error as Record<string, unknown>).message === 'string')
      return (error as { message: string }).message
  }
  return 'Something went wrong'
}
