import { createI18n } from 'vue-i18n'
import en from './en'
import vi from './vi'

export type Locale = 'vi' | 'en'

const STORAGE_KEY = 'locale'

export const getSavedLocale = (): Locale => {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (raw === 'en' || raw === 'vi') return raw
  return 'vi'
}

export const setSavedLocale = (locale: Locale) => {
  localStorage.setItem(STORAGE_KEY, locale)
}

export const i18n = createI18n({
  legacy: false,
  locale: getSavedLocale(),
  fallbackLocale: 'vi',
  messages: {
    en,
    vi,
  },
})
