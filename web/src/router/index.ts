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
    { path: '/', redirect: '/members' },
    { path: '/members', name: 'members', component: () => import('@/views/MembersListView.vue') },
    { path: '/members/:id', name: 'member-detail', component: () => import('@/views/MemberDetailView.vue') },
    { path: '/alerts', name: 'alerts', component: () => import('@/views/AlertsInboxView.vue') },
    { path: '/import', name: 'import', component: () => import('@/views/ImportView.vue') },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.token) return { name: 'login' }
})

export default router
