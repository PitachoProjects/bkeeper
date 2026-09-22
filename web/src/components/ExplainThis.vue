<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { api, ApiError } from '@/lib/api'

const props = defineProps<{ metricKey: string; iconOnly?: boolean }>()

const { t } = useI18n()

interface MetricDefinitionDetail {
  key: string
  name: string
  category: string
  unit: string
  aggregation: string
  version: number
  definition: string
  howCalculated: string
  period: string
  whyItMatters: string
  limitations: string
}

// Shared across every ExplainThis instance on the page so the same metric isn't re-fetched per row.
const cache = new Map<string, Promise<MetricDefinitionDetail>>()

const open = ref(false)
const loading = ref(false)
const error = ref(false)
const detail = ref<MetricDefinitionDetail | null>(null)

async function toggle() {
  open.value = !open.value
  if (!open.value || detail.value || loading.value) return

  loading.value = true
  error.value = false
  try {
    let pending = cache.get(props.metricKey)
    if (!pending) {
      pending = api.get<MetricDefinitionDetail>(`/metric-definitions/${props.metricKey}`)
      cache.set(props.metricKey, pending)
    }
    detail.value = await pending
  } catch (e) {
    cache.delete(props.metricKey)
    error.value = true
    if (!(e instanceof ApiError)) throw e
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <span class="explain-this" :class="{ corner: iconOnly }">
    <button
      type="button"
      class="ghost trigger"
      :class="{ active: open, 'icon-only': iconOnly }"
      :title="iconOnly ? t('explainThis.trigger') : undefined"
      @click="toggle"
      :aria-expanded="open"
      :aria-label="iconOnly ? t('explainThis.trigger') : undefined"
    >
      <span class="glyph" aria-hidden="true">ⓘ</span>
      <template v-if="!iconOnly">{{ t('explainThis.trigger') }}</template>
    </button>

    <div v-if="open" class="panel card" :class="{ 'panel-corner': iconOnly }">
      <p v-if="loading" class="state">{{ t('explainThis.loading') }}</p>
      <p v-else-if="error" class="state error">{{ t('explainThis.error') }}</p>
      <dl v-else-if="detail">
        <dt>{{ t('explainThis.definition') }}</dt>
        <dd>{{ detail.definition }}</dd>

        <dt>{{ t('explainThis.howCalculated') }}</dt>
        <dd class="formula">{{ detail.howCalculated }}</dd>

        <dt>{{ t('explainThis.period') }}</dt>
        <dd>{{ detail.period }}</dd>

        <dt>{{ t('explainThis.whyItMatters') }}</dt>
        <dd>{{ detail.whyItMatters }}</dd>

        <dt>{{ t('explainThis.limitations') }}</dt>
        <dd>{{ detail.limitations }}</dd>
      </dl>
    </div>
  </span>
</template>

<style scoped>
.explain-this {
  position: relative;
  display: inline-block;
}
.explain-this.corner {
  position: absolute;
  top: 0.6rem;
  right: 0.6rem;
}
.trigger {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.1rem 0.4rem;
  font-size: 0.7rem;
  font-weight: 500;
  border-radius: 999px;
  border-color: transparent;
  color: var(--color-text-muted);
  vertical-align: middle;
  margin-left: 0.35rem;
}
.trigger.icon-only {
  margin-left: 0;
  padding: 0.1rem;
  width: 1.35rem;
  height: 1.35rem;
  justify-content: center;
}
.trigger:hover,
.trigger.active {
  color: var(--color-accent);
  background: var(--color-accent-soft);
}
.glyph {
  font-size: 0.85rem;
  line-height: 1;
}
.panel {
  position: absolute;
  z-index: 20;
  top: calc(100% + 0.35rem);
  left: 0;
  width: min(340px, 80vw);
  padding: 0.9rem 1rem;
  font-weight: normal;
  text-align: left;
  white-space: normal;
}
.panel-corner {
  left: auto;
  right: 0;
}
.state {
  font-size: 0.85rem;
  color: var(--color-text-muted);
}
.state.error {
  color: var(--color-danger);
}
dl {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}
dt {
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-faint);
  margin-top: 0.6rem;
}
dt:first-child {
  margin-top: 0;
}
dd {
  font-size: 0.82rem;
  color: var(--color-text);
  margin: 0;
}
dd.formula {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.75rem;
  color: var(--color-text-muted);
}
</style>
