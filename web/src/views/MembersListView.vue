<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api } from '@/lib/api'

interface MemberListItem {
  id: string
  name: string
  email: string | null
  status: string
  joinDate: string
}

const members = ref<MemberListItem[]>([])
const search = ref('')
const loading = ref(true)

async function load() {
  loading.value = true
  const query = search.value ? `?search=${encodeURIComponent(search.value)}` : ''
  members.value = await api.get<MemberListItem[]>(`/members${query}`)
  loading.value = false
}

onMounted(load)
</script>

<template>
  <div>
    <div class="header">
      <h1>Members</h1>
      <input v-model="search" placeholder="Search by name…" @keyup.enter="load" />
    </div>
    <p v-if="loading">Loading…</p>
    <table v-else>
      <thead>
        <tr>
          <th>Name</th>
          <th>Email</th>
          <th>Status</th>
          <th>Joined</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="m in members" :key="m.id" @click="$router.push(`/members/${m.id}`)">
          <td>{{ m.name }}</td>
          <td>{{ m.email }}</td>
          <td><span class="badge" :class="m.status.toLowerCase()">{{ m.status }}</span></td>
          <td>{{ m.joinDate }}</td>
        </tr>
        <tr v-if="members.length === 0">
          <td colspan="4">No members yet — try the Import page.</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}
input {
  padding: 0.5rem;
  border-radius: 6px;
  border: 1px solid #ccc;
  width: 240px;
}
table {
  width: 100%;
  border-collapse: collapse;
  background: white;
  border-radius: 8px;
  overflow: hidden;
}
th,
td {
  text-align: left;
  padding: 0.6rem 0.8rem;
  border-bottom: 1px solid #eee;
}
tbody tr {
  cursor: pointer;
}
tbody tr:hover {
  background: #f0f0f5;
}
.badge {
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  background: #dff5e1;
  color: #1e7a34;
}
.badge.frozen {
  background: #e5eefc;
  color: #1e4d7a;
}
.badge.cancelled,
.badge.lapsed {
  background: #fbe3e1;
  color: #a12e24;
}
</style>
