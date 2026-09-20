<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { api } from '@/lib/api'

interface MemberDetail {
  id: string
  name: string
  email: string | null
  phone: string | null
  status: string
  joinDate: string
  awayUntil: string | null
  injuryFlagUntil: string | null
}

interface MemberNote {
  id: string
  text: string
  source: string
  isActive: boolean
  createdAt: string
}

interface TimelineItem {
  type: string
  at: string
  summary: string
}

interface Consent {
  channel: string
  granted: boolean
}

interface OutreachItem {
  id: string
  channel: string
  templateKey: string | null
  body: string
  sentBy: string
  status: string
  isHoldout: boolean
  createdAt: string
}

const route = useRoute()
const memberId = route.params.id as string

const member = ref<MemberDetail | null>(null)
const notes = ref<MemberNote[]>([])
const timeline = ref<TimelineItem[]>([])
const consent = ref<Consent[]>([])
const outreach = ref<OutreachItem[]>([])
const newNote = ref('')
const savingNote = ref(false)

async function load() {
  ;[member.value, notes.value, timeline.value, consent.value, outreach.value] = await Promise.all([
    api.get<MemberDetail>(`/members/${memberId}`),
    api.get<MemberNote[]>(`/members/${memberId}/notes`),
    api.get<TimelineItem[]>(`/members/${memberId}/timeline`),
    api.get<Consent[]>(`/members/${memberId}/consent`),
    api.get<OutreachItem[]>(`/members/${memberId}/outreach`),
  ])
}

async function toggleConsent(c: Consent) {
  const next = !c.granted
  await api.put(`/members/${memberId}/consent`, { channel: c.channel, granted: next })
  c.granted = next
}

async function addNote() {
  if (!newNote.value.trim()) return
  savingNote.value = true
  try {
    const note = await api.post<MemberNote>(`/members/${memberId}/notes`, { text: newNote.value.trim() })
    notes.value = [note, ...notes.value]
    newNote.value = ''
  } finally {
    savingNote.value = false
  }
}

async function removeNote(noteId: string) {
  await api.delete(`/members/${memberId}/notes/${noteId}`)
  notes.value = notes.value.filter((n) => n.id !== noteId)
}

onMounted(load)
</script>

<template>
  <div v-if="member">
    <h1>{{ member.name }}</h1>
    <p class="meta">{{ member.email }} · {{ member.phone }} · joined {{ member.joinDate }}</p>

    <section class="card">
      <h2>Notes</h2>
      <p class="hint">Context for coaches — e.g. "recovering from a knee injury". Notes can be added here or arrive from an import.</p>
      <form class="add-note" @submit.prevent="addNote">
        <input v-model="newNote" placeholder="Add a note…" />
        <button type="submit" :disabled="savingNote">Add</button>
      </form>
      <ul class="notes">
        <li v-for="n in notes.filter((n) => n.isActive)" :key="n.id">
          <span class="source" :class="n.source.toLowerCase()">{{ n.source }}</span>
          <span class="text">{{ n.text }}</span>
          <span class="date">{{ new Date(n.createdAt).toLocaleDateString() }}</span>
          <button class="remove" @click="removeNote(n.id)">✕</button>
        </li>
        <li v-if="notes.filter((n) => n.isActive).length === 0" class="empty">No notes yet.</li>
      </ul>
    </section>

    <section class="card">
      <h2>Consent</h2>
      <p class="hint">Which channels this member can be messaged on. Defaults to granted until revoked.</p>
      <ul class="consent">
        <li v-for="c in consent" :key="c.channel">
          <label>
            <input type="checkbox" :checked="c.granted" @change="toggleConsent(c)" />
            {{ c.channel }}
          </label>
        </li>
      </ul>
    </section>

    <section class="card">
      <h2>Outreach history</h2>
      <ul class="outreach">
        <li v-for="o in outreach" :key="o.id">
          <span class="source" :class="o.sentBy.toLowerCase()">{{ o.sentBy }}</span>
          <span class="channel">{{ o.channel }}</span>
          <span class="text">{{ o.isHoldout ? '[holdout — no message sent]' : o.body }}</span>
          <span class="status">{{ o.status }}</span>
          <span class="date">{{ new Date(o.createdAt).toLocaleString() }}</span>
        </li>
        <li v-if="outreach.length === 0" class="empty">No messages sent yet.</li>
      </ul>
    </section>

    <section class="card">
      <h2>Timeline</h2>
      <ul class="timeline">
        <li v-for="(t, i) in timeline" :key="i">
          <span class="type" :class="t.type">{{ t.type }}</span>
          <span>{{ t.summary }}</span>
          <span class="date">{{ new Date(t.at).toLocaleString() }}</span>
        </li>
        <li v-if="timeline.length === 0" class="empty">No activity yet.</li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.meta {
  color: #666;
  margin-top: -0.5rem;
}
.card {
  background: white;
  border-radius: 8px;
  padding: 1rem 1.25rem;
  margin-top: 1rem;
}
.hint {
  font-size: 0.85rem;
  color: #777;
  margin-top: -0.25rem;
}
.add-note {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}
.add-note input {
  flex: 1;
  padding: 0.5rem;
  border-radius: 6px;
  border: 1px solid #ccc;
}
.add-note button {
  padding: 0.5rem 1rem;
  border-radius: 6px;
  border: none;
  background: #1a1a2e;
  color: white;
  cursor: pointer;
}
.notes,
.timeline,
.outreach,
.consent {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.notes li,
.timeline li,
.outreach li {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.4rem 0;
  border-bottom: 1px solid #f0f0f0;
}
.consent li label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  text-transform: capitalize;
}
.source,
.type,
.channel,
.status {
  font-size: 0.7rem;
  text-transform: uppercase;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  background: #eee;
  color: #555;
}
.source.import,
.source.system {
  background: #e5eefc;
  color: #1e4d7a;
}
.text {
  flex: 1;
}
.date {
  margin-left: auto;
  font-size: 0.75rem;
  color: #999;
}
.remove {
  background: none;
  border: none;
  color: #aaa;
  cursor: pointer;
}
.empty {
  color: #999;
}
</style>
