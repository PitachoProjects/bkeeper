<script setup lang="ts">
import { computed, reactive } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useThemeStore } from '@/stores/theme'
import { useLocaleStore } from '@/stores/locale'

const auth = useAuthStore()
const theme = useThemeStore()
useLocaleStore()
const { t } = useI18n()
const route = useRoute()

interface NavItem {
  to: string
  label: string
}
interface NavGroup {
  key: string
  label: string
  items: NavItem[]
}

// Workflow-oriented groups, replacing the old flat Members/Alerts/Dashboards/Import/Settings
// list. Several items across groups intentionally point at the same route — DashboardsView.vue
// is one component whose :tab route param drives which of its four sections renders (see its
// ROUTE_TO_TAB map), so Classes/Insights/Reports below reuse those routes rather than forking
// the analytics page.
const groups = computed<NavGroup[]>(() => [
  {
    key: 'dashboard',
    label: t('nav.groups.dashboard'),
    items: [
      { to: '/dashboard/retention', label: t('nav.dashboard.overview') },
      { to: '/dashboard/attendance', label: t('nav.dashboard.attendance') },
      { to: '/dashboard/interventions', label: t('nav.dashboard.interventions') },
      { to: '/dashboard/my-week', label: t('nav.dashboard.myWeek') },
    ],
  },
  {
    key: 'athletes',
    label: t('nav.groups.athletes'),
    items: [
      { to: '/athletes/all', label: t('nav.athletes.all') },
      { to: '/athletes/at-risk', label: t('nav.athletes.atRisk') },
      { to: '/athletes/early-warning', label: t('nav.athletes.earlyWarning') },
      { to: '/athletes/new', label: t('nav.athletes.new') },
      { to: '/athletes/inactive', label: t('nav.athletes.inactive') },
      { to: '/alerts', label: t('nav.athletes.alertInbox') },
    ],
  },
  {
    key: 'classes',
    label: t('nav.groups.classes'),
    items: [
      // ClassSession/Booking data only exists today inside the workouts dashboard tab
      // (heatmap, class fill, recent sessions) — no dedicated Classes page/API yet, so this
      // reuses that tab rather than inventing capacity/coach-management screens with no backend.
      { to: '/dashboard/attendance', label: t('nav.classes.analytics') },
    ],
  },
  {
    key: 'insights',
    label: t('nav.groups.insights'),
    items: [
      { to: '/dashboard/retention', label: t('nav.insights.retention') },
      { to: '/dashboard/interventions', label: t('nav.insights.alertOutcomes') },
      // Health Score and Metric-registry "explain this" insights (other branches) plug in here.
    ],
  },
  {
    key: 'reports',
    label: t('nav.groups.reports'),
    items: [
      // The retention page's own "Export CSV" button calls GET /dashboards/retention/export —
      // no other report type has backend support, so this section stays to that one link.
      { to: '/dashboard/retention', label: t('nav.reports.retention') },
    ],
  },
  {
    key: 'configuration',
    label: t('nav.groups.configuration'),
    items: [
      { to: '/configuration/general', label: t('nav.configuration.general') },
      { to: '/import', label: t('nav.configuration.import') },
      // Health Score config, the Metric registry editor, and Coach/Payment settings
      // (other branches) add sibling routes/items here.
    ],
  },
])

// No nav item here is more visible than its underlying page: risk-score data stays gated
// inside MemberDetailView (Manager/Owner only) and the alert-severity-based Athletes presets
// reuse the alerts endpoint, which was already unrestricted by role — this restructure adds
// no new role gates and removes none.
const activeGroupKey = computed(() => {
  if (route.name === 'member-detail') return 'athletes'
  return groups.value.find((g) => g.items.some((i) => route.path === i.to || route.path.startsWith(`${i.to}/`)))?.key
})
const collapsed = reactive<Record<string, boolean>>({})
function isExpanded(key: string) {
  return key in collapsed ? !collapsed[key] : key === activeGroupKey.value
}
function toggleGroup(key: string) {
  collapsed[key] = isExpanded(key)
}
</script>

