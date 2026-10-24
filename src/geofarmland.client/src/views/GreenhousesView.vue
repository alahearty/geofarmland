<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { greenhousesApi, type GreenhouseOverview, type GreenhouseUnit, type GreenhouseUnitRequest } from '@/api/greenhouses'
import { enterpriseApi, type Farm, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()
const overview = ref<GreenhouseOverview | null>(null)
const farms = ref<Farm[]>([])
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const showModal = ref(false)
const editing = ref<GreenhouseUnit | null>(null)
const saving = ref(false)
const formError = ref('')

const form = reactive({
  farmId: null as number | null,
  landParcelId: null as number | null,
  name: '',
  structureType: 'Greenhouse',
  areaM2: null as number | null,
  currentCrop: '',
  status: 'Active',
  notes: ''
})

const structureTypes = ['Greenhouse', 'ShadeNet', 'Tunnel', 'Nursery', 'Other']
const statuses = ['Active', 'Fallow', 'Maintenance', 'Archived']

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const units = computed(() => overview.value?.units ?? [])
const structureTypeCount = computed(() => new Set(units.value.map((unit) => unit.structureType)).size)
const parcelsForFarm = computed(() => parcels.value.filter((parcel) => !form.farmId || parcel.farmId === form.farmId))
const filteredUnits = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return units.value
  return units.value.filter((unit) =>
    unit.name.toLowerCase().includes(term)
    || (unit.currentCrop ?? '').toLowerCase().includes(term)
    || unit.farmName.toLowerCase().includes(term))
})

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message || candidate.response?.data?.detail || candidate.message || 'The request could not be completed.'
}

