<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { severityLabel } from '@/lib/labels'
import InfoTip from '@/components/InfoTip.vue'

const { t } = useI18n()

interface MyWeekAlert {
  id: string
  memberId: string
  memberName: string
  family: string
  severity: string
  status: string
  dueAt: string
}
interface MyWeek {
  openAlerts: MyWeekAlert[]
  dueThisWeekCount: number
  resolvedThisWeekCount: number
}

const loading = ref(true)
const data = ref<MyWeek | null>(null)
const severityFilter = ref('')

const visibleAlerts = computed(() =>
  (data.value?.openAlerts ?? []).filter((a) => !severityFilter.value || a.severity === severityFilter.value),
)

onMounted(async () => {
  loading.value = true
  try {
    data.value = await api.get('/dashboards/my-week')
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div>
    <h1>{{ t('dashboards.pages.myWeek') }}</h1>
    <p class="page-subtitle">{{ t('dashboards.myWeek.pageHint') }}</p>

    <p v-if="loading">Loading…</p>

    <template v-else-if="data">
      <div class="stat-row">
        <div class="stat">
          <span class="stat-value">{{ data.openAlerts.length }}</span>
          <span class="stat-label">{{ t('dashboards.myWeek.openCoach') }}<InfoTip :text="t('dashboards.myWeek.openCoachHint')" /></span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ data.dueThisWeekCount }}</span>
          <span class="stat-label">{{ t('dashboards.myWeek.dueThisWeek') }}<InfoTip :text="t('dashboards.myWeek.dueThisWeekHint')" /></span>
        </div>
        <div class="stat">
          <span class="stat-value">{{ data.resolvedThisWeekCount }}</span>
          <span class="stat-label">{{ t('dashboards.myWeek.resolvedThisWeek') }}<InfoTip :text="t('dashboards.myWeek.resolvedThisWeekHint')" /></span>
        </div>
      </div>
      <section class="card">
        <div class="section-header">
          <h2>{{ t('dashboards.myWeek.openAlertsTitle') }}</h2>
          <select v-model="severityFilter">
            <option value="">{{ t('alerts.allSeverities') }}</option>
            <option value="Red">{{ t('severity.Red') }}</option>
            <option value="Amber">{{ t('severity.Amber') }}</option>
            <option value="Info">{{ t('severity.Info') }}</option>
          </select>
        </div>
        <table>
          <thead>
            <tr>
              <th>{{ t('alerts.severity') }}</th>
              <th>{{ t('alerts.member') }}</th>
              <th>{{ t('alerts.family') }}</th>
              <th>{{ t('alerts.status') }}</th>
              <th>{{ t('dashboards.myWeek.due') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="a in visibleAlerts" :key="a.id">
              <td><span class="badge" :class="a.severity.toLowerCase()">{{ severityLabel(a.severity) }}</span></td>
              <td><RouterLink :to="`/members/${a.memberId}`">{{ a.memberName }}</RouterLink></td>
              <td>{{ a.family }}</td>
              <td>{{ a.status }}</td>
              <td>{{ new Date(a.dueAt).toLocaleString() }}</td>
            </tr>
            <tr v-if="visibleAlerts.length === 0"><td colspan="5" class="empty">{{ t('dashboards.myWeek.empty') }}</td></tr>
          </tbody>
        </table>
      </section>
    </template>
  </div>
</template>
