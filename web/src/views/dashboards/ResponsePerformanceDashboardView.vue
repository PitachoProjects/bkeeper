<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/lib/api'
import { severityLabel } from '@/lib/labels'
import ExplainThis from '@/components/ExplainThis.vue'
import BarChart from '@/components/BarChart.vue'

const { t } = useI18n()

interface AlertOperations {
  volumeBySeverity: Record<string, number>
  volumeByFamily: Record<string, number>
  slaComplianceRatePct: number
  avgTimeToClaimHours: number | null
  outcomesMix: Record<string, number>
  saveRatePct: number
  holdoutReturnRatePct: number | null
  treatedReturnRatePct: number | null
}

const SEVERITY_COLOR: Record<string, string> = { Red: 'red', Amber: 'amber', Info: 'info' }

const loading = ref(true)
const data = ref<AlertOperations | null>(null)

onMounted(async () => {
  loading.value = true
  try {
    data.value = await api.get('/dashboards/alerts')
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div>
    <h1>{{ t('dashboards.pages.responsePerformance') }}</h1>
    <p class="page-subtitle">{{ t('dashboards.alertOps.pageHint') }}</p>

    <p v-if="loading">Loading…</p>

    <template v-else-if="data">
      <div class="stat-row">
        <div class="stat">
          <ExplainThis metric-key="alert_sla_compliance_rate_pct" icon-only />
          <span class="stat-value">{{ data.slaComplianceRatePct }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.slaCompliance') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.allTime') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="alert_avg_time_to_claim_hours" icon-only />
          <span class="stat-value">{{ data.avgTimeToClaimHours ?? '—' }}</span>
          <span class="stat-label">{{ t('dashboards.alertOps.avgHoursToClaim') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.allTime') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="alert_save_rate_pct" icon-only />
          <span class="stat-value">{{ data.saveRatePct }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.saveRate') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.allTime') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="alert_return_rate_holdout_vs_treated_pct" icon-only />
          <span class="stat-value">{{ data.treatedReturnRatePct ?? '—' }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.treatedReturnRate') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.next14Days') }}</span>
        </div>
        <div class="stat">
          <ExplainThis metric-key="alert_return_rate_holdout_vs_treated_pct" icon-only />
          <span class="stat-value">{{ data.holdoutReturnRatePct ?? '—' }}%</span>
          <span class="stat-label">{{ t('dashboards.alertOps.holdoutReturnRate') }}</span>
          <span class="stat-period">{{ t('dashboards.periods.next14Days') }}</span>
        </div>
      </div>

      <section class="card">
        <h2>{{ t('dashboards.alertOps.volumeTitle') }}</h2>
        <p class="hint">{{ t('dashboards.alertOps.volumeHint') }}</p>
        <div class="two-col">
          <div>
            <h3>{{ t('dashboards.alertOps.bySeverity') }}</h3>
            <BarChart
              :bars="Object.entries(data.volumeBySeverity).map(([k, v]) => ({ label: severityLabel(k), value: v, colorClass: SEVERITY_COLOR[k] }))"
            />
          </div>
          <div>
            <h3>{{ t('dashboards.alertOps.byFamily') }}</h3>
            <BarChart :bars="Object.entries(data.volumeByFamily).map(([k, v]) => ({ label: k, value: v }))" />
          </div>
        </div>
      </section>

      <section class="card">
        <h2>{{ t('dashboards.alertOps.outcomesTitle') }}</h2>
        <p class="hint">{{ t('dashboards.alertOps.outcomesHint') }}</p>
        <BarChart :bars="Object.entries(data.outcomesMix).map(([k, v]) => ({ label: k.replaceAll('_', ' '), value: v }))" />
        <p v-if="Object.keys(data.outcomesMix).length === 0" class="empty">{{ t('dashboards.alertOps.empty') }}</p>
      </section>
    </template>
  </div>
</template>

<style scoped>
.two-col {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}
.two-col h3 {
  font-size: 0.85rem;
  margin-bottom: 0.4rem;
  color: var(--color-text-muted);
}
</style>