function statusClass(value: string) {
  switch (value.toLowerCase()) {
    case 'active': return 'gh-active'
    case 'fallow': return 'gh-fallow'
    case 'maintenance': return 'gh-maintenance'
    default: return 'gh-archived'
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const organizationId = workspace.organizationId
    const [greenhouseResponse, farmResponse, parcelResponse] = await Promise.all([
      greenhousesApi.overview(organizationId),
      enterpriseApi.farms(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = greenhouseResponse.data
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
    structureType: 'Greenhouse',
    areaM2: null,
    currentCrop: '',
    status: 'Active',
    notes: ''
  })
  showModal.value = true
}

function editUnit(unit: GreenhouseUnit) {
  formError.value = ''
  editing.value = unit
  Object.assign(form, {
    farmId: unit.farmId,
    landParcelId: unit.landParcelId ?? null,
    name: unit.name,
    structureType: unit.structureType,
    areaM2: Number(unit.areaM2),
    currentCrop: unit.currentCrop ?? '',
    status: unit.status,
    notes: unit.notes ?? ''
  })
  showModal.value = true
}

function onFarmChange() {
  form.landParcelId = parcelsForFarm.value[0]?.id ?? null
}

async function save() {
  formError.value = ''
  if (!form.farmId) {
    formError.value = 'Select the farm for this unit.'
    return
  }
  if (form.name.trim().length < 2) {
    formError.value = 'Enter a unit name of at least two characters.'
    return
  }
  if (!form.areaM2 || form.areaM2 <= 0) {
    formError.value = 'Enter a covered area greater than zero.'
    return
  }
  saving.value = true
  const payload: GreenhouseUnitRequest = {
    farmId: form.farmId,
    landParcelId: form.landParcelId,
    name: form.name.trim(),
    structureType: form.structureType,
    areaM2: form.areaM2,
    currentCrop: form.currentCrop.trim() || undefined,
    status: form.status,
    notes: form.notes.trim() || undefined
  }
  try {
    if (editing.value) {
      const { data } = await greenhousesApi.update(editing.value.id, payload)
      message.value = `Unit "${data.name}" updated.`
    } else {
      const { data } = await greenhousesApi.create(payload, workspace.organizationId)
      message.value = `Unit "${data.name}" registered.`
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

async function remove(unit: GreenhouseUnit) {
  if (!window.confirm(`Delete the greenhouse unit "${unit.name}"?`)) return
  try {
    await greenhousesApi.remove(unit.id)
    message.value = `Unit "${unit.name}" deleted.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

onMounted(load)
</script>

<template>
  <div class="gh-page">
    <div v-if="error" class="notice notice-error"><AppIcon name="warning" :size="18" /> {{ error }}<button type="button" @click="load">Retry</button></div>
    <div v-if="message" class="notice notice-success"><AppIcon name="greenhouse" :size="18" /> {{ message }}<button type="button" class="dismiss" @click="message = ''">Dismiss</button></div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Protected production</p>
        <h2>Greenhouses &amp; plantations</h2>
        <p class="subtitle">Register greenhouses, shade nets, tunnels and nurseries with covered area and current crop focus.</p>
        <div class="scope-line"><span><AppIcon name="greenhouse" :size="14" /> {{ workspace.activeOrganization?.name }}</span><span v-if="!canManage" class="read-only">Read-only role</span></div>
      </div>
      <button v-if="canManage" type="button" class="btn btn-primary" @click="openModal"><AppIcon name="plus" :size="16" /> Add unit</button>
    </section>

    <div v-if="loading" class="loading-state">Loading greenhouse register...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Protected units</span><strong>{{ formatNumber(overview.metrics.units) }}</strong><small>{{ formatNumber(overview.metrics.activeUnits) }} active</small></div>
        <div class="stat"><span>Covered area</span><strong>{{ formatNumber(overview.metrics.coveredAreaM2) }} m²</strong><small>Greenhouse and shade area</small></div>
        <div class="stat"><span>Cropped units</span><strong>{{ formatNumber(overview.metrics.croppedUnits) }}</strong><small>With a recorded crop focus</small></div>
        <div class="stat"><span>Structure types</span><strong>{{ structureTypeCount }}</strong><small>Across the register</small></div>
      </section>

      <section class="panel">
        <header class="panel-head">
          <div><p class="panel-eyebrow">Protected assets</p><h3>Greenhouse register</h3><p class="panel-subtitle">{{ filteredUnits.length }} units in view</p></div>
          <label class="search-field"><AppIcon name="search" :size="16" /><input v-model="search" type="search" placeholder="Search units or crops..." /></label>
        </header>
        <div v-if="filteredUnits.length === 0" class="empty-panel"><span class="empty-icon"><AppIcon name="greenhouse" :size="26" /></span><p>{{ units.length === 0 ? 'No greenhouse units yet. Register a protected structure to begin tracking production.' : 'No units match the current search.' }}</p><button v-if="canManage && units.length === 0" type="button" class="btn btn-primary" @click="openModal">Add first unit</button></div>
        <div v-else class="table-wrap">
          <table>
            <thead><tr><th>Unit</th><th>Farm / parcel</th><th>Structure</th><th class="numeric">Area</th><th>Current crop</th><th>Status</th><th class="actions-col"></th></tr></thead>
            <tbody>
              <tr v-for="unit in filteredUnits" :key="unit.id">
                <td><div class="unit-cell"><span class="unit-icon"><AppIcon name="greenhouse" :size="16" /></span><strong>{{ unit.name }}</strong></div></td>
                <td><div class="cell-stack"><strong>{{ unit.farmName }}</strong><small>{{ unit.landParcelName || 'No parcel link' }}</small></div></td>
                <td>{{ titleCase(unit.structureType) }}</td>
                <td class="numeric strong-cell">{{ formatNumber(unit.areaM2) }} m²</td>
                <td>{{ unit.currentCrop || '—' }}</td>
                <td><span class="gh-pill" :class="statusClass(unit.status)">{{ titleCase(unit.status) }}</span></td>
                <td class="actions-cell"><template v-if="canManage"><button type="button" class="row-action" @click="editUnit(unit)">Edit</button><button type="button" class="row-action danger" @click="remove(unit)">Delete</button></template></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>

    <AppModal v-if="showModal" :title="editing ? 'Edit greenhouse unit' : 'Register a greenhouse unit'" subtitle="Capture the structure, area and current crop focus." @close="showModal = false">
      <form id="greenhouse-form" class="form-grid" @submit.prevent="save">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2"><span>Unit name</span><input v-model="form.name" maxlength="120" placeholder="e.g. North Tomato Tunnel" autofocus /></label>
        <label class="field"><span>Farm</span><select v-model.number="form.farmId" @change="onFarmChange"><option :value="null" disabled>Select a farm</option><option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option></select></label>
        <label class="field"><span>Land parcel (optional)</span><select v-model.number="form.landParcelId"><option :value="null">No parcel link</option><option v-for="parcel in parcelsForFarm" :key="parcel.id" :value="parcel.id">{{ parcel.name }}</option></select></label>
        <label class="field"><span>Structure type</span><select v-model="form.structureType"><option v-for="type in structureTypes" :key="type" :value="type">{{ titleCase(type) }}</option></select></label>
        <label class="field"><span>Status</span><select v-model="form.status"><option v-for="status in statuses" :key="status" :value="status">{{ titleCase(status) }}</option></select></label>
        <label class="field"><span>Covered area (m²)</span><input v-model.number="form.areaM2" type="number" min="0.01" step="0.01" /></label>
        <label class="field"><span>Current crop</span><input v-model="form.currentCrop" maxlength="120" placeholder="e.g. Tomatoes" /></label>
        <label class="field span-2"><span>Notes</span><input v-model="form.notes" maxlength="1000" placeholder="Optional structure notes" /></label>
      </form>
      <template #footer><button type="button" class="btn btn-secondary" @click="showModal = false">Cancel</button><button type="submit" form="greenhouse-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editing ? 'Save unit' : 'Register unit' }}</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.gh-page { color: #1d3525; }
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
.unit-cell { display: flex; align-items: center; gap: 0.45rem; }
.unit-icon { display: grid; place-items: center; width: 28px; height: 28px; flex: 0 0 auto; border-radius: 8px; background: #eef5ed; color: #4f7c55; }
.unit-cell strong { color: #294931; font-size: 0.7rem; }
.cell-stack { display: flex; flex-direction: column; }
.cell-stack strong { color: #294931; font-size: 0.7rem; }
.cell-stack small { color: #939f94; font-size: 0.58rem; }
.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.gh-pill { display: inline-flex; border-radius: 999px; padding: 0.14rem 0.4rem; font-size: 0.55rem; font-weight: 800; }
.gh-active { background: #e8f5ec; color: #28734a; }
.gh-fallow { background: #fff6df; color: #956315; }
.gh-maintenance { background: #eaf3fa; color: #346b93; }
.gh-archived { background: #eff1ef; color: #6c786f; }
.actions-col { width: 100px; }
.actions-cell { text-align: right; }
.row-action { border: 0; background: transparent; color: #2f704a; font-size: 0.6rem; font-weight: 700; cursor: pointer; }
.row-action + .row-action { margin-left: 0.4rem; }
.row-action.danger { color: #9b4141; }
.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 2.5rem 1.5rem; text-align: center; }
.empty-panel p { max-width: 420px; margin: 0; color: #849287; font-size: 0.7rem; line-height: 1.5; }
.empty-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; background: #eef5ed; color: #4f7c55; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
@media (max-width: 900px) { .page-intro { flex-direction: column; align-items: flex-start; } .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } .panel-head { flex-direction: column; } .search-field { width: 100%; } }
@media (max-width: 620px) { .stat-row { grid-template-columns: 1fr; } .form-grid { grid-template-columns: 1fr; } .field.span-2 { grid-column: auto; } }
</style>
