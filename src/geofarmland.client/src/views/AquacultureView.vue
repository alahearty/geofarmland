<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { aquacultureApi, type AquacultureOverview, type AquaculturePond, type AquaculturePondRequest } from '@/api/aquaculture'
import { enterpriseApi, type Farm, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatDateOnly, formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()
const overview = ref<AquacultureOverview | null>(null)
const farms = ref<Farm[]>([])
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const showModal = ref(false)
const editing = ref<AquaculturePond | null>(null)
const saving = ref(false)
const formError = ref('')

const form = reactive({
  farmId: null as number | null,
  landParcelId: null as number | null,
  name: '',
  waterType: 'Freshwater',
  systemType: 'Pond',
  areaM2: null as number | null,
  species: '',
  stockCount: 0,
  averageWeightG: null as number | null,
  lastStockedDate: '',
  status: 'Active',
  notes: ''
})

const waterTypes = ['Freshwater', 'Brackish', 'Marine']
const systemTypes = ['Pond', 'Cage', 'Tank', 'Raceway', 'Other']
const statuses = ['Active', 'Fallow', 'Maintenance', 'Archived']

const todayIso = () => {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}
const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const ponds = computed(() => overview.value?.ponds ?? [])
const parcelsForFarm = computed(() => parcels.value.filter((parcel) => !form.farmId || parcel.farmId === form.farmId))
const filteredPonds = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return ponds.value
  return ponds.value.filter((pond) =>
    pond.name.toLowerCase().includes(term)
    || (pond.species ?? '').toLowerCase().includes(term)
    || pond.farmName.toLowerCase().includes(term))
})

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message || candidate.response?.data?.detail || candidate.message || 'The request could not be completed.'
}

