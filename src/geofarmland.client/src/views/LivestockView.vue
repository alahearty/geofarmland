<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  livestockApi,
  type LivestockEvent,
  type LivestockGroup,
  type LivestockGroupRequest,
  type LivestockOverview
} from '@/api/livestock'
import { enterpriseApi, type Farm, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatDateOnly, formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()

const overview = ref<LivestockOverview | null>(null)
const farms = ref<Farm[]>([])
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const statusFilter = ref('')

const showGroupModal = ref(false)
const showEventModal = ref(false)
const editingGroup = ref<LivestockGroup | null>(null)
const eventGroup = ref<LivestockGroup | null>(null)
const saving = ref(false)
const formError = ref('')

const groupForm = reactive({
  farmId: null as number | null,
  grazingLandParcelId: null as number | null,
  name: '',
  species: 'Cattle',
  breed: '',
  purpose: 'Beef',
  headCount: null as number | null,
  averageWeightKg: null as number | null,
  status: 'Active',
  joinedDate: '',
  notes: ''
})

const eventForm = reactive({
  eventType: 'Health',
  eventDate: '',
  headCount: null as number | null,
  weightKg: null as number | null,
  notes: ''
})

const speciesOptions = ['Cattle', 'Sheep', 'Goats', 'Poultry', 'Pigs', 'Horses', 'Other']
const purposeOptions = ['Beef', 'Dairy', 'Wool', 'Meat', 'Eggs', 'Breeding', 'Other']
const statusOptions = ['Active', 'Quarantine', 'Sold', 'Archived']
const eventTypeOptions = ['Health', 'Vaccination', 'Movement', 'Mortality', 'Sale', 'Weight', 'Other']

const todayIso = () => {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const groups = computed(() => overview.value?.groups ?? [])
const statuses = computed(() => [...new Set(groups.value.map((group) => group.status))].sort())
const filteredGroups = computed(() => {
  const term = search.value.trim().toLowerCase()
  return groups.value.filter((group) => {
    if (statusFilter.value && group.status !== statusFilter.value) return false
    if (!term) return true
    return group.name.toLowerCase().includes(term)
      || group.species.toLowerCase().includes(term)
      || (group.breed ?? '').toLowerCase().includes(term)
      || group.farmName.toLowerCase().includes(term)
  })
})
const filteredHead = computed(() => filteredGroups.value.reduce((total, group) => total + group.headCount, 0))
const parcelsForFarm = computed(() =>
  parcels.value.filter((parcel) => !groupForm.farmId || parcel.farmId === groupForm.farmId)
)
const eventNeedsHeadCount = computed(() => ['Sale', 'Mortality', 'Health', 'Vaccination'].includes(eventForm.eventType))
const eventNeedsWeight = computed(() => eventForm.eventType === 'Weight')

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.message
    || 'The request could not be completed.'
}

function statusClass(value: string) {
  switch (value.toLowerCase()) {
    case 'active': return 'herd-active'
    case 'quarantine': return 'herd-quarantine'
    case 'sold': return 'herd-sold'
    case 'archived': return 'herd-archived'
    default: return 'herd-neutral'
  }
}

function eventClass(value: string) {
  switch (value.toLowerCase()) {
    case 'health': return 'event-health'
    case 'vaccination': return 'event-vaccination'
    case 'movement': return 'event-movement'
    case 'mortality': return 'event-mortality'
    case 'sale': return 'event-sale'
    case 'weight': return 'event-weight'
    default: return 'event-other'
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const organizationId = workspace.organizationId
    const [livestockResponse, farmResponse, parcelResponse] = await Promise.all([
      livestockApi.overview(organizationId),
      enterpriseApi.farms(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = livestockResponse.data
    farms.value = farmResponse.data
    parcels.value = parcelResponse.data.items
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function resetGroupForm() {
  formError.value = ''
  Object.assign(groupForm, {
    farmId: farms.value[0]?.id ?? null,
    grazingLandParcelId: null,
    name: '',
    species: 'Cattle',
    breed: '',
    purpose: 'Beef',
    headCount: null,
    averageWeightKg: null,
    status: 'Active',
    joinedDate: '',
    notes: ''
  })
  editingGroup.value = null
}

function openGroupModal() {
  resetGroupForm()
  showGroupModal.value = true
}

function editGroup(group: LivestockGroup) {
  formError.value = ''
  editingGroup.value = group
  Object.assign(groupForm, {
    farmId: group.farmId,
    grazingLandParcelId: group.grazingLandParcelId ?? null,
    name: group.name,
    species: group.species,
    breed: group.breed ?? '',
    purpose: group.purpose,
    headCount: group.headCount,
    averageWeightKg: group.averageWeightKg == null ? null : Number(group.averageWeightKg),
    status: group.status,
    joinedDate: group.joinedDate ?? '',
    notes: group.notes ?? ''
  })
  showGroupModal.value = true
}

function onFarmChange() {
  groupForm.grazingLandParcelId = null
}

async function saveGroup() {
  formError.value = ''
  if (!groupForm.farmId) {
    formError.value = 'Select the farm that owns this group.'
    return
  }
  if (groupForm.name.trim().length < 2) {
    formError.value = 'Enter a group name of at least two characters.'
    return
  }
  if (!groupForm.headCount || groupForm.headCount < 1) {
    formError.value = 'Head count must be at least one.'
    return
  }

  saving.value = true
  const payload: LivestockGroupRequest = {
    farmId: groupForm.farmId,
    grazingLandParcelId: groupForm.grazingLandParcelId ?? null,
    name: groupForm.name.trim(),
    species: groupForm.species,
    breed: groupForm.breed.trim() || undefined,
    purpose: groupForm.purpose,
    headCount: groupForm.headCount,
    averageWeightKg: groupForm.averageWeightKg ?? null,
    status: groupForm.status,
    joinedDate: groupForm.joinedDate || null,
    notes: groupForm.notes.trim() || undefined
  }
  try {
    if (editingGroup.value) {
      const { data } = await livestockApi.updateGroup(editingGroup.value.id, payload)
      message.value = `Group "${data.name}" updated.`
    } else {
      const { data } = await livestockApi.createGroup(payload, workspace.organizationId)
      message.value = `Group "${data.name}" registered.`
    }
    showGroupModal.value = false
    editingGroup.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function deleteGroup(group: LivestockGroup) {
  if (!window.confirm(`Delete the livestock group "${group.name}" and its event history?`)) return
  try {
    await livestockApi.deleteGroup(group.id)
    message.value = `Group "${group.name}" deleted.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

function openEventModal(group: LivestockGroup) {
  formError.value = ''
  eventGroup.value = group
  Object.assign(eventForm, {
    eventType: 'Health',
    eventDate: todayIso(),
    headCount: null,
    weightKg: null,
    notes: ''
  })
  showEventModal.value = true
}

async function saveEvent() {
  if (!eventGroup.value) return
  formError.value = ''
  if (!eventForm.eventDate) {
    formError.value = 'Enter the event date.'
    return
  }
  if (eventNeedsHeadCount.value && (!eventForm.headCount || eventForm.headCount < 1)) {
    formError.value = 'Enter the affected head count.'
    return
  }
  if (eventNeedsWeight.value && (!eventForm.weightKg || eventForm.weightKg <= 0)) {
    formError.value = 'Enter the measured weight.'
    return
  }

  saving.value = true
  try {
    await livestockApi.createEvent(eventGroup.value.id, {
      eventType: eventForm.eventType,
      eventDate: eventForm.eventDate,
      headCount: eventNeedsHeadCount.value ? eventForm.headCount : null,
      weightKg: eventNeedsWeight.value ? eventForm.weightKg : null,
      notes: eventForm.notes.trim() || undefined
    })
    message.value = `Event recorded for "${eventGroup.value.name}".`
    showEventModal.value = false
    eventGroup.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="livestock-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>
    <div v-if="message" class="notice notice-success">
      <AppIcon name="livestock" :size="18" /> {{ message }}
      <button type="button" class="dismiss" @click="message = ''">Dismiss</button>
    </div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Animal production</p>
        <h2>Livestock &amp; pasture</h2>
        <p class="subtitle">Track herds, grazing parcels, health events and weight movement across the farm estate.</p>
        <div class="scope-line">
          <span><AppIcon name="livestock" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!canManage" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button v-if="canManage" type="button" class="btn btn-primary" @click="openGroupModal">
          <AppIcon name="plus" :size="16" /> Add livestock group
        </button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading livestock records...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Total head</span><strong>{{ formatNumber(overview.metrics.totalHead) }}</strong><small>Active and quarantined</small></div>
        <div class="stat"><span>Active groups</span><strong>{{ formatNumber(overview.metrics.activeGroups) }}</strong><small>{{ formatNumber(overview.metrics.quarantineGroups) }} in quarantine</small></div>
        <div class="stat"><span>Average weight</span><strong>{{ overview.metrics.averageWeightKg == null ? '—' : `${formatNumber(overview.metrics.averageWeightKg)} kg` }}</strong><small>Weighted live herd average</small></div>
        <div class="stat"><span>Events · 30 days</span><strong>{{ formatNumber(overview.metrics.eventsLast30Days) }}</strong><small>Health, weight and movement</small></div>
      </section>

      <div class="livestock-layout">
        <section class="panel herd-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Herd register</p>
              <h3>Livestock groups</h3>
              <p class="panel-subtitle">{{ filteredGroups.length }} groups · {{ formatNumber(filteredHead) }} head</p>
            </div>
            <div class="panel-tools">
              <label class="search-field">
                <AppIcon name="search" :size="16" />
                <input v-model="search" type="search" placeholder="Search groups, farms..." />
              </label>
              <select v-model="statusFilter" aria-label="Filter livestock status">
                <option value="">All statuses</option>
                <option v-for="status in statuses" :key="status" :value="status">{{ titleCase(status) }}</option>
              </select>
            </div>
          </header>

          <div v-if="filteredGroups.length === 0" class="empty-panel">
            <span class="empty-icon"><AppIcon name="livestock" :size="26" /></span>
            <h4>{{ groups.length === 0 ? 'No livestock groups yet' : 'No groups match this view' }}</h4>
            <p>
              {{ groups.length === 0
                ? 'Register a herd, flock or other group and link it to a farm and grazing parcel.'
                : 'Clear the search and status filter to see all livestock groups.' }}
            </p>
            <button v-if="canManage && groups.length === 0" type="button" class="btn btn-primary" @click="openGroupModal">
              <AppIcon name="plus" :size="16" /> Register first group
            </button>
          </div>

          <div v-else class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Group</th>
                  <th>Farm / grazing</th>
                  <th>Purpose</th>
                  <th class="numeric">Head</th>
                  <th class="numeric">Avg weight</th>
                  <th>Status</th>
                  <th>Last event</th>
                  <th class="actions-col"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="group in filteredGroups" :key="group.id">
                  <td>
                    <div class="group-cell">
                      <span class="group-avatar"><AppIcon name="livestock" :size="16" /></span>
                      <span>
                        <strong>{{ group.name }}</strong>
                        <small>{{ titleCase(group.species) }}<template v-if="group.breed"> · {{ group.breed }}</template></small>
                      </span>
                    </div>
                  </td>
                  <td>
                    <div class="farm-cell">
                      <strong>{{ group.farmName }}</strong>
                      <small>{{ group.grazingParcelName || 'No grazing parcel assigned' }}</small>
                    </div>
                  </td>
                  <td>{{ titleCase(group.purpose) }}</td>
                  <td class="numeric strong-cell">{{ formatNumber(group.headCount) }}</td>
                  <td class="numeric">{{ group.averageWeightKg == null ? '—' : `${formatNumber(group.averageWeightKg)} kg` }}</td>
                  <td><span class="herd-pill" :class="statusClass(group.status)">{{ titleCase(group.status) }}</span></td>
                  <td class="date-cell">{{ group.lastEventDate ? formatDateOnly(group.lastEventDate) : 'No events' }}</td>
                  <td class="actions-cell">
                    <template v-if="canManage">
                      <button type="button" class="row-action" @click="editGroup(group)">Edit</button>
                      <button type="button" class="row-action" @click="openEventModal(group)">Event</button>
                      <button type="button" class="row-action danger" @click="deleteGroup(group)">Delete</button>
                    </template>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <aside class="side-stack">
          <section class="panel">
            <header class="panel-head">
              <div>
                <p class="panel-eyebrow">Composition</p>
                <h3>Species mix</h3>
              </div>
            </header>
            <div v-if="overview.speciesBreakdown.length" class="species-list">
              <div v-for="(item, index) in overview.speciesBreakdown" :key="item.species" class="species-row">
                <div class="species-top">
                  <span><i :style="{ background: ['#4a9d70', '#d49a3a', '#5180a4', '#8f70a4', '#7f9f58'][index % 5] }"></i>{{ titleCase(item.species) }}</span>
                  <strong>{{ formatNumber(item.headCount) }} head</strong>
                </div>
                <div class="bar-track">
                  <span :style="{ width: `${Math.max(3, Math.round((item.headCount / Math.max(1, overview.metrics.totalHead)) * 100))}%`, background: ['#4a9d70', '#d49a3a', '#5180a4', '#8f70a4', '#7f9f58'][index % 5] }"></span>
                </div>
                <small>{{ item.groups }} {{ item.groups === 1 ? 'group' : 'groups' }}</small>
              </div>
            </div>
            <div v-else class="compact-empty"><p>Species composition appears once live groups are registered.</p></div>
          </section>

          <section class="panel">
            <header class="panel-head">
              <div>
                <p class="panel-eyebrow">Activity log</p>
                <h3>Recent livestock events</h3>
              </div>
              <span class="count-badge">{{ overview.recentEvents.length }}</span>
            </header>
            <ul v-if="overview.recentEvents.length" class="event-list">
              <li v-for="event in overview.recentEvents.slice(0, 6)" :key="event.id">
                <span class="event-icon" :class="eventClass(event.eventType)"><AppIcon name="calendar" :size="14" /></span>
                <span class="event-copy">
                  <strong>{{ event.groupName }}</strong>
                  <small>{{ formatDateOnly(event.eventDate) }} · {{ event.farmName }}</small>
                </span>
                <span class="event-pill" :class="eventClass(event.eventType)">{{ titleCase(event.eventType) }}</span>
              </li>
            </ul>
            <div v-else class="compact-empty"><p>No livestock events have been recorded.</p></div>
          </section>
        </aside>
      </div>
    </template>

    <AppModal
      v-if="showGroupModal"
      :title="editingGroup ? 'Edit livestock group' : 'Register a livestock group'"
      subtitle="Link the group to a farm and optional grazing parcel."
      @close="showGroupModal = false"
    >
      <form id="group-form" class="form-grid" @submit.prevent="saveGroup">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2">
          <span>Group name</span>
          <input v-model="groupForm.name" maxlength="120" placeholder="e.g. North Pasture Herd" autofocus />
        </label>
        <label class="field">
          <span>Farm</span>
          <select v-model.number="groupForm.farmId" @change="onFarmChange">
            <option :value="null" disabled>Select a farm</option>
            <option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option>
          </select>
        </label>
        <label class="field">
          <span>Grazing parcel (optional)</span>
          <select v-model.number="groupForm.grazingLandParcelId">
            <option :value="null">No parcel assigned</option>
            <option v-for="parcel in parcelsForFarm" :key="parcel.id" :value="parcel.id">{{ parcel.name }}</option>
          </select>
        </label>
        <label class="field">
          <span>Species</span>
          <select v-model="groupForm.species">
            <option v-for="species in speciesOptions" :key="species" :value="species">{{ species }}</option>
          </select>
        </label>
        <label class="field">
          <span>Breed</span>
          <input v-model="groupForm.breed" maxlength="120" placeholder="e.g. Bonsmara" />
        </label>
        <label class="field">
          <span>Purpose</span>
          <select v-model="groupForm.purpose">
            <option v-for="purpose in purposeOptions" :key="purpose" :value="purpose">{{ titleCase(purpose) }}</option>
          </select>
        </label>
        <label class="field">
          <span>Status</span>
          <select v-model="groupForm.status">
            <option v-for="status in statusOptions" :key="status" :value="status">{{ titleCase(status) }}</option>
          </select>
        </label>
        <label class="field">
          <span>Head count</span>
          <input v-model.number="groupForm.headCount" type="number" min="1" step="1" />
        </label>
        <label class="field">
          <span>Average weight (kg)</span>
          <input v-model.number="groupForm.averageWeightKg" type="number" min="0" step="0.01" />
        </label>
        <label class="field">
          <span>Joined date</span>
          <input v-model="groupForm.joinedDate" type="date" :max="todayIso()" />
        </label>
        <label class="field">
          <span>Notes</span>
          <input v-model="groupForm.notes" maxlength="1000" placeholder="Optional group note" />
        </label>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showGroupModal = false">Cancel</button>
        <button type="submit" form="group-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editingGroup ? 'Save group' : 'Create group' }}</button>
      </template>
    </AppModal>

    <AppModal
      v-if="showEventModal && eventGroup"
      title="Record a livestock event"
      :subtitle="`${eventGroup.name} · ${eventGroup.headCount} head`"
      @close="showEventModal = false"
    >
      <form id="event-form" class="form-grid" @submit.prevent="saveEvent">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field">
          <span>Event type</span>
          <select v-model="eventForm.eventType">
            <option v-for="type in eventTypeOptions" :key="type" :value="type">{{ titleCase(type) }}</option>
          </select>
        </label>
        <label class="field">
          <span>Event date</span>
          <input v-model="eventForm.eventDate" type="date" :max="todayIso()" />
        </label>
        <label v-if="eventNeedsHeadCount" class="field">
          <span>Affected head count</span>
          <input v-model.number="eventForm.headCount" type="number" min="1" :max="eventGroup.headCount" />
        </label>
        <label v-if="eventNeedsWeight" class="field">
          <span>Weight (kg)</span>
          <input v-model.number="eventForm.weightKg" type="number" min="0.01" step="0.01" />
        </label>
        <label class="field span-2">
          <span>Notes</span>
          <input v-model="eventForm.notes" maxlength="1000" placeholder="Treatment, destination, cause or observation" />
        </label>
        <p class="event-note span-2">
          Sale and mortality events reduce the group head count. Weight events update the group's average weight.
        </p>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showEventModal = false">Cancel</button>
        <button type="submit" form="event-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : 'Record event' }}</button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.livestock-page { color: #1d3525; }

.notice {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.85rem;
  padding: 0.7rem 0.85rem;
  border-radius: 9px;
  font-size: 0.78rem;
}

.notice-error { border: 1px solid #f0c9c9; background: #fdf0f0; color: #8d2f2f; }
.notice-success { border: 1px solid #cbe6d2; background: #f0faf2; color: #286b43; }
.notice button { margin-left: auto; border: 0; background: transparent; color: inherit; font-weight: 700; cursor: pointer; text-decoration: underline; }
.notice .dismiss { text-decoration: none; font-size: 0.68rem; }

.page-intro {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1.2rem;
  margin-bottom: 0.85rem;
}

.eyebrow {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin: 0 0 0.3rem;
  color: #6e8273;
  font-size: 0.64rem;
  font-weight: 800;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.eyebrow-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #3b9560;
  box-shadow: 0 0 0 3px rgba(59, 149, 96, 0.12);
}

.page-intro h2 {
  margin: 0;
  color: #15311f;
  font-size: clamp(1.5rem, 2vw, 1.95rem);
  letter-spacing: -0.035em;
}

.subtitle { margin: 0.35rem 0 0; color: #6c7e70; font-size: 0.82rem; }

.scope-line {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
  margin-top: 0.55rem;
  color: #859388;
  font-size: 0.65rem;
  font-weight: 600;
}

.scope-line span { display: inline-flex; align-items: center; gap: 0.3rem; }
.read-only { padding: 0.18rem 0.45rem; border-radius: 999px; background: #f2f5f2; color: #76857a; }
.head-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  min-height: 36px;
  border: 1px solid transparent;
  border-radius: 9px;
  padding: 0.48rem 0.75rem;
  font-size: 0.75rem;
  font-weight: 700;
  text-decoration: none;
  cursor: pointer;
}

.btn-primary { background: #1d5a39; border-color: #1d5a39; color: #fff; box-shadow: 0 5px 13px rgba(29, 90, 57, 0.18); }
.btn-primary:hover { background: #16482d; }
.btn-secondary { background: #fff; border-color: #d9e4da; color: #2f513c; }
.btn-secondary:hover { background: #f7faf7; }
.btn:disabled { opacity: 0.62; cursor: not-allowed; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }

.stat-row {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.65rem;
  margin-bottom: 0.75rem;
}

.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }

.livestock-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.7fr) minmax(300px, 0.8fr);
  gap: 0.8rem;
  align-items: start;
}

.side-stack { display: flex; flex-direction: column; gap: 0.8rem; }

.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.panel-subtitle { margin: 0.18rem 0 0; color: #8a978c; font-size: 0.64rem; }

.panel-tools { display: flex; align-items: center; gap: 0.45rem; }
.search-field { display: flex; align-items: center; gap: 0.4rem; width: 230px; padding: 0.42rem 0.55rem; border: 1px solid #dfe7df; border-radius: 8px; background: #f7faf7; color: #7c8e80; }
.search-field input { min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; color: #24412d; font-size: 0.7rem; }
.panel-tools select { max-width: 126px; border: 1px solid #dfe7df; border-radius: 8px; background: #f7faf7; padding: 0.42rem 0.35rem; color: #3f5b49; font-size: 0.68rem; font-weight: 600; }

.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.7rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.7rem; border-top: 1px solid #eef2ee; color: #4c6353; white-space: nowrap; }

.group-cell { display: flex; align-items: center; gap: 0.45rem; }
.group-avatar { display: grid; place-items: center; width: 28px; height: 28px; flex: 0 0 auto; border-radius: 8px; background: #f2f6ee; color: #6d813f; }
.group-cell > span:last-child, .farm-cell { display: flex; flex-direction: column; }
.group-cell strong, .farm-cell strong { color: #294931; font-size: 0.7rem; }
.group-cell small, .farm-cell small { color: #939f94; font-size: 0.58rem; }

.herd-pill { display: inline-flex; border-radius: 999px; padding: 0.16rem 0.42rem; font-size: 0.56rem; font-weight: 800; white-space: nowrap; }
.herd-active { background: #e8f5ec; color: #28734a; }
.herd-quarantine { background: #fff6df; color: #956315; }
.herd-sold { background: #eaf3fa; color: #346b93; }
.herd-archived { background: #eff1ef; color: #6c786f; }
.herd-neutral { background: #f1f4f1; color: #6d7a70; }

.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.date-cell { color: #607765; font-size: 0.64rem; }
.actions-col { width: 126px; }
.actions-cell { text-align: right; }
.row-action { border: 0; background: transparent; color: #2f704a; font-size: 0.6rem; font-weight: 700; cursor: pointer; }
.row-action + .row-action { margin-left: 0.4rem; }
.row-action.danger { color: #9b4141; }

.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 3rem 1.5rem; text-align: center; }
.empty-panel h4 { margin: 0; color: #2b4a34; font-size: 0.9rem; }
.empty-panel p { max-width: 420px; margin: 0; color: #849287; font-size: 0.72rem; line-height: 1.55; }
.empty-icon { display: grid; place-items: center; width: 50px; height: 50px; border-radius: 14px; background: #f4f7ef; color: #748c4d; }

.species-list { padding: 0.35rem 1rem 0.9rem; }
.species-row { padding: 0.5rem 0; border-top: 1px solid #eef2ee; }
.species-row:first-child { border-top: 0; }
.species-top { display: flex; align-items: center; justify-content: space-between; gap: 0.6rem; color: #415d48; font-size: 0.68rem; font-weight: 700; }
.species-top span { display: inline-flex; align-items: center; gap: 0.4rem; }
.species-top i { width: 7px; height: 7px; border-radius: 2px; }
.species-top strong { font-size: 0.64rem; }
.species-row small { display: block; margin-top: 0.2rem; color: #919d93; font-size: 0.58rem; }
.bar-track { height: 5px; margin-top: 0.35rem; border-radius: 999px; background: #f0f4f0; overflow: hidden; }
.bar-track span { display: block; height: 100%; border-radius: inherit; min-width: 3px; }

.event-list { list-style: none; margin: 0; padding: 0 0.7rem 0.7rem; }
.event-list li { display: flex; align-items: center; gap: 0.5rem; padding: 0.55rem 0.2rem; border-top: 1px solid #eef2ee; }
.event-list li:first-child { border-top: 0; }
.event-icon { display: grid; place-items: center; width: 28px; height: 28px; flex: 0 0 auto; border-radius: 8px; }
.event-copy { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.event-copy strong { overflow: hidden; color: #294931; font-size: 0.68rem; text-overflow: ellipsis; white-space: nowrap; }
.event-copy small { overflow: hidden; color: #929e94; font-size: 0.58rem; text-overflow: ellipsis; white-space: nowrap; }
.event-pill { flex: 0 0 auto; border-radius: 999px; padding: 0.14rem 0.38rem; font-size: 0.54rem; font-weight: 800; }
.event-health { background: #e8f5ec; color: #28734a; }
.event-vaccination { background: #eaf3fa; color: #346b93; }
.event-movement { background: #f2edf8; color: #70518f; }
.event-mortality { background: #fdecec; color: #9b1c1c; }
.event-sale { background: #fff6df; color: #956315; }
.event-weight { background: #f0f4f0; color: #607765; }
.event-other { background: #f1f4f1; color: #6d7a70; }

.count-badge { display: inline-flex; align-items: center; justify-content: center; min-width: 22px; height: 22px; border-radius: 999px; background: #f2f6ee; color: #647d3d; font-size: 0.6rem; font-weight: 800; }
.compact-empty { padding: 1rem; color: #879589; font-size: 0.68rem; line-height: 1.5; }
.compact-empty p { margin: 0; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select, .field textarea { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus, .field textarea:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
.field textarea { resize: vertical; }
.event-note { grid-column: 1 / -1; margin: 0; color: #849287; font-size: 0.65rem; line-height: 1.5; }

@media (max-width: 1150px) {
  .livestock-layout { grid-template-columns: 1fr; }
  .side-stack { display: grid; grid-template-columns: 1fr 1fr; }
}

@media (max-width: 900px) {
  .page-intro { flex-direction: column; align-items: flex-start; }
  .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .side-stack { grid-template-columns: 1fr; }
  .panel-head { flex-direction: column; }
  .panel-tools { width: 100%; }
  .search-field { flex: 1; width: auto; }
}

@media (max-width: 620px) {
  .stat-row { grid-template-columns: 1fr; }
  .form-grid { grid-template-columns: 1fr; }
  .field.span-2 { grid-column: auto; }
}
</style>
