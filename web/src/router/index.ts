import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

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

    // Dashboard — DashboardsView.vue is one component; :tab drives which of its four
    // sections is shown (see that file's ROUTE_TO_TAB map), so Classes/Insights/Reports
    // below link into the same routes instead of forking the page.
    { path: '/dashboard', redirect: '/dashboard/retention' },
    { path: '/dashboard/:tab', name: 'dashboard', component: () => import('@/views/DashboardsView.vue') },
    { path: '/dashboards', redirect: '/dashboard/retention' },

    // Athletes — MembersListView.vue applies a filter preset from the route (see its PRESET_* maps).
    { path: '/athletes', redirect: '/athletes/all' },
    { path: '/athletes/:preset', name: 'athletes', component: () => import('@/views/MembersListView.vue') },
    { path: '/members', redirect: '/athletes/all' },
    { path: '/members/:id', name: 'member-detail', component: () => import('@/views/MemberDetailView.vue') },

    { path: '/alerts', name: 'alerts', component: () => import('@/views/AlertsInboxView.vue') },
    { path: '/import', name: 'import', component: () => import('@/views/ImportView.vue') },
    { path: '/coaches', name: 'coaches', component: () => import('@/views/CoachesListView.vue') },

    // Configuration — renamed/relocated Settings. Health Score config, the Metric registry
    // editor, and Coach/Payment settings from other branches add sibling routes here.
    { path: '/configuration', redirect: '/configuration/general' },
    { path: '/configuration/general', name: 'configuration-general', component: () => import('@/views/SettingsView.vue') },
    { path: '/settings', redirect: '/configuration/general' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.token) return { name: 'login' }
})

export default router
