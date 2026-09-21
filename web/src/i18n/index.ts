import { createI18n } from 'vue-i18n'
import en from './locales/en.json'
import ptPT from './locales/pt-PT.json'

export const STORAGE_KEY = 'bkeeper.locale'
export const SUPPORTED_LOCALES = ['en', 'pt-PT'] as const
export type Locale = (typeof SUPPORTED_LOCALES)[number]

export function preferredLocale(): Locale {
  const stored = localStorage.getItem(STORAGE_KEY)
  if ((SUPPORTED_LOCALES as readonly string[]).includes(stored ?? '')) return stored as Locale
  return navigator.language.startsWith('pt') ? 'pt-PT' : 'en'
}

export const i18n = createI18n({
  legacy: false,
  locale: preferredLocale(),
  fallbackLocale: 'en',
  messages: { en, 'pt-PT': ptPT },
})
