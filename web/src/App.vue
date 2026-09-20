<script setup lang="ts">
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useThemeStore } from '@/stores/theme'

const auth = useAuthStore()
const theme = useThemeStore()
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
      <div class="theme-switch" role="group" aria-label="Theme">
        <button :class="{ active: theme.mode === 'light' }" @click="theme.setMode('light')">Light</button>
        <button :class="{ active: theme.mode === 'dark' }" @click="theme.setMode('dark')">Dark</button>
      </div>
      <div class="user">
        <div>{{ auth.displayName }}</div>
        <div class="role">{{ auth.role }}</div>
        <button class="ghost" @click="auth.logout()">Sign out</button>
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
  gap: 0.25rem;
  flex: 1;
}
nav a {
  color: var(--color-text-muted);
  text-decoration: none;
  padding: 0.5rem 0.7rem;
  border-radius: var(--radius-sm);
  font-size: 0.9rem;
  font-weight: 500;
  border-left: 2px solid transparent;
  transition:
    background-color 0.15s ease,
    color 0.15s ease;
}
nav a:hover {
  background: var(--color-bg-soft);
  color: var(--color-text);
}
nav a.router-link-active {
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
  min-height: 100vh;
}
</style>