function statusClass(value: string) {
  switch (value.toLowerCase()) {
    case 'active': return 'pond-active'
    case 'fallow': return 'pond-fallow'
    case 'maintenance': return 'pond-maintenance'
    default: return 'pond-archived'
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const organizationId = workspace.organizationId
    const [aquaResponse, farmResponse, parcelResponse] = await Promise.all([
      aquacultureApi.overview(organizationId),
      enterpriseApi.farms(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = aquaResponse.data
    farms.value = farmResponse.data
    parcels.value = parcelResponse.data.items
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function openModal() {
  formError.value = ''
  editing.value = null
  const firstFarm = farms.value[0]?.id ?? null
  const firstParcel = parcels.value.find((parcel) => parcel.farmId === firstFarm)
  Object.assign(form, {
    farmId: firstFarm,
    landParcelId: firstParcel?.id ?? null,
    name: '',
    waterType: 'Freshwater',
    systemType: 'Pond',
    areaM2: null,
    species: '',
    stockCount: 0,
    averageWeightG: null,
    lastStockedDate: '',
    status: 'Active',
    notes: ''
  })
  showModal.value = true
}

function editPond(pond: AquaculturePond) {
  formError.value = ''
  editing.value = pond
  Object.assign(form, {
    farmId: pond.farmId,
    landParcelId: pond.landParcelId ?? null,
    name: pond.name,
    waterType: pond.waterType,
    systemType: pond.systemType,
    areaM2: Number(pond.areaM2),
    species: pond.species ?? '',
    stockCount: pond.stockCount,
    averageWeightG: pond.averageWeightG == null ? null : Number(pond.averageWeightG),
    lastStockedDate: pond.lastStockedDate ?? '',
    status: pond.status,
    notes: pond.notes ?? ''
  })
  showModal.value = true
}

function onFarmChange() {
  form.landParcelId = parcelsForFarm.value[0]?.id ?? null
}

async function save() {
  formError.value = ''
  if (!form.farmId) {
    formError.value = 'Select the farm for this pond.'
    return
  }
  if (form.name.trim().length < 2) {
    formError.value = 'Enter a pond name of at least two characters.'
    return
  }
  if (!form.areaM2 || form.areaM2 <= 0) {
    formError.value = 'Enter a pond area greater than zero.'
    return
  }
  saving.value = true
  const payload: AquaculturePondRequest = {
    farmId: form.farmId,
    landParcelId: form.landParcelId,
    name: form.name.trim(),
    waterType: form.waterType,
    systemType: form.systemType,
    areaM2: form.areaM2,
    species: form.species.trim() || undefined,
    stockCount: form.stockCount,
    averageWeightG: form.averageWeightG,
    lastStockedDate: form.lastStockedDate || null,
    status: form.status,
    notes: form.notes.trim() || undefined
  }
  try {
    if (editing.value) {
      const { data } = await aquacultureApi.update(editing.value.id, payload)
      message.value = `Pond "${data.name}" updated.`
    } else {
      const { data } = await aquacultureApi.create(payload, workspace.organizationId)
      message.value = `Pond "${data.name}" registered.`
    }
    showModal.value = false
    editing.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function remove(pond: AquaculturePond) {
  if (!window.confirm(`Delete the aquaculture pond "${pond.name}"?`)) return
  try {
    await aquacultureApi.remove(pond.id)
    message.value = `Pond "${pond.name}" deleted.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

onMounted(load)
</script>

<template>
  <div class="aqua-page">
    <div v-if="error" class="notice notice-error"><AppIcon name="warning" :size="18" /> {{ error }}<button type="button" @click="load">Retry</button></div>
    <div v-if="message" class="notice notice-success"><AppIcon name="aquaculture" :size="18" /> {{ message }}<button type="button" class="dismiss" @click="message = ''">Dismiss</button></div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Aquatic production</p>
        <h2>Aquaculture</h2>
        <p class="subtitle">Register fishponds, cages and tanks with current stock, average weight and production status.</p>
        <div class="scope-line"><span><AppIcon name="aquaculture" :size="14" /> {{ workspace.activeOrganization?.name }}</span><span v-if="!canManage" class="read-only">Read-only role</span></div>
      </div>
      <button v-if="canManage" type="button" class="btn btn-primary" @click="openModal"><AppIcon name="plus" :size="16" /> Add pond</button>
    </section>

    <div v-if="loading" class="loading-state">Loading aquaculture register...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Ponds &amp; systems</span><strong>{{ formatNumber(overview.metrics.ponds) }}</strong><small>{{ formatNumber(overview.metrics.activePonds) }} active</small></div>
        <div class="stat"><span>Water area</span><strong>{{ formatNumber(overview.metrics.waterAreaM2) }} m²</strong><small>Registered production area</small></div>
        <div class="stat"><span>Stocked systems</span><strong>{{ formatNumber(overview.metrics.stockedPonds) }}</strong><small>Currently holding stock</small></div>
        <div class="stat"><span>Total stock</span><strong>{{ formatNumber(overview.metrics.totalStock) }}</strong><small>Animals across all ponds</small></div>
      </section>

      <section class="panel">
        <header class="panel-head">
          <div><p class="panel-eyebrow">Aquatic assets</p><h3>Pond register</h3><p class="panel-subtitle">{{ filteredPonds.length }} systems in view</p></div>
          <label class="search-field"><AppIcon name="search" :size="16" /><input v-model="search" type="search" placeholder="Search ponds or species..." /></label>
        </header>
        <div v-if="filteredPonds.length === 0" class="empty-panel"><span class="empty-icon"><AppIcon name="aquaculture" :size="26" /></span><p>{{ ponds.length === 0 ? 'No aquaculture ponds yet. Register a pond, cage or tank to begin tracking aquatic production.' : 'No ponds match the current search.' }}</p><button v-if="canManage && ponds.length === 0" type="button" class="btn btn-primary" @click="openModal">Add first pond</button></div>
        <div v-else class="table-wrap">
          <table>
            <thead><tr><th>Pond</th><th>Farm / parcel</th><th>System</th><th class="numeric">Area</th><th>Species</th><th class="numeric">Stock</th><th class="numeric">Avg weight</th><th>Status</th><th class="actions-col"></th></tr></thead>
            <tbody>
              <tr v-for="pond in filteredPonds" :key="pond.id">
                <td><div class="pond-cell"><span class="pond-icon"><AppIcon name="aquaculture" :size="16" /></span><span><strong>{{ pond.name }}</strong><small>{{ titleCase(pond.waterType) }} water</small></span></div></td>
                <td><div class="cell-stack"><strong>{{ pond.farmName }}</strong><small>{{ pond.landParcelName || 'No parcel link' }}</small></div></td>
                <td>{{ titleCase(pond.systemType) }}</td>
                <td class="numeric">{{ formatNumber(pond.areaM2) }} m²</td>
                <td>{{ pond.species || '—' }}</td>
                <td class="numeric strong-cell">{{ formatNumber(pond.stockCount) }}</td>
                <td class="numeric">{{ pond.averageWeightG == null ? '—' : `${formatNumber(pond.averageWeightG)} g` }}</td>
                <td><span class="pond-pill" :class="statusClass(pond.status)">{{ titleCase(pond.status) }}</span></td>
                <td class="actions-cell"><template v-if="canManage"><button type="button" class="row-action" @click="editPond(pond)">Edit</button><button type="button" class="row-action danger" @click="remove(pond)">Delete</button></template></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>

    <AppModal v-if="showModal" :title="editing ? 'Edit aquaculture pond' : 'Register an aquaculture pond'" subtitle="Capture the production system, area and current stock." @close="showModal = false">
      <form id="aqua-form" class="form-grid" @submit.prevent="save">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2"><span>Pond name</span><input v-model="form.name" maxlength="120" placeholder="e.g. East Tilapia Pond" autofocus /></label>
        <label class="field"><span>Farm</span><select v-model.number="form.farmId" @change="onFarmChange"><option :value="null" disabled>Select a farm</option><option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option></select></label>
        <label class="field"><span>Land parcel (optional)</span><select v-model.number="form.landParcelId"><option :value="null">No parcel link</option><option v-for="parcel in parcelsForFarm" :key="parcel.id" :value="parcel.id">{{ parcel.name }}</option></select></label>
        <label class="field"><span>Water type</span><select v-model="form.waterType"><option v-for="type in waterTypes" :key="type" :value="type">{{ type }}</option></select></label>
        <label class="field"><span>System type</span><select v-model="form.systemType"><option v-for="type in systemTypes" :key="type" :value="type">{{ titleCase(type) }}</option></select></label>
        <label class="field"><span>Area (m²)</span><input v-model.number="form.areaM2" type="number" min="0.01" step="0.01" /></label>
        <label class="field"><span>Status</span><select v-model="form.status"><option v-for="status in statuses" :key="status" :value="status">{{ titleCase(status) }}</option></select></label>
        <label class="field"><span>Species</span><input v-model="form.species" maxlength="120" placeholder="e.g. Tilapia" /></label>
        <label class="field"><span>Stock count</span><input v-model.number="form.stockCount" type="number" min="0" step="1" /></label>
        <label class="field"><span>Average weight (g)</span><input v-model.number="form.averageWeightG" type="number" min="0" step="0.01" /></label>
        <label class="field"><span>Last stocked date</span><input v-model="form.lastStockedDate" type="date" :max="todayIso()" /></label>
        <label class="field span-2"><span>Notes</span><input v-model="form.notes" maxlength="1000" placeholder="Optional production notes" /></label>
      </form>
      <template #footer><button type="button" class="btn btn-secondary" @click="showModal = false">Cancel</button><button type="submit" form="aqua-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editing ? 'Save pond' : 'Register pond' }}</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.aqua-page { color: #1d3525; }
.notice { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.85rem; padding: 0.7rem 0.85rem; border-radius: 9px; font-size: 0.78rem; }
.notice-error { border: 1px solid #f0c9c9; background: #fdf0f0; color: #8d2f2f; }
.notice-success { border: 1px solid #cbe6d2; background: #f0faf2; color: #286b43; }
.notice button { margin-left: auto; border: 0; background: transparent; color: inherit; font-weight: 700; cursor: pointer; text-decoration: underline; }
.notice .dismiss { text-decoration: none; font-size: 0.68rem; }
.page-intro { display: flex; align-items: flex-end; justify-content: space-between; gap: 1.2rem; margin-bottom: 0.85rem; }
.eyebrow { display: flex; align-items: center; gap: 0.4rem; margin: 0 0 0.3rem; color: #6e8273; font-size: 0.64rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.eyebrow-dot { width: 6px; height: 6px; border-radius: 50%; background: #3b9560; box-shadow: 0 0 0 3px rgba(59, 149, 96, 0.12); }
.page-intro h2 { margin: 0; color: #15311f; font-size: clamp(1.5rem, 2vw, 1.95rem); letter-spacing: -0.035em; }
.subtitle { margin: 0.35rem 0 0; color: #6c7e70; font-size: 0.82rem; }
.scope-line { display: flex; flex-wrap: wrap; gap: 0.7rem; margin-top: 0.55rem; color: #859388; font-size: 0.65rem; font-weight: 600; }
.scope-line span { display: inline-flex; align-items: center; gap: 0.3rem; }
.read-only { padding: 0.18rem 0.45rem; border-radius: 999px; background: #f2f5f2; color: #76857a; }
.btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; min-height: 36px; border: 1px solid transparent; border-radius: 9px; padding: 0.48rem 0.75rem; font-size: 0.75rem; font-weight: 700; cursor: pointer; }
.btn-primary { background: #1d5a39; border-color: #1d5a39; color: #fff; }
.btn-primary:hover { background: #16482d; }
.btn-secondary { background: #fff; border-color: #d9e4da; color: #2f513c; }
.btn:disabled { opacity: 0.62; cursor: not-allowed; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }
.stat-row { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.65rem; margin-bottom: 0.75rem; }
.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }
.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.panel-subtitle { margin: 0.18rem 0 0; color: #8a978c; font-size: 0.64rem; }
.search-field { display: flex; align-items: center; gap: 0.4rem; width: 250px; padding: 0.42rem 0.55rem; border: 1px solid #dfe7df; border-radius: 8px; background: #f7faf7; color: #7c8e80; }
.search-field input { min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; color: #24412d; font-size: 0.7rem; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.8rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.8rem; border-top: 1px solid #eef2ee; color: #4c6353; white-space: nowrap; }
.pond-cell { display: flex; align-items: center; gap: 0.45rem; }
.pond-icon { display: grid; place-items: center; width: 28px; height: 28px; flex: 0 0 auto; border-radius: 8px; background: #eaf4f8; color: #397899; }
.pond-cell > span:last-child, .cell-stack { display: flex; flex-direction: column; }
.pond-cell strong, .cell-stack strong { color: #294931; font-size: 0.7rem; }
.pond-cell small, .cell-stack small { color: #939f94; font-size: 0.58rem; }
.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.pond-pill { display: inline-flex; border-radius: 999px; padding: 0.14rem 0.4rem; font-size: 0.55rem; font-weight: 800; }
.pond-active { background: #e8f5ec; color: #28734a; }
.pond-fallow { background: #fff6df; color: #956315; }
.pond-maintenance { background: #eaf3fa; color: #346b93; }
.pond-archived { background: #eff1ef; color: #6c786f; }
.actions-col { width: 100px; }
.actions-cell { text-align: right; }
.row-action { border: 0; background: transparent; color: #2f704a; font-size: 0.6rem; font-weight: 700; cursor: pointer; }
.row-action + .row-action { margin-left: 0.4rem; }
.row-action.danger { color: #9b4141; }
.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 2.5rem 1.5rem; text-align: center; }
.empty-panel p { max-width: 420px; margin: 0; color: #849287; font-size: 0.7rem; line-height: 1.5; }
.empty-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; background: #eaf4f8; color: #397899; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
@media (max-width: 900px) { .page-intro { flex-direction: column; align-items: flex-start; } .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } .panel-head { flex-direction: column; } .search-field { width: 100%; } }
@media (max-width: 620px) { .stat-row { grid-template-columns: 1fr; } .form-grid { grid-template-columns: 1fr; } .field.span-2 { grid-column: auto; } }
</style>