<template>
  <div v-if="!route.meta.public" class="shell">
    <aside class="sidebar">
      <div class="brand">BKeeper</div>
      <nav>
        <div v-for="group in groups" :key="group.key" class="nav-group">
          <button
            type="button"
            class="nav-group-header"
            :class="{ active: activeGroupKey === group.key }"
            :aria-expanded="isExpanded(group.key)"
            @click="toggleGroup(group.key)"
          >
            <span>{{ group.label }}</span>
            <span class="chevron" :class="{ open: isExpanded(group.key) }">›</span>
          </button>
          <div v-show="isExpanded(group.key)" class="nav-group-items">
            <RouterLink v-for="item in group.items" :key="item.to" :to="item.to">{{ item.label }}</RouterLink>
          </div>
        </div>
      </nav>
      <div class="theme-switch" role="group" aria-label="Theme">
        <button :class="{ active: theme.mode === 'light' }" @click="theme.setMode('light')">{{ t('theme.light') }}</button>
        <button :class="{ active: theme.mode === 'dark' }" @click="theme.setMode('dark')">{{ t('theme.dark') }}</button>
      </div>
      <div class="user">
        <div>{{ auth.displayName }}</div>
        <div class="role">{{ auth.role }}</div>
        <button class="ghost" @click="auth.logout()">{{ t('signOut') }}</button>
      </div>
    </aside>
    <main class="content"><RouterView /></main>
  </div>
  <RouterView v-else />
</template>

<style scoped>
.shell {
  display: flex;
  height: 100vh;
  background: var(--color-bg);
}
.sidebar {
  width: 232px;
  flex-shrink: 0;
  background: var(--color-surface);
  border-right: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1.5rem 1.1rem;
  height: 100%;
  overflow-y: auto;
}
.brand {
  font-weight: 700;
  font-size: 1.15rem;
  letter-spacing: -0.01em;
  color: var(--color-text);
}
nav {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  flex: 1;
  overflow-y: auto;
}
.nav-group-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.55rem 0.7rem;
  margin-top: 0.5rem;
  cursor: pointer;
}
.nav-group-header:hover {
  filter: none;
  background: var(--color-bg-soft);
  color: var(--color-text);
}
.nav-group-header.active {
  color: var(--color-accent);
}
.chevron {
  display: inline-block;
  font-size: 0.9rem;
  transition: transform 0.15s ease;
}
.chevron.open {
  transform: rotate(90deg);
}
.nav-group-items {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}
.nav-group-items a {
  color: var(--color-text-muted);
  text-decoration: none;
  padding: 0.5rem 0.7rem 0.5rem 1.1rem;
  border-radius: var(--radius-sm);
  font-size: 0.9rem;
  font-weight: 500;
  border-left: 2px solid transparent;
  transition:
    background-color 0.15s ease,
    color 0.15s ease;
}
.nav-group-items a:hover {
  background: var(--color-bg-soft);
  color: var(--color-text);
}
.nav-group-items a.router-link-active {
  background: var(--color-accent-soft);
  color: var(--color-accent);
  border-left-color: var(--color-accent);
  font-weight: 600;
}
.theme-switch {
  display: flex;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-sm);
  overflow: hidden;
}
.theme-switch button {
  flex: 1;
  border: none;
  border-radius: 0;
  background: transparent;
  color: var(--color-text-muted);
  font-weight: 500;
  padding: 0.4rem 0;
}
.theme-switch button:hover {
  filter: none;
  background: var(--color-bg-soft);
}
.theme-switch button.active {
  background: var(--color-accent);
  color: var(--color-accent-contrast);
}
.user {
  font-size: 0.82rem;
  border-top: 1px solid var(--color-border);
  padding-top: 0.9rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.role {
  color: var(--color-text-faint);
  text-transform: capitalize;
}
.user button {
  width: 100%;
}
.content {
  flex: 1;
  padding: 2rem 2.5rem;
  background: var(--color-bg);
  height: 100%;
  overflow-y: auto;
}
</style>
