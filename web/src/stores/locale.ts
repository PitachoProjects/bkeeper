import { defineStore } from 'pinia'
import { ref } from 'vue'
import { i18n, preferredLocale, STORAGE_KEY, type Locale } from '@/i18n'

export const useLocaleStore = defineStore('locale', () => {
  const current = ref<Locale>(preferredLocale())

  function setLocale(next: Locale) {
    current.value = next
    localStorage.setItem(STORAGE_KEY, next)
    ;(i18n.global.locale as unknown as { value: Locale }).value = next
  }

  setLocale(current.value)

  return { current, setLocale }
})
