<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { useLocaleStore } from '@/stores/locale'
import { SUPPORTED_LOCALES, type Locale } from '@/i18n'
import InfoTip from '@/components/InfoTip.vue'

interface RuleCatalogItem {
  code: string
  family: string
  description: string
  enabled: boolean
  cooldownDaysAmber: number
  cooldownDaysRed: number
}

const { t } = useI18n()
const locale = useLocaleStore()
const rules = ref<RuleCatalogItem[]>([])
const loading = ref(true)

const LOCALE_LABELS: Record<Locale, string> = { en: 'English', 'pt-PT': 'Português (PT)' }

async function load() {
  loading.value = true
  rules.value = await api.get<RuleCatalogItem[]>('/rules')
  loading.value = false
}

async function toggleRule(rule: RuleCatalogItem) {
  const next = !rule.enabled
  await api.put(`/rules/${rule.code}/enabled`, { enabled: next })
  rule.enabled = next
}

onMounted(load)
</script>

<template>
  <div>
    <h1>{{ t('settings.title') }}</h1>

    <section class="card">
      <h2>{{ t('settings.language') }}</h2>
      <p class="hint">{{ t('settings.languageHint') }}</p>
      <div class="locale-switch" role="group" aria-label="Language">
        <button v-for="l in SUPPORTED_LOCALES" :key="l" :class="{ active: locale.current === l }" @click="locale.setLocale(l)">
          {{ LOCALE_LABELS[l] }}
        </button>
      </div>
    </section>

    <section class="card">
      <h2>{{ t('settings.rules') }}</h2>
      <p class="hint">{{ t('settings.rulesHint') }}</p>
      <p v-if="loading">Loading…</p>
      <table v-else>
        <thead>
          <tr>
            <th>{{ t('settings.code') }}</th>
            <th>{{ t('settings.family') }}</th>
            <th>{{ t('settings.description') }}</th>
            <th>{{ t('settings.enabled') }}<InfoTip :text="t('settings.enabledHint')" /></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in rules" :key="r.code">
            <td>{{ r.code }}</td>
            <td>{{ r.family }}</td>
            <td>{{ r.description }}</td>
            <td>
              <label class="switch">
                <input type="checkbox" :checked="r.enabled" @change="toggleRule(r)" />
              </label>
            </td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>

<style scoped>
.card {
  padding: 1rem 1.25rem;
  margin-top: 1rem;
}
.hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin-top: -0.25rem;
}
.locale-switch {
  display: flex;
  gap: 0.5rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-sm);
  overflow: hidden;
  width: fit-content;
}
.locale-switch button {
  border: none;
  border-radius: 0;
  background: transparent;
  color: var(--color-text-muted);
  font-weight: 500;
  padding: 0.4rem 0.9rem;
}
.locale-switch button.active {
  background: var(--color-accent);
  color: var(--color-accent-contrast);
}
table {
  margin-top: 0.75rem;
}
</style>
