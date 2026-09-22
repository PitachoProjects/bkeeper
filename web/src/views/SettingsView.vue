<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { useLocaleStore } from '@/stores/locale'
import { useAuthStore } from '@/stores/auth'
import { SUPPORTED_LOCALES, type Locale } from '@/i18n'
import InfoTip from '@/components/InfoTip.vue'

interface RuleCatalogItem {
  code: string
  family: string
  description: string
  enabled: boolean
  toggleable: boolean
  cooldownDaysAmber: number
  cooldownDaysRed: number
}

interface HealthScoreConfig {
  version: number
  attendanceWeight: number
  consistencyWeight: number
  bookingBehaviourWeight: number
  progressWeight: number
  engagementWeight: number
  attendanceEnabled: boolean
  consistencyEnabled: boolean
  bookingBehaviourEnabled: boolean
  progressEnabled: boolean
  engagementEnabled: boolean
  minTenureDays: number
  minSessions: number
}

const HEALTH_SCORE_FACTORS = [
  { key: 'attendance', weight: 'attendanceWeight', enabled: 'attendanceEnabled' },
  { key: 'consistency', weight: 'consistencyWeight', enabled: 'consistencyEnabled' },
  { key: 'bookingBehaviour', weight: 'bookingBehaviourWeight', enabled: 'bookingBehaviourEnabled' },
  { key: 'progress', weight: 'progressWeight', enabled: 'progressEnabled' },
  { key: 'engagement', weight: 'engagementWeight', enabled: 'engagementEnabled' },
] as const

const { t } = useI18n()
const locale = useLocaleStore()
const auth = useAuthStore()
const canManageHealthScore = computed(() => auth.role === 'Manager' || auth.role === 'Owner')
const rules = ref<RuleCatalogItem[]>([])
const loading = ref(true)
const healthScoreConfig = ref<HealthScoreConfig | null>(null)
const savingHealthScore = ref(false)
const healthScoreSavedMessage = ref('')

const LOCALE_LABELS: Record<Locale, string> = { en: 'English', 'pt-PT': 'Português (PT)' }

const healthScoreTotal = computed(() => {
  const c = healthScoreConfig.value
  if (!c) return 0
  return Math.round((c.attendanceWeight + c.consistencyWeight + c.bookingBehaviourWeight + c.progressWeight + c.engagementWeight) * 10) / 10
})
const healthScoreTotalValid = computed(() => Math.abs(healthScoreTotal.value - 100) < 0.01)
const enabledColumnHint = computed(() =>
  rules.value.some((r) => !r.toggleable) ? `${t('settings.enabledHint')} ${t('settings.notToggleableHint')}` : t('settings.enabledHint'),
)

async function load() {
  loading.value = true
  const requests: Promise<unknown>[] = [api.get<RuleCatalogItem[]>('/rules').then((r) => (rules.value = r))]
  if (canManageHealthScore.value) {
    requests.push(api.get<HealthScoreConfig>('/health-score/config').then((c) => (healthScoreConfig.value = c)))
  }
  await Promise.all(requests)
  loading.value = false
}

async function toggleRule(rule: RuleCatalogItem) {
  if (!rule.toggleable) return
  const next = !rule.enabled
  await api.put(`/rules/${rule.code}/enabled`, { enabled: next })
  rule.enabled = next
}

async function saveHealthScoreConfig() {
  if (!healthScoreConfig.value || !healthScoreTotalValid.value) return
  savingHealthScore.value = true
  healthScoreSavedMessage.value = ''
  try {
    const saved = await api.put<HealthScoreConfig>('/health-score/config', healthScoreConfig.value)
    healthScoreConfig.value = saved
    healthScoreSavedMessage.value = t('settings.healthScore.saved', { version: saved.version })
  } finally {
    savingHealthScore.value = false
  }
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
            <th>{{ t('settings.enabled') }}<InfoTip :text="enabledColumnHint" /></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in rules" :key="r.code">
            <td>{{ r.code }}</td>
            <td>{{ r.family }}</td>
            <td>{{ r.description }}</td>
            <td>
              <label class="switch">
                <input type="checkbox" :checked="r.enabled" :disabled="!r.toggleable" @change="toggleRule(r)" />
              </label>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <section v-if="canManageHealthScore && healthScoreConfig" class="card">
      <h2>{{ t('settings.healthScore.title') }}</h2>
      <p class="hint">{{ t('settings.healthScore.hint') }}</p>
      <p class="hint">{{ t('settings.healthScore.version') }}: v{{ healthScoreConfig.version }}</p>

      <table>
        <thead>
          <tr>
            <th>{{ t('settings.family') }}</th>
            <th>{{ t('settings.healthScore.enabled') }}</th>
            <th>{{ t('settings.healthScore.weight') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="f in HEALTH_SCORE_FACTORS" :key="f.key">
            <td>{{ t(`memberDetail.healthScore.factors.${f.key.charAt(0).toUpperCase()}${f.key.slice(1)}`) }}</td>
            <td>
              <label class="switch">
                <input type="checkbox" v-model="healthScoreConfig[f.enabled]" />
              </label>
            </td>
            <td>
              <input
                type="number"
                min="0"
                max="100"
                step="1"
                class="weight-input"
                v-model.number="healthScoreConfig[f.weight]"
              />
            </td>
          </tr>
        </tbody>
      </table>

      <p class="total" :class="{ invalid: !healthScoreTotalValid }">
        {{ t('settings.healthScore.total') }}: {{ healthScoreTotal }}%
      </p>
      <p v-if="!healthScoreTotalValid" class="total-error">
        {{ t('settings.healthScore.invalidTotal', { total: healthScoreTotal }) }}
      </p>

      <div class="cold-start">
        <label>
          {{ t('settings.healthScore.minTenureDays') }}
          <input type="number" min="0" v-model.number="healthScoreConfig.minTenureDays" />
        </label>
        <label>
          {{ t('settings.healthScore.minSessions') }}
          <input type="number" min="0" v-model.number="healthScoreConfig.minSessions" />
        </label>
      </div>
      <p class="hint">{{ t('settings.healthScore.coldStartHint') }}</p>

      <button :disabled="!healthScoreTotalValid || savingHealthScore" @click="saveHealthScoreConfig">
        {{ t('settings.healthScore.save') }}
      </button>
      <p v-if="healthScoreSavedMessage" class="hint">{{ healthScoreSavedMessage }}</p>
    </section>
  </div>
</template>

<style scoped>
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
.weight-input {
  width: 4.5rem;
}
.total {
  font-weight: 600;
  margin-top: 0.75rem;
}
.total.invalid {
  color: var(--color-danger);
}
.total-error {
  font-size: 0.85rem;
  color: var(--color-danger);
  margin-top: -0.5rem;
}
.cold-start {
  display: flex;
  gap: 1.5rem;
  margin-top: 0.75rem;
}
.cold-start label {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}
.cold-start input {
  width: 6rem;
}
</style>
