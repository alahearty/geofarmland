<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { enterpriseApi, type Farm, type LandParcel, type LandParcelRequest } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import BoundaryDrawMap from '@/components/BoundaryDrawMap.vue'
import { formatDate, formatHectares, statusClass, titleCase } from '@/utils/enterprise'
import { polygonAreaHectares } from '@/utils/plots'

const route = useRoute()
const router = useRouter()
const workspace = useWorkspaceStore()

const farms = ref<Farm[]>([])
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const activeFarmId = ref<number | null>(null)
const search = ref('')
const highlightedParcelId = ref<number | null>(null)

const showFarmModal = ref(false)
const showParcelModal = ref(false)
const editingParcel = ref<LandParcel | null>(null)
const saving = ref(false)
const formError = ref('')
const parcelPoints = ref<number[][]>([])

const farmForm = reactive({
  name: '',
  code: '',
  region: '',
  country: 'South Africa',
  description: ''
})

const parcelForm = reactive({
  farmId: null as number | null,
  name: '',
  code: '',
  landUse: 'Cropland',
  tenureType: 'Owned',
  status: 'Active',
  notes: ''
})

const landUseOptions = ['Cropland', 'Pasture', 'Orchard', 'Vineyard', 'Greenhouse', 'Aquaculture', 'Conservation', 'Fallow', 'Other']
const tenureOptions = ['Owned', 'Leased', 'Communal', 'StateLease', 'Other']
const statusOptions = ['Active', 'Fallow', 'UnderDevelopment', 'Archived']

const filteredParcels = computed(() => {
  const term = search.value.trim().toLowerCase()
  return parcels.value.filter((parcel) => {
    if (activeFarmId.value && parcel.farmId !== activeFarmId.value) return false
    if (!term) return true
    return parcel.name.toLowerCase().includes(term)
      || (parcel.code ?? '').toLowerCase().includes(term)
      || parcel.farmName.toLowerCase().includes(term)
  })
})

