<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { adminApi, type AdminOverview } from '@/api/admin'

const overview = ref<AdminOverview | null>(null)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const { data } = await adminApi.overview()
    overview.value = data
  } catch (err) {
    const candidate = err as { response?: { data?: { message?: string } }; message?: string }
    error.value = candidate.response?.data?.message || candidate.message || 'Could not load the admin overview.'
  } finally {
    loading.value = false
  }
}

function formatDate(value: string) {
  return new Date(value).toLocaleString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="page-heading">
      <p class="eyebrow">Administration</p>
      <h1>Admin overview</h1>
      <p>Platform-wide accounts and resource counts. Enterprise farm and parcel data is organisation-scoped for regular users.</p>
    </div>

    <div v-if="loading" class="state">Loading platform overview...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <template v-else-if="overview">
      <div class="stats">
        <div class="stat"><span>Users</span><strong>{{ overview.users }}</strong></div>
        <div class="stat"><span>Admins</span><strong>{{ overview.admins }}</strong></div>
        <div class="stat"><span>Organisations</span><strong>{{ overview.organizations }}</strong></div>
        <div class="stat"><span>Farms</span><strong>{{ overview.farms }}</strong></div>
        <div class="stat"><span>Land parcels</span><strong>{{ overview.landParcels }}</strong></div>
        <div class="stat"><span>Legacy plots</span><strong>{{ overview.plots }}</strong></div>
        <div class="stat"><span>Devices</span><strong>{{ overview.devices }}</strong></div>
        <div class="stat"><span>Sensor readings</span><strong>{{ overview.readings }}</strong></div>
      </div>

      <section class="card">
        <div class="card-head">
          <div>
            <h2>User directory</h2>
            <p>Most recently created accounts.</p>
          </div>
          <span class="count">{{ overview.recentUsers.length }} shown</span>
        </div>

        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Roles</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="user in overview.recentUsers" :key="user.id">
                <td class="name">{{ user.fullName || '—' }}</td>
                <td>{{ user.email }}</td>
                <td>
                  <span v-for="role in (user.roles.length ? user.roles : ['User'])" :key="role" class="role">{{ role }}</span>
                </td>
                <td class="muted">{{ formatDate(user.createdAt) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.page { max-width: 1100px; margin: 0 auto; }
.page-heading { margin-bottom: 1.25rem; }
.eyebrow { margin: 0 0 0.3rem; color: #6b7c70; font-size: 0.72rem; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; }
h1 { margin: 0; font-size: 1.6rem; }
.page-heading > p:last-child { margin: 0.35rem 0 0; color: #6b7c70; font-size: 0.9rem; max-width: 760px; line-height: 1.5; }
.stats { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.7rem; margin-bottom: 1rem; }
.stat { background: white; border: 1px solid #e2e8e2; border-radius: 10px; padding: 0.9rem; box-shadow: 0 2px 8px rgba(26, 71, 42, 0.05); }
.stat span { display: block; color: #6b7c70; font-size: 0.74rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; }
.stat strong { display: block; margin-top: 0.35rem; font-size: 1.5rem; color: #1a472a; }
.card { background: white; border: 1px solid #e2e8e2; border-radius: 10px; box-shadow: 0 2px 8px rgba(26, 71, 42, 0.06); overflow: hidden; }
.card-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 1.1rem 1.2rem; border-bottom: 1px solid #eef2ee; }
.card-head h2 { margin: 0; font-size: 1.05rem; }
.card-head p { margin: 0.25rem 0 0; color: #6b7c70; font-size: 0.8rem; }
.count { border-radius: 999px; background: #edf6ef; color: #1a6b3c; padding: 0.2rem 0.55rem; font-size: 0.7rem; font-weight: 700; white-space: nowrap; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.84rem; }
th { text-align: left; padding: 0.7rem 1.2rem; color: #6b7c70; background: #f7faf7; font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.06em; }
td { padding: 0.75rem 1.2rem; border-top: 1px solid #eef2ee; color: #314b39; }
.name { font-weight: 600; color: #183024; }
.muted { color: #6b7c70; white-space: nowrap; }
.role { display: inline-block; margin-right: 0.3rem; border-radius: 999px; background: #edf6ef; color: #1a6b3c; padding: 0.18rem 0.5rem; font-size: 0.68rem; font-weight: 700; }
.state { color: #6b7c70; }
.error { border-radius: 7px; background: #fdecec; color: #9b1c1c; padding: 0.8rem 1rem; }
@media (max-width: 900px) { .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) { .stats { grid-template-columns: 1fr; } }
</style>
