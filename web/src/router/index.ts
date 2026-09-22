import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

// Nine real pages, nine nav entries, one route each — see App.vue for the nav list that mirrors
// this 1:1 (the four dashboard pages used to be tabs on one /dashboard page; each is a real page
// now). Old URLs from a previous nav layout (fake "groups" that pointed multiple sidebar entries
// at the same page, and a couple of renamed sections) redirect below so no bookmark or external
// link breaks, but none of them are linked from the UI anymore.
const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue'), meta: { public: true } },
    {
      path: '/bootstrap',
      name: 'bootstrap',
      component: () => import('@/views/BootstrapView.vue'),
      meta: { public: true },
    },
    { path: '/f/:token', name: 'public-form', component: () => import('@/views/PublicFormView.vue'), meta: { public: true } },
    { path: '/', redirect: '/dashboard/retention' },

    // Dashboards — four separate pages, each its own sidebar entry (not tabs on one page).
    { path: '/dashboard', redirect: '/dashboard/retention' },
    { path: '/dashboard/retention', name: 'dashboard-retention', component: () => import('@/views/dashboards/RetentionDashboardView.vue') },
    { path: '/dashboard/interventions', name: 'dashboard-response-performance', component: () => import('@/views/dashboards/ResponsePerformanceDashboardView.vue') },
    { path: '/dashboard/attendance', name: 'dashboard-workouts', component: () => import('@/views/dashboards/WorkoutsDashboardView.vue') },
    { path: '/dashboard/my-week', name: 'dashboard-my-week', component: () => import('@/views/dashboards/MyWeekDashboardView.vue') },

    // Members — one page; "at risk" / "new" / etc. are quick filters via ?filter=, not separate
    // routes, so they can't accidentally spawn separate nav entries again later.
    { path: '/members', name: 'members', component: () => import('@/views/MembersListView.vue') },
    { path: '/members/:id', name: 'member-detail', component: () => import('@/views/MemberDetailView.vue') },

    { path: '/alerts', name: 'alerts', component: () => import('@/views/AlertsInboxView.vue') },
    { path: '/coaches', name: 'coaches', component: () => import('@/views/CoachesListView.vue') },
    { path: '/import', name: 'import', component: () => import('@/views/ImportView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },

    // --- Redirects from the previous nav layout (kept for old links only, not linked in the UI) ---
    { path: '/dashboards', redirect: '/dashboard/retention' },
    { path: '/athletes', redirect: '/members' },
    {
      path: '/athletes/:preset',
      redirect: (to) => (to.params.preset === 'all' ? '/members' : `/members?filter=${to.params.preset}`),
    },
    { path: '/configuration', redirect: '/settings' },
    { path: '/configuration/general', redirect: '/settings' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.token) return { name: 'login' }
})

export default router