const visibleHectares = computed(() =>
  filteredParcels.value.reduce((total, parcel) => total + Number(parcel.areaHectares), 0)
)
const draftArea = computed(() => polygonAreaHectares(parcelPoints.value))
const selectedFarm = computed(() => farms.value.find((farm) => farm.id === activeFarmId.value) ?? null)

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.message
    || 'The request could not be completed.'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const organizationId = workspace.organizationId
    const [farmResponse, parcelResponse] = await Promise.all([
      enterpriseApi.farms(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    farms.value = farmResponse.data
    parcels.value = parcelResponse.data.items
    if (activeFarmId.value && !farms.value.some((farm) => farm.id === activeFarmId.value)) {
      activeFarmId.value = null
    }
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function openFarmModal() {
  formError.value = ''
  Object.assign(farmForm, { name: '', code: '', region: '', country: 'South Africa', description: '' })
  showFarmModal.value = true
}

async function saveFarm() {
  formError.value = ''
  if (farmForm.name.trim().length < 2) {
    formError.value = 'Enter a farm name of at least two characters.'
    return
  }
  saving.value = true
  try {
    const { data } = await enterpriseApi.createFarm({
      name: farmForm.name.trim(),
      code: farmForm.code.trim() || undefined,
      region: farmForm.region.trim() || undefined,
      country: farmForm.country.trim() || undefined,
      description: farmForm.description.trim() || undefined
    }, workspace.organizationId)
    showFarmModal.value = false
    message.value = `Farm "${data.name}" created.`
    activeFarmId.value = data.id
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

function resetParcelForm(farmId: number | null = null) {
  formError.value = ''
  Object.assign(parcelForm, {
    farmId: farmId ?? activeFarmId.value ?? farms.value[0]?.id ?? null,
    name: '',
    code: '',
    landUse: 'Cropland',
    tenureType: 'Owned',
    status: 'Active',
    notes: ''
  })
  parcelPoints.value = []
  editingParcel.value = null
}

function openParcelModal(farmId: number | null = null) {
  if (farms.value.length === 0) {
    openFarmModal()
    return
  }
  resetParcelForm(farmId)
  showParcelModal.value = true
}

function editParcel(parcel: LandParcel) {
  formError.value = ''
  editingParcel.value = parcel
  Object.assign(parcelForm, {
    farmId: parcel.farmId,
    name: parcel.name,
    code: parcel.code ?? '',
    landUse: parcel.landUse,
    tenureType: parcel.tenureType,
    status: parcel.status,
    notes: parcel.notes ?? ''
  })
  parcelPoints.value = parcel.coordinates.map(([lng, lat]) => [lng, lat])
  showParcelModal.value = true
}

function closeParcelModal() {
  showParcelModal.value = false
  editingParcel.value = null
  formError.value = ''
}

async function saveParcel() {
  formError.value = ''
  if (!parcelForm.farmId) {
    formError.value = 'Select the farm that owns this parcel.'
    return
  }
  if (parcelForm.name.trim().length < 2) {
    formError.value = 'Enter a land parcel name of at least two characters.'
    return
  }
  if (parcelPoints.value.length < 3) {
    formError.value = 'Place at least three boundary points on the map.'
    return
  }

  saving.value = true
  const payload: LandParcelRequest = {
    farmId: parcelForm.farmId,
    name: parcelForm.name.trim(),
    code: parcelForm.code.trim() || undefined,
    landUse: parcelForm.landUse,
    tenureType: parcelForm.tenureType,
    status: parcelForm.status,
    notes: parcelForm.notes.trim() || undefined,
    coordinates: parcelPoints.value
  }
  try {
    if (editingParcel.value) {
      const { data } = await enterpriseApi.updateParcel(editingParcel.value.id, payload)
      message.value = `Land parcel "${data.name}" updated.`
      highlightedParcelId.value = data.id
    } else {
      const { data } = await enterpriseApi.createParcel(payload, workspace.organizationId)
      message.value = `Land parcel "${data.name}" registered.`
      highlightedParcelId.value = data.id
      activeFarmId.value = data.farmId
    }
    showParcelModal.value = false
    editingParcel.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function deleteParcel(parcel: LandParcel) {
  if (!window.confirm(`Delete the land parcel "${parcel.name}"? This removes its boundary from the register.`)) return
  try {
    await enterpriseApi.deleteParcel(parcel.id)
    message.value = `Land parcel "${parcel.name}" deleted.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

function toggleFarm(farmId: number) {
  activeFarmId.value = activeFarmId.value === farmId ? null : farmId
}

function applyQuery() {
  const newType = route.query.new
  if (newType === 'parcel' && workspace.canManageParcels) {
    openParcelModal()
    router.replace({ name: 'Farms' })
    return
  }
  const parcelId = Number(route.query.parcel)
  if (Number.isFinite(parcelId) && parcelId > 0) {
    const parcel = parcels.value.find((item) => item.id === parcelId)
    if (parcel) {
      activeFarmId.value = parcel.farmId
      search.value = parcel.name
      highlightedParcelId.value = parcel.id
    }
    router.replace({ name: 'Farms' })
  }
}

onMounted(async () => {
  await load()
  applyQuery()
})
</script>

<template>
  <div class="farms-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>
    <div v-if="message" class="notice notice-success">
      <AppIcon name="farm" :size="18" /> {{ message }}
      <button type="button" class="dismiss" @click="message = ''">Dismiss</button>
    </div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Enterprise land register</p>
        <h2>Farms &amp; land parcels</h2>
        <p class="subtitle">Maintain the spatial estate, assign land use and keep every parcel connected to its farm.</p>
        <div class="scope-line">
          <span><AppIcon name="farm" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!workspace.canManageParcels" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button v-if="workspace.canManageFarms" type="button" class="btn btn-secondary" @click="openFarmModal">
          <AppIcon name="plus" :size="16" /> Add farm
        </button>
        <button v-if="workspace.canManageParcels" type="button" class="btn btn-primary" @click="openParcelModal()">
          <AppIcon name="plus" :size="16" /> Add land parcel
        </button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">
      <span></span> Loading the enterprise land register...
    </div>

    <template v-else>
      <section class="stat-row">
        <div class="stat"><span>Farms</span><strong>{{ farms.length }}</strong><small>In this organisation</small></div>
        <div class="stat"><span>Land parcels</span><strong>{{ filteredParcels.length }}</strong><small>{{ activeFarmId ? selectedFarm?.name : 'Across all farms' }}</small></div>
        <div class="stat"><span>Mapped area</span><strong>{{ formatHectares(visibleHectares) }}</strong><small>Selected scope</small></div>
        <div class="stat"><span>Active parcels</span><strong>{{ filteredParcels.filter((parcel) => parcel.status === 'Active').length }}</strong><small>Currently in production</small></div>
      </section>

      <section class="farm-grid">
        <button
          type="button"
          class="farm-card all-farms"
          :class="{ active: activeFarmId === null }"
          @click="activeFarmId = null"
        >
          <span class="farm-card-icon"><AppIcon name="layers" :size="20" /></span>
          <span class="farm-card-copy">
            <strong>All farms</strong>
            <small>{{ farms.length }} farms · {{ parcels.length }} parcels</small>
          </span>
          <span class="farm-card-metric">{{ formatHectares(parcels.reduce((total, parcel) => total + Number(parcel.areaHectares), 0)) }}</span>
        </button>

        <button
          v-for="farm in farms"
          :key="farm.id"
          type="button"
          class="farm-card"
          :class="{ active: activeFarmId === farm.id }"
          @click="toggleFarm(farm.id)"
        >
          <span class="farm-card-icon">{{ farm.name.slice(0, 1).toUpperCase() }}</span>
          <span class="farm-card-copy">
            <strong>{{ farm.name }}</strong>
            <small>{{ farm.region || 'Region not set' }}<template v-if="farm.code"> · {{ farm.code }}</template></small>
          </span>
          <span class="farm-card-metric">{{ farm.parcelCount }} <small>parcels</small></span>
        </button>

        <button v-if="workspace.canManageFarms" type="button" class="farm-card add-card" @click="openFarmModal">
          <span class="farm-card-icon"><AppIcon name="plus" :size="19" /></span>
          <span class="farm-card-copy">
            <strong>Register a farm</strong>
            <small>Add another operating unit</small>
          </span>
        </button>
      </section>

      <section class="register-panel">
        <header class="register-head">
          <div>
            <p class="panel-eyebrow">Parcel register</p>
            <h3>{{ selectedFarm ? selectedFarm.name : 'All land parcels' }}</h3>
            <p class="register-subtitle">{{ filteredParcels.length }} parcels · {{ formatHectares(visibleHectares) }}</p>
          </div>
          <div class="register-tools">
            <label class="search-field">
              <AppIcon name="search" :size="16" />
              <input v-model="search" type="search" placeholder="Search parcels..." />
            </label>
            <button v-if="activeFarmId || search" type="button" class="btn btn-secondary small" @click="activeFarmId = null; search = ''">
              Clear filters
            </button>
          </div>
        </header>

        <div v-if="filteredParcels.length === 0" class="empty-register">
          <span class="empty-icon"><AppIcon name="map" :size="26" /></span>
          <h4>{{ parcels.length === 0 ? 'No land parcels registered yet' : 'No parcels match this scope' }}</h4>
          <p>
            {{ parcels.length === 0
              ? 'Create a farm, then draw the first parcel boundary to start building the spatial register.'
              : 'Adjust the farm filter or search term to see other parcels.' }}
          </p>
          <button v-if="workspace.canManageParcels && parcels.length === 0" type="button" class="btn btn-primary" @click="openParcelModal()">
            <AppIcon name="plus" :size="16" /> Register first parcel
          </button>
        </div>

        <div v-else class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Land parcel</th>
                <th>Farm</th>
                <th>Land use</th>
                <th>Tenure</th>
                <th class="numeric">Area</th>
                <th>Status</th>
                <th>Registered</th>
                <th class="actions-col"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="parcel in filteredParcels" :key="parcel.id" :class="{ highlighted: highlightedParcelId === parcel.id }">
                <td>
                  <div class="parcel-identity">
                    <span class="parcel-icon"><AppIcon name="map" :size="15" /></span>
                    <span>
                      <strong>{{ parcel.name }}</strong>
                      <small>{{ parcel.code || 'No parcel code' }}</small>
                    </span>
                  </div>
                </td>
                <td>{{ parcel.farmName }}</td>
                <td>{{ titleCase(parcel.landUse) }}</td>
                <td>{{ titleCase(parcel.tenureType) }}</td>
                <td class="numeric strong-cell">{{ formatHectares(parcel.areaHectares) }}</td>
                <td><span class="status-pill" :class="statusClass(parcel.status)">{{ titleCase(parcel.status) }}</span></td>
                <td class="muted-cell">{{ formatDate(parcel.createdAt) }}</td>
                <td class="actions-cell">
                  <button v-if="workspace.canManageParcels" type="button" class="row-action" @click="editParcel(parcel)">Edit</button>
                  <button v-if="workspace.canManageParcels" type="button" class="row-action danger" @click="deleteParcel(parcel)">Delete</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>

    <AppModal v-if="showFarmModal" title="Register a farm" subtitle="Create an operating unit inside this organisation." @close="showFarmModal = false">
      <form id="farm-form" class="form-grid" @submit.prevent="saveFarm">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2">
          <span>Farm name</span>
          <input v-model="farmForm.name" maxlength="160" placeholder="e.g. Highlands Estate" autofocus />
        </label>
        <label class="field">
          <span>Farm code</span>
          <input v-model="farmForm.code" maxlength="40" placeholder="e.g. HIG-001" />
        </label>
        <label class="field">
          <span>Region</span>
          <input v-model="farmForm.region" maxlength="120" placeholder="e.g. Western Cape" />
        </label>
        <label class="field">
          <span>Country</span>
          <input v-model="farmForm.country" maxlength="80" />
        </label>
        <label class="field span-2">
          <span>Description</span>
          <textarea v-model="farmForm.description" maxlength="1000" rows="3" placeholder="Optional operating context"></textarea>
        </label>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showFarmModal = false">Cancel</button>
        <button type="submit" form="farm-form" class="btn btn-primary" :disabled="saving">
          {{ saving ? 'Saving...' : 'Create farm' }}
        </button>
      </template>
    </AppModal>

    <AppModal
      v-if="showParcelModal"
      :title="editingParcel ? 'Edit land parcel' : 'Register a land parcel'"
      subtitle="Set the parcel details and draw its boundary in WGS84 coordinates."
      size="wide"
      @close="closeParcelModal"
    >
      <form id="parcel-form" class="parcel-form" @submit.prevent="saveParcel">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <div class="form-column">
          <label class="field">
            <span>Farm</span>
            <select v-model.number="parcelForm.farmId">
              <option :value="null" disabled>Select a farm</option>
              <option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option>
            </select>
          </label>
          <label class="field">
            <span>Parcel name</span>
            <input v-model="parcelForm.name" maxlength="120" placeholder="e.g. North Orchard" />
          </label>
          <label class="field">
            <span>Parcel code</span>
            <input v-model="parcelForm.code" maxlength="40" placeholder="e.g. HIG-P01" />
          </label>
          <div class="field-row">
            <label class="field">
              <span>Land use</span>
              <select v-model="parcelForm.landUse">
                <option v-for="option in landUseOptions" :key="option" :value="option">{{ titleCase(option) }}</option>
              </select>
            </label>
            <label class="field">
              <span>Tenure</span>
              <select v-model="parcelForm.tenureType">
                <option v-for="option in tenureOptions" :key="option" :value="option">{{ titleCase(option) }}</option>
              </select>
            </label>
          </div>
          <label class="field">
            <span>Status</span>
            <select v-model="parcelForm.status">
              <option v-for="option in statusOptions" :key="option" :value="option">{{ titleCase(option) }}</option>
            </select>
          </label>
          <label class="field">
            <span>Notes</span>
            <textarea v-model="parcelForm.notes" maxlength="1000" rows="3" placeholder="Optional management notes"></textarea>
          </label>
        </div>

        <div class="draw-column">
          <div class="draw-head">
            <div>
              <span class="field-label">Boundary</span>
              <small>Click the map to place at least three corners.</small>
            </div>
            <span class="area-chip">{{ formatHectares(draftArea) }} · {{ parcelPoints.length }} points</span>
          </div>
          <BoundaryDrawMap v-model="parcelPoints" height="330px" />
          <p class="draw-note">
            Boundaries are validated for ring closure, self-intersections and overlap with other parcels on the same farm.
          </p>
        </div>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="closeParcelModal">Cancel</button>
        <button type="submit" form="parcel-form" class="btn btn-primary" :disabled="saving">
          {{ saving ? 'Saving...' : editingParcel ? 'Save parcel' : 'Register parcel' }}
        </button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.farms-page {
  color: #1d3525;
}

.notice {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.85rem;
  padding: 0.7rem 0.85rem;
  border-radius: 9px;
  font-size: 0.78rem;
}

.notice-error {
  border: 1px solid #f0c9c9;
  background: #fdf0f0;
  color: #8d2f2f;
}

.notice-success {
  border: 1px solid #cbe6d2;
  background: #f0faf2;
  color: #286b43;
}

.notice button {
  margin-left: auto;
  border: 0;
  background: transparent;
  color: inherit;
  font-weight: 700;
  cursor: pointer;
  text-decoration: underline;
}

.notice .dismiss {
  text-decoration: none;
  font-size: 0.68rem;
}

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

.subtitle {
  margin: 0.35rem 0 0;
  color: #6c7e70;
  font-size: 0.82rem;
}

.scope-line {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
  margin-top: 0.55rem;
  color: #859388;
  font-size: 0.65rem;
  font-weight: 600;
}

.scope-line span {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
}

.read-only {
  padding: 0.18rem 0.45rem;
  border-radius: 999px;
  background: #f2f5f2;
  color: #76857a;
}

.head-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

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

.btn-primary {
  background: #1d5a39;
  border-color: #1d5a39;
  color: #fff;
  box-shadow: 0 5px 13px rgba(29, 90, 57, 0.18);
}

.btn-primary:hover {
  background: #16482d;
}

.btn-secondary {
  background: #fff;
  border-color: #d9e4da;
  color: #2f513c;
}

.btn-secondary:hover {
  background: #f7faf7;
}

.btn.small {
  min-height: 31px;
  padding: 0.35rem 0.62rem;
  font-size: 0.68rem;
}

.btn:disabled {
  opacity: 0.62;
  cursor: not-allowed;
}

.loading-state {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 2rem;
  color: #7b8c7f;
  font-size: 0.8rem;
}

.loading-state span {
  width: 14px;
  height: 14px;
  border: 2px solid #c7d8ca;
  border-top-color: #2d7a52;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.stat-row {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.65rem;
  margin-bottom: 0.75rem;
}

.stat {
  padding: 0.75rem 0.85rem;
  border: 1px solid #e1e9e1;
  border-radius: 11px;
  background: #fff;
}

.stat > span {
  display: block;
  color: #829286;
  font-size: 0.58rem;
  font-weight: 800;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.stat strong {
  display: block;
  margin-top: 0.2rem;
  color: #1b3f28;
  font-size: 1.3rem;
  letter-spacing: -0.03em;
}

.stat small {
  display: block;
  margin-top: 0.15rem;
  color: #8d998f;
  font-size: 0.6rem;
}

.farm-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.65rem;
  margin-bottom: 0.8rem;
}

.farm-card {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  min-width: 0;
  padding: 0.7rem 0.8rem;
  border: 1px solid #e1e9e1;
  border-radius: 11px;
  background: #fff;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.farm-card:hover {
  border-color: #bcd4c1;
  box-shadow: 0 5px 16px rgba(26, 75, 43, 0.06);
}

.farm-card.active {
  border-color: #83b491;
  background: #f1f9f3;
  box-shadow: 0 5px 16px rgba(26, 75, 43, 0.08);
}

.farm-card.add-card {
  border-style: dashed;
  background: #fafcfa;
  color: #5f7867;
}

.farm-card-icon {
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  flex: 0 0 auto;
  border-radius: 9px;
  background: #eaf4ec;
  color: #2d764d;
  font-size: 0.76rem;
  font-weight: 800;
}

.farm-card.add-card .farm-card-icon {
  background: #f2f7f2;
  color: #6e8876;
}

.farm-card-copy {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.farm-card-copy strong {
  overflow: hidden;
  color: #24432e;
  font-size: 0.73rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.farm-card-copy small {
  overflow: hidden;
  margin-top: 0.12rem;
  color: #8a978c;
  font-size: 0.6rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.farm-card-metric {
  flex: 0 0 auto;
  color: #3e6049;
  font-size: 0.65rem;
  font-weight: 700;
  white-space: nowrap;
}

.farm-card-metric small {
  color: #8c998e;
  font-size: 0.55rem;
}

.register-panel {
  border: 1px solid #e1e9e1;
  border-radius: 13px;
  background: #fff;
  box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04);
  overflow: hidden;
}

.register-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.9rem 1rem;
  border-bottom: 1px solid #edf2ed;
}

.panel-eyebrow {
  margin: 0 0 0.15rem;
  color: #829286;
  font-size: 0.58rem;
  font-weight: 800;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.register-head h3 {
  margin: 0;
  color: #1c3a26;
  font-size: 0.95rem;
}

.register-subtitle {
  margin: 0.18rem 0 0;
  color: #8a978c;
  font-size: 0.64rem;
}

.register-tools {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.search-field {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  width: 250px;
  padding: 0.42rem 0.55rem;
  border: 1px solid #dfe7df;
  border-radius: 8px;
  background: #f7faf7;
  color: #7c8e80;
}

.search-field input {
  min-width: 0;
  flex: 1;
  border: 0;
  outline: 0;
  background: transparent;
  color: #24412d;
  font-size: 0.7rem;
}

.table-wrap {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.7rem;
}

th {
  padding: 0.55rem 0.8rem;
  color: #8b978d;
  background: #fbfcfb;
  font-size: 0.56rem;
  font-weight: 800;
  letter-spacing: 0.07em;
  text-align: left;
  text-transform: uppercase;
  white-space: nowrap;
}

td {
  padding: 0.58rem 0.8rem;
  border-top: 1px solid #eef2ee;
  color: #4c6353;
  white-space: nowrap;
}

tr.highlighted td {
  background: #f2faf4;
}

.numeric {
  text-align: right;
}

.strong-cell {
  color: #24432e;
  font-weight: 700;
}

.muted-cell {
  color: #89968c;
}

.parcel-identity {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.parcel-icon {
  display: grid;
  place-items: center;
  width: 27px;
  height: 27px;
  flex: 0 0 auto;
  border-radius: 7px;
  background: #eff6f0;
  color: #4a7c5b;
}

.parcel-identity > span:last-child {
  display: flex;
  flex-direction: column;
}

.parcel-identity strong {
  color: #294931;
  font-size: 0.7rem;
}

.parcel-identity small {
  color: #939f94;
  font-size: 0.58rem;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  padding: 0.16rem 0.42rem;
  font-size: 0.56rem;
  font-weight: 800;
  white-space: nowrap;
}

.status-active { background: #e8f5ec; color: #28734a; }
.status-fallow { background: #fff6df; color: #956315; }
.status-development { background: #eaf3fa; color: #346b93; }
.status-archived { background: #eff1ef; color: #6c786f; }
.status-neutral { background: #f1f4f1; color: #6d7a70; }

.actions-col {
  width: 120px;
}

.actions-cell {
  text-align: right;
}

.row-action {
  border: 0;
  background: transparent;
  color: #2f704a;
  font-size: 0.61rem;
  font-weight: 700;
  cursor: pointer;
}

.row-action + .row-action {
  margin-left: 0.45rem;
}

.row-action.danger {
  color: #9b4141;
}

.empty-register {
  display: grid;
  place-items: center;
  gap: 0.45rem;
  padding: 3rem 1.5rem;
  text-align: center;
}

.empty-register h4 {
  margin: 0;
  color: #2b4a34;
  font-size: 0.92rem;
}

.empty-register p {
  max-width: 430px;
  margin: 0;
  color: #849287;
  font-size: 0.72rem;
  line-height: 1.55;
}

.empty-icon {
  display: grid;
  place-items: center;
  width: 50px;
  height: 50px;
  border-radius: 14px;
  background: #f0f6f1;
  color: #558366;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.form-error {
  grid-column: 1 / -1;
  margin: 0;
  padding: 0.55rem 0.65rem;
  border: 1px solid #f0c9c9;
  border-radius: 8px;
  background: #fdf0f0;
  color: #8d2f2f;
  font-size: 0.72rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.28rem;
  font-size: 0.7rem;
  font-weight: 700;
  color: #48604f;
}

.field.span-2 {
  grid-column: 1 / -1;
}

.field input,
.field select,
.field textarea {
  width: 100%;
  border: 1px solid #d8e3d9;
  border-radius: 8px;
  padding: 0.5rem 0.6rem;
  background: #fff;
  color: #263f2e;
  font-size: 0.76rem;
  font-weight: 400;
  outline: none;
}

.field input:focus,
.field select:focus,
.field textarea:focus {
  border-color: #8fbd9d;
  box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08);
}

.field textarea {
  resize: vertical;
}

.parcel-form {
  display: grid;
  grid-template-columns: minmax(240px, 0.72fr) minmax(360px, 1.28fr);
  gap: 1rem;
}

.form-column {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.form-error {
  grid-column: 1 / -1;
}

.field-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.55rem;
}

.draw-column {
  min-width: 0;
}

.draw-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.6rem;
  margin-bottom: 0.4rem;
}

.field-label {
  display: block;
  color: #48604f;
  font-size: 0.7rem;
  font-weight: 700;
}

.draw-head small {
  display: block;
  margin-top: 0.1rem;
  color: #8d998f;
  font-size: 0.6rem;
}

.area-chip {
  flex: 0 0 auto;
  padding: 0.22rem 0.48rem;
  border-radius: 999px;
  background: #edf7ef;
  color: #2d734b;
  font-size: 0.6rem;
  font-weight: 800;
}

.draw-note {
  margin: 0.4rem 0 0;
  color: #8b978d;
  font-size: 0.6rem;
  line-height: 1.45;
}

@media (max-width: 1150px) {
  .farm-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 900px) {
  .stat-row {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .page-intro {
    flex-direction: column;
    align-items: flex-start;
  }

  .register-head {
    flex-direction: column;
  }

  .register-tools {
    width: 100%;
  }

  .search-field {
    flex: 1;
    width: auto;
  }

  .parcel-form {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 620px) {
  .farm-grid,
  .stat-row {
    grid-template-columns: 1fr;
  }

  .form-grid,
  .field-row {
    grid-template-columns: 1fr;
  }

  .field.span-2 {
    grid-column: auto;
  }
}
</style>
