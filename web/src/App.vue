<script setup lang="ts">
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
</script>

<template>
  <div v-if="!route.meta.public" class="shell">
    <aside class="sidebar">
      <div class="brand">BKeeper</div>
      <nav>
        <RouterLink to="/members">Members</RouterLink>
        <RouterLink to="/alerts">Alert inbox</RouterLink>
        <RouterLink to="/import">Import</RouterLink>
      </nav>
      <div class="user">
        <div>{{ auth.displayName }}</div>
        <div class="role">{{ auth.role }}</div>
        <button @click="auth.logout()">Sign out</button>
      </div>
    </aside>
    <main class="content"><RouterView /></main>
  </div>
  <RouterView v-else />
</template>

<style scoped>
.shell {
  display: flex;
  min-height: 100vh;
}
.sidebar {
  width: 220px;
  background: #1a1a2e;
  color: white;
  display: flex;
  flex-direction: column;
  padding: 1.25rem 1rem;
}
.brand {
  font-weight: 700;
  font-size: 1.25rem;
  margin-bottom: 1.5rem;
}
nav {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  flex: 1;
}
nav a {
  color: #ccc;
  text-decoration: none;
  padding: 0.4rem 0.5rem;
  border-radius: 6px;
}
nav a.router-link-active {
  background: #33334d;
  color: white;
}
.user {
  font-size: 0.85rem;
  border-top: 1px solid #33334d;
  padding-top: 0.75rem;
}
.role {
  color: #999;
  margin-bottom: 0.5rem;
}
.user button {
  width: 100%;
  padding: 0.4rem;
  background: transparent;
  border: 1px solid #555;
  color: #ccc;
  border-radius: 6px;
  cursor: pointer;
}
.content {
  flex: 1;
  padding: 1.5rem 2rem;
  background: #f7f7fa;
}
</style>
