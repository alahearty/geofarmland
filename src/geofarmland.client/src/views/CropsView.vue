<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  cropsApi,
  type CropOverview,
  type CropSeason,
  type CropSeasonRequest
} from '@/api/crops'
import { enterpriseApi, type Farm, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import {
  cropStatusClass,
  formatDateOnly,
  formatHectares,
  formatNumber,
  percentage,
  titleCase
} from '@/utils/enterprise'

const workspace = useWorkspaceStore()

const overview = ref<CropOverview | null>(null)
const farms = ref<Farm[]>([])
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const statusFilter = ref('')

const showCropModal = ref(false)
const showSeasonModal = ref(false)
const showHarvestModal = ref(false)
const editingSeason = ref<CropSeason | null>(null)
const harvestSeason = ref<CropSeason | null>(null)
const saving = ref(false)
const formError = ref('')

const cropForm = reactive({
  name: '',
  category: 'Other',
  variety: '',
  description: '',
  isActive: true
})

const seasonForm = reactive({
  cropId: null as number | null,
  farmId: null as number | null,
  landParcelId: null as number | null,
  seasonName: '',
  status: 'Planned',
  plantingDate: '',
  expectedHarvestDate: '',
  areaHectares: null as number | null,
  expectedYieldTons: null as number | null,
  notes: ''
})

const harvestForm = reactive({
  harvestDate: '',
  quantityTons: null as number | null,
  grade: '',
  notes: ''
})

const categoryOptions = ['Cereal', 'Vegetable', 'Fruit', 'Legume', 'Fodder', 'Industrial', 'Other']
const seasonStatusOptions = ['Planned', 'Planted', 'Growing', 'Harvested', 'Failed']
const gradeOptions = ['A', 'B', 'C', 'Reject']

const todayIso = () => {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const seasons = computed(() => overview.value?.seasons ?? [])
const crops = computed(() => overview.value?.crops ?? [])
const statuses = computed(() => [...new Set(seasons.value.map((season) => season.status))].sort())

const filteredSeasons = computed(() => {
  const term = search.value.trim().toLowerCase()
  return seasons.value.filter((season) => {
    if (statusFilter.value && season.status !== statusFilter.value) return false
    if (!term) return true
    return season.cropName.toLowerCase().includes(term)
      || (season.variety ?? '').toLowerCase().includes(term)
      || season.seasonName.toLowerCase().includes(term)
      || season.farmName.toLowerCase().includes(term)
      || season.landParcelName.toLowerCase().includes(term)
  })
})

const filteredHectares = computed(() =>
  filteredSeasons.value.reduce((total, season) => total + Number(season.areaHectares), 0)
)

const upcomingHarvests = computed(() =>
  [...seasons.value]
    .filter((season) => season.expectedHarvestDate && ['Planned', 'Planted', 'Growing'].includes(season.status))
    .sort((left, right) => String(left.expectedHarvestDate).localeCompare(String(right.expectedHarvestDate)))
    .slice(0, 4)
)

const parcelsForFarm = computed(() =>
  parcels.value.filter((parcel) => !seasonForm.farmId || parcel.farmId === seasonForm.farmId)
)

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
    const [cropResponse, farmResponse, parcelResponse] = await Promise.all([
      cropsApi.overview(organizationId),
      enterpriseApi.farms(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = cropResponse.data
    farms.value = farmResponse.data
    parcels.value = parcelResponse.data.items
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function openCropModal() {
  formError.value = ''
  Object.assign(cropForm, {
    name: '',
    category: 'Other',
    variety: '',
    description: '',
    isActive: true
  })
  showCropModal.value = true
}

async function saveCrop() {
  formError.value = ''
  if (cropForm.name.trim().length < 2) {
    formError.value = 'Enter a crop name of at least two characters.'
    return
  }
  saving.value = true
  try {
    const { data } = await cropsApi.createCrop({
      name: cropForm.name.trim(),
      category: cropForm.category,
      variety: cropForm.variety.trim() || undefined,
      description: cropForm.description.trim() || undefined,
      isActive: cropForm.isActive
    }, workspace.organizationId)
    showCropModal.value = false
    message.value = `Crop "${data.name}" added to the catalogue.`
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

function resetSeasonForm() {
  formError.value = ''
  Object.assign(seasonForm, {
    cropId: crops.value[0]?.id ?? null,
    farmId: farms.value[0]?.id ?? null,
    landParcelId: parcels.value.find((parcel) => parcel.farmId === farms.value[0]?.id)?.id ?? null,
    seasonName: '',
    status: 'Planned',
    plantingDate: todayIso(),
    expectedHarvestDate: '',
    areaHectares: parcels.value.find((parcel) => parcel.farmId === farms.value[0]?.id)?.areaHectares ?? null,
    expectedYieldTons: null,
    notes: ''
  })
  editingSeason.value = null
}

function openSeasonModal() {
  if (crops.value.length === 0) {
    message.value = 'Add a crop to the catalogue before creating a season.'
    openCropModal()
    return
  }
  resetSeasonForm()
  showSeasonModal.value = true
}

function editSeason(season: CropSeason) {
  formError.value = ''
  editingSeason.value = season
  Object.assign(seasonForm, {
    cropId: season.cropId,
    farmId: season.farmId,
    landParcelId: season.landParcelId,
    seasonName: season.seasonName,
    status: season.status,
    plantingDate: season.plantingDate,
    expectedHarvestDate: season.expectedHarvestDate ?? '',
    areaHectares: Number(season.areaHectares),
    expectedYieldTons: season.expectedYieldTons == null ? null : Number(season.expectedYieldTons),
    notes: season.notes ?? ''
  })
  showSeasonModal.value = true
}

function onFarmChange() {
  const farmParcels = parcels.value.filter((parcel) => parcel.farmId === seasonForm.farmId)
  seasonForm.landParcelId = farmParcels[0]?.id ?? null
  seasonForm.areaHectares = farmParcels[0]?.areaHectares ?? null
}

function onParcelChange() {
  const parcel = parcels.value.find((item) => item.id === seasonForm.landParcelId)
  if (parcel) seasonForm.areaHectares = parcel.areaHectares
}

async function saveSeason() {
  formError.value = ''
  if (!seasonForm.cropId || !seasonForm.farmId || !seasonForm.landParcelId) {
    formError.value = 'Select the crop, farm and land parcel for this season.'
    return
  }
  if (seasonForm.seasonName.trim().length < 2) {
    formError.value = 'Enter a season name of at least two characters.'
    return
  }
  if (!seasonForm.plantingDate) {
    formError.value = 'Enter the planting date.'
    return
  }

  saving.value = true
  const payload: CropSeasonRequest = {
    cropId: seasonForm.cropId,
    farmId: seasonForm.farmId,
    landParcelId: seasonForm.landParcelId,
    seasonName: seasonForm.seasonName.trim(),
    status: seasonForm.status,
    plantingDate: seasonForm.plantingDate,
    expectedHarvestDate: seasonForm.expectedHarvestDate || null,
    areaHectares: seasonForm.areaHectares ?? undefined,
    expectedYieldTons: seasonForm.expectedYieldTons ?? null,
    notes: seasonForm.notes.trim() || undefined
  }
  try {
    if (editingSeason.value) {
      const { data } = await cropsApi.updateSeason(editingSeason.value.id, payload)
      message.value = `Season "${data.seasonName}" updated.`
    } else {
      const { data } = await cropsApi.createSeason(payload, workspace.organizationId)
      message.value = `Season "${data.seasonName}" created.`
    }
    showSeasonModal.value = false
    editingSeason.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function deleteSeason(season: CropSeason) {
  if (!window.confirm(`Delete the season "${season.seasonName}" and its harvest records?`)) return
  try {
    await cropsApi.deleteSeason(season.id)
    message.value = `Season "${season.seasonName}" deleted.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

async function deleteCrop(cropId: number) {
  const crop = crops.value.find((item) => item.id === cropId)
  if (!crop || !window.confirm(`Remove "${crop.name}" from the crop catalogue?`)) return
  try {
    await cropsApi.deleteCrop(crop.id)
    message.value = `Crop "${crop.name}" removed.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

function openHarvestModal(season: CropSeason) {
  formError.value = ''
  harvestSeason.value = season
  Object.assign(harvestForm, {
    harvestDate: todayIso(),
    quantityTons: null,
    grade: '',
    notes: ''
  })
  showHarvestModal.value = true
}

async function saveHarvest() {
  if (!harvestSeason.value) return
  formError.value = ''
  if (!harvestForm.harvestDate) {
    formError.value = 'Enter the harvest date.'
    return
  }
  if (!harvestForm.quantityTons || harvestForm.quantityTons <= 0) {
    formError.value = 'Enter a harvested quantity greater than zero.'
    return
  }
  saving.value = true
  try {
    await cropsApi.createHarvest(harvestSeason.value.id, {
      harvestDate: harvestForm.harvestDate,
      quantityTons: harvestForm.quantityTons,
      grade: harvestForm.grade || undefined,
      notes: harvestForm.notes.trim() || undefined
    })
    message.value = `Harvest recorded for "${harvestSeason.value.seasonName}".`
    showHarvestModal.value = false
    harvestSeason.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

function harvestProgress(season: CropSeason) {
  if (!season.expectedYieldTons || season.expectedYieldTons <= 0) return 0
  return percentage(Number(season.harvestedTons), Number(season.expectedYieldTons))
}

onMounted(load)
</script>

<template>
  <div class="crops-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>
    <div v-if="message" class="notice notice-success">
      <AppIcon name="crop" :size="18" /> {{ message }}
      <button type="button" class="dismiss" @click="message = ''">Dismiss</button>
    </div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Production intelligence</p>
        <h2>Crops &amp; planting</h2>
        <p class="subtitle">Plan crop cycles against land parcels and track yield from planting through harvest.</p>
        <div class="scope-line">
          <span><AppIcon name="crop" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!canManage" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button v-if="canManage" type="button" class="btn btn-secondary" @click="openCropModal">
          <AppIcon name="plus" :size="16" /> Add crop
        </button>
        <button v-if="canManage" type="button" class="btn btn-primary" @click="openSeasonModal">
          <AppIcon name="plus" :size="16" /> New season
        </button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading crop production...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Active seasons</span><strong>{{ formatNumber(overview.metrics.activeSeasons) }}</strong><small>{{ formatNumber(overview.metrics.plannedSeasons) }} planned</small></div>
        <div class="stat"><span>Planted area</span><strong>{{ formatHectares(overview.metrics.plantedHectares) }}</strong><small>{{ formatHectares(filteredHectares) }} in view</small></div>
        <div class="stat"><span>Expected yield</span><strong>{{ formatNumber(overview.metrics.expectedYieldTons) }} t</strong><small>Across live seasons</small></div>
        <div class="stat"><span>Harvested</span><strong>{{ formatNumber(overview.metrics.harvestedTons) }} t</strong><small>{{ overview.metrics.upcomingHarvests }} upcoming dates</small></div>
      </section>

      <div class="crops-layout">
        <section class="panel cycles-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Season register</p>
              <h3>Crop cycles</h3>
              <p class="panel-subtitle">{{ filteredSeasons.length }} cycles · {{ formatHectares(filteredHectares) }}</p>
            </div>
            <div class="panel-tools">
              <label class="search-field">
                <AppIcon name="search" :size="16" />
                <input v-model="search" type="search" placeholder="Search crops or parcels..." />
              </label>
              <select v-model="statusFilter" aria-label="Filter crop season status">
                <option value="">All statuses</option>
                <option v-for="status in statuses" :key="status" :value="status">{{ titleCase(status) }}</option>
              </select>
            </div>
          </header>

          <div v-if="filteredSeasons.length === 0" class="empty-panel">
            <span class="empty-icon"><AppIcon name="crop" :size="26" /></span>
            <h4>{{ seasons.length === 0 ? 'No crop cycles yet' : 'No cycles match this view' }}</h4>
            <p>
              {{ seasons.length === 0
                ? 'Add a crop to the catalogue, then create a season linked to a land parcel.'
                : 'Clear the search and status filter to see the full season register.' }}
            </p>
            <button v-if="canManage && seasons.length === 0" type="button" class="btn btn-primary" @click="openSeasonModal">
              <AppIcon name="plus" :size="16" /> Create first season
            </button>
          </div>

          <div v-else class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Crop cycle</th>
                  <th>Parcel</th>
                  <th>Status</th>
                  <th>Planting</th>
                  <th>Harvest</th>
                  <th class="numeric">Area</th>
                  <th>Yield progress</th>
                  <th class="actions-col"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="season in filteredSeasons" :key="season.id">
                  <td>
                    <div class="crop-cell">
                      <span class="crop-avatar"><AppIcon name="crop" :size="15" /></span>
                      <span>
                        <strong>{{ season.cropName }}<template v-if="season.variety"> · {{ season.variety }}</template></strong>
                        <small>{{ season.seasonName }}</small>
                      </span>
                    </div>
                  </td>
                  <td>
                    <div class="parcel-cell">
                      <strong>{{ season.landParcelName }}</strong>
                      <small>{{ season.farmName }}</small>
                    </div>
                  </td>
                  <td><span class="crop-pill" :class="cropStatusClass(season.status)">{{ titleCase(season.status) }}</span></td>
                  <td class="date-cell">{{ formatDateOnly(season.plantingDate) }}</td>
                  <td class="date-cell">{{ formatDateOnly(season.expectedHarvestDate) }}</td>
                  <td class="numeric strong-cell">{{ formatHectares(season.areaHectares) }}</td>
                  <td>
                    <div class="yield-cell">
                      <div class="yield-top">
                        <span>{{ formatNumber(season.harvestedTons) }} / {{ season.expectedYieldTons == null ? '—' : `${formatNumber(season.expectedYieldTons)} t` }}</span>
                        <strong>{{ harvestProgress(season) }}%</strong>
                      </div>
                      <div class="bar-track"><span :style="{ width: `${harvestProgress(season)}%` }"></span></div>
                    </div>
                  </td>
                  <td class="actions-cell">
                    <template v-if="canManage">
                      <button type="button" class="row-action" @click="editSeason(season)">Edit</button>
                      <button type="button" class="row-action" @click="openHarvestModal(season)">Harvest</button>
                      <button type="button" class="row-action danger" @click="deleteSeason(season)">Delete</button>
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
                <p class="panel-eyebrow">Catalogue</p>
                <h3>Registered crops</h3>
              </div>
              <button v-if="canManage" type="button" class="icon-add" aria-label="Add crop" @click="openCropModal">
                <AppIcon name="plus" :size="15" />
              </button>
            </header>
            <ul v-if="crops.length" class="crop-list">
              <li v-for="crop in crops" :key="crop.id">
                <span class="crop-list-icon"><AppIcon name="leaf" :size="15" /></span>
                <span class="crop-list-copy">
                  <strong>{{ crop.name }}</strong>
                  <small>{{ titleCase(crop.category) }}<template v-if="crop.variety"> · {{ crop.variety }}</template></small>
                </span>
                <span class="season-count">{{ crop.seasonCount }} {{ crop.seasonCount === 1 ? 'cycle' : 'cycles' }}</span>
                <button v-if="canManage" type="button" class="remove-action" title="Remove crop" @click="deleteCrop(crop.id)">
                  <AppIcon name="close" :size="13" />
                </button>
              </li>
            </ul>
            <div v-else class="compact-empty">
              <p>No crops in the catalogue. Add a crop to begin planning seasons.</p>
              <button v-if="canManage" type="button" class="text-action" @click="openCropModal">Add a crop <AppIcon name="arrow" :size="12" /></button>
            </div>
          </section>

          <section class="panel">
            <header class="panel-head">
              <div>
                <p class="panel-eyebrow">Schedule</p>
                <h3>Upcoming harvests</h3>
              </div>
              <span class="count-badge">{{ upcomingHarvests.length }}</span>
            </header>
            <ul v-if="upcomingHarvests.length" class="upcoming-list">
              <li v-for="season in upcomingHarvests" :key="season.id">
                <span class="date-tile"><strong>{{ formatDateOnly(season.expectedHarvestDate).split(' ')[0] }}</strong><small>{{ formatDateOnly(season.expectedHarvestDate).split(' ')[1] }}</small></span>
                <span class="upcoming-copy">
                  <strong>{{ season.cropName }}</strong>
                  <small>{{ season.landParcelName }} · {{ season.seasonName }}</small>
                </span>
              </li>
            </ul>
            <div v-else class="compact-empty">
              <p>No future harvest dates are scheduled.</p>
            </div>
          </section>

          <section class="panel">
            <header class="panel-head">
              <div>
                <p class="panel-eyebrow">Recorded output</p>
                <h3>Recent harvests</h3>
              </div>
            </header>
            <ul v-if="overview.recentHarvests.length" class="harvest-list">
              <li v-for="harvest in overview.recentHarvests.slice(0, 4)" :key="harvest.id">
                <span class="harvest-icon"><AppIcon name="inventory" :size="15" /></span>
                <span class="harvest-copy">
                  <strong>{{ harvest.cropName }} · {{ harvest.seasonName }}</strong>
                  <small>{{ harvest.farmName }} · {{ formatDateOnly(harvest.harvestDate) }}</small>
                </span>
                <span class="harvest-qty">{{ formatNumber(harvest.quantityTons) }} t</span>
              </li>
            </ul>
            <div v-else class="compact-empty">
              <p>No harvest records have been captured.</p>
            </div>
          </section>
        </aside>
      </div>
    </template>

    <AppModal v-if="showCropModal" title="Add a crop" subtitle="Build the crop catalogue used by production cycles." @close="showCropModal = false">
      <form id="crop-form" class="form-grid" @submit.prevent="saveCrop">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2">
          <span>Crop name</span>
          <input v-model="cropForm.name" maxlength="120" placeholder="e.g. Maize" autofocus />
        </label>
        <label class="field">
          <span>Category</span>
          <select v-model="cropForm.category">
            <option v-for="category in categoryOptions" :key="category" :value="category">{{ category }}</option>
          </select>
        </label>
        <label class="field">
          <span>Variety</span>
          <input v-model="cropForm.variety" maxlength="120" placeholder="e.g. SC Duma 43" />
        </label>
        <label class="field span-2">
          <span>Description</span>
          <textarea v-model="cropForm.description" maxlength="1000" rows="3" placeholder="Optional agronomic notes"></textarea>
        </label>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showCropModal = false">Cancel</button>
        <button type="submit" form="crop-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : 'Add crop' }}</button>
      </template>
    </AppModal>

    <AppModal
      v-if="showSeasonModal"
      :title="editingSeason ? 'Edit crop season' : 'Create a crop season'"
      subtitle="Link a crop cycle to a land parcel and plan its production window."
      size="wide"
      @close="showSeasonModal = false"
    >
      <form id="season-form" class="form-grid three-col" @submit.prevent="saveSeason">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field">
          <span>Crop</span>
          <select v-model.number="seasonForm.cropId">
            <option :value="null" disabled>Select a crop</option>
            <option v-for="crop in crops" :key="crop.id" :value="crop.id">{{ crop.name }}<template v-if="crop.variety"> · {{ crop.variety }}</template></option>
          </select>
        </label>
        <label class="field">
          <span>Farm</span>
          <select v-model.number="seasonForm.farmId" @change="onFarmChange">
            <option :value="null" disabled>Select a farm</option>
            <option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option>
          </select>
        </label>
        <label class="field">
          <span>Land parcel</span>
          <select v-model.number="seasonForm.landParcelId" @change="onParcelChange">
            <option :value="null" disabled>Select a parcel</option>
            <option v-for="parcel in parcelsForFarm" :key="parcel.id" :value="parcel.id">{{ parcel.name }}</option>
          </select>
        </label>
        <label class="field span-2">
          <span>Season name</span>
          <input v-model="seasonForm.seasonName" maxlength="80" placeholder="e.g. 2026/27 Summer" />
        </label>
        <label class="field">
          <span>Status</span>
          <select v-model="seasonForm.status">
            <option v-for="status in seasonStatusOptions" :key="status" :value="status">{{ titleCase(status) }}</option>
          </select>
        </label>
        <label class="field">
          <span>Planting date</span>
          <input v-model="seasonForm.plantingDate" type="date" />
        </label>
        <label class="field">
          <span>Expected harvest</span>
          <input v-model="seasonForm.expectedHarvestDate" type="date" :min="seasonForm.plantingDate" />
        </label>
        <label class="field">
          <span>Planted area (ha)</span>
          <input v-model.number="seasonForm.areaHectares" type="number" min="0.01" step="0.01" placeholder="Parcel area" />
        </label>
        <label class="field">
          <span>Expected yield (t)</span>
          <input v-model.number="seasonForm.expectedYieldTons" type="number" min="0" step="0.01" />
        </label>
        <label class="field span-3">
          <span>Notes</span>
          <textarea v-model="seasonForm.notes" maxlength="1000" rows="3" placeholder="Optional planting or agronomy notes"></textarea>
        </label>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showSeasonModal = false">Cancel</button>
        <button type="submit" form="season-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editingSeason ? 'Save season' : 'Create season' }}</button>
      </template>
    </AppModal>

    <AppModal
      v-if="showHarvestModal && harvestSeason"
      title="Record a harvest"
      :subtitle="`${harvestSeason.cropName} · ${harvestSeason.seasonName}`"
      @close="showHarvestModal = false"
    >
      <form id="harvest-form" class="form-grid" @submit.prevent="saveHarvest">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field">
          <span>Harvest date</span>
          <input v-model="harvestForm.harvestDate" type="date" :min="harvestSeason.plantingDate" :max="todayIso()" />
        </label>
        <label class="field">
          <span>Quantity (t)</span>
          <input v-model.number="harvestForm.quantityTons" type="number" min="0.001" step="0.001" placeholder="e.g. 3.25" />
        </label>
        <label class="field">
          <span>Grade</span>
          <select v-model="harvestForm.grade">
            <option value="">Not graded</option>
            <option v-for="grade in gradeOptions" :key="grade" :value="grade">Grade {{ grade }}</option>
          </select>
        </label>
        <label class="field">
          <span>Notes</span>
          <input v-model="harvestForm.notes" maxlength="1000" placeholder="Optional harvest note" />
        </label>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showHarvestModal = false">Cancel</button>
        <button type="submit" form="harvest-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : 'Record harvest' }}</button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.crops-page {
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

.btn:disabled {
  opacity: 0.62;
  cursor: not-allowed;
}

.loading-state {
  padding: 2rem;
  color: #7b8c7f;
  font-size: 0.8rem;
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

.crops-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.7fr) minmax(300px, 0.8fr);
  gap: 0.8rem;
  align-items: start;
}

.side-stack {
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
}

.panel {
  border: 1px solid #e1e9e1;
  border-radius: 13px;
  background: #fff;
  box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04);
  overflow: hidden;
}

.panel-head {
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

.panel-head h3 {
  margin: 0;
  color: #1c3a26;
  font-size: 0.95rem;
}

.panel-subtitle {
  margin: 0.18rem 0 0;
  color: #8a978c;
  font-size: 0.64rem;
}

.panel-tools {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.search-field {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  width: 245px;
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

.panel-tools select {
  max-width: 126px;
  border: 1px solid #dfe7df;
  border-radius: 8px;
  background: #f7faf7;
  padding: 0.42rem 0.35rem;
  color: #3f5b49;
  font-size: 0.68rem;
  font-weight: 600;
}

.icon-add {
  display: grid;
  place-items: center;
  width: 27px;
  height: 27px;
  border: 1px solid #dce8de;
  border-radius: 7px;
  background: #f4faf5;
  color: #2f754d;
  cursor: pointer;
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
  padding: 0.55rem 0.7rem;
  color: #8b978d;
  background: #fbfcfb;
  font-size: 0.54rem;
  font-weight: 800;
  letter-spacing: 0.07em;
  text-align: left;
  text-transform: uppercase;
  white-space: nowrap;
}

td {
  padding: 0.58rem 0.7rem;
  border-top: 1px solid #eef2ee;
  color: #4c6353;
  white-space: nowrap;
}

.crop-cell {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.crop-avatar {
  display: grid;
  place-items: center;
  width: 27px;
  height: 27px;
  flex: 0 0 auto;
  border-radius: 7px;
  background: #eff7ef;
  color: #3c8054;
}

.crop-cell > span:last-child,
.parcel-cell {
  display: flex;
  flex-direction: column;
}

.crop-cell strong,
.parcel-cell strong {
  color: #294931;
  font-size: 0.7rem;
}

.crop-cell small,
.parcel-cell small {
  color: #939f94;
  font-size: 0.58rem;
}

.crop-pill {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.16rem 0.42rem;
  font-size: 0.56rem;
  font-weight: 800;
  white-space: nowrap;
}

.crop-planned { background: #f1f4f1; color: #67766a; }
.crop-planted { background: #eaf3fa; color: #346b93; }
.crop-growing { background: #e8f5ec; color: #28734a; }
.crop-harvested { background: #fff6df; color: #956315; }
.crop-failed { background: #fdecec; color: #9b1c1c; }
.crop-neutral { background: #f1f4f1; color: #6d7a70; }

.date-cell {
  color: #607765;
  font-size: 0.64rem;
}

.numeric {
  text-align: right;
}

.strong-cell {
  color: #24432e;
  font-weight: 700;
}

.yield-cell {
  min-width: 105px;
}

.yield-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.35rem;
  color: #8a978c;
  font-size: 0.58rem;
}

.yield-top strong {
  color: #47634f;
  font-size: 0.58rem;
}

.bar-track {
  height: 4px;
  margin-top: 0.3rem;
  border-radius: 999px;
  background: #edf2ed;
  overflow: hidden;
}

.bar-track span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: #4a9d70;
}

.actions-col {
  width: 128px;
}

.actions-cell {
  text-align: right;
}

.row-action {
  border: 0;
  background: transparent;
  color: #2f704a;
  font-size: 0.6rem;
  font-weight: 700;
  cursor: pointer;
}

.row-action + .row-action {
  margin-left: 0.4rem;
}

.row-action.danger {
  color: #9b4141;
}

.empty-panel {
  display: grid;
  place-items: center;
  gap: 0.45rem;
  padding: 3rem 1.5rem;
  text-align: center;
}

.empty-panel h4 {
  margin: 0;
  color: #2b4a34;
  font-size: 0.9rem;
}

.empty-panel p {
  max-width: 420px;
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

.crop-list,
.upcoming-list,
.harvest-list {
  list-style: none;
  margin: 0;
  padding: 0 0.7rem 0.7rem;
}

.crop-list li,
.upcoming-list li,
.harvest-list li {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.55rem 0.2rem;
  border-top: 1px solid #eef2ee;
}

.crop-list li:first-child,
.upcoming-list li:first-child,
.harvest-list li:first-child {
  border-top: 0;
}

.crop-list-icon,
.harvest-icon {
  display: grid;
  place-items: center;
  width: 27px;
  height: 27px;
  flex: 0 0 auto;
  border-radius: 7px;
  background: #f0f7f1;
  color: #4a7c5b;
}

.crop-list-copy,
.upcoming-copy,
.harvest-copy {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.crop-list-copy strong,
.upcoming-copy strong,
.harvest-copy strong {
  overflow: hidden;
  color: #294931;
  font-size: 0.69rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.crop-list-copy small,
.upcoming-copy small,
.harvest-copy small {
  overflow: hidden;
  color: #929e94;
  font-size: 0.58rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.season-count {
  flex: 0 0 auto;
  color: #7e8f83;
  font-size: 0.57rem;
}

.remove-action {
  display: grid;
  place-items: center;
  width: 20px;
  height: 20px;
  border: 0;
  border-radius: 5px;
  background: transparent;
  color: #a35353;
  cursor: pointer;
}

.remove-action:hover {
  background: #fdf2f2;
}

.count-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 22px;
  border-radius: 999px;
  background: #edf6ef;
  color: #2f704a;
  font-size: 0.6rem;
  font-weight: 800;
}

.date-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  flex: 0 0 auto;
  border-radius: 8px;
  background: #f2f7f2;
  color: #3d6049;
}

.date-tile strong {
  font-size: 0.66rem;
  text-transform: uppercase;
}

.date-tile small {
  color: #8b998f;
  font-size: 0.52rem;
}

.harvest-qty {
  flex: 0 0 auto;
  color: #3e6049;
  font-size: 0.65rem;
  font-weight: 800;
}

.compact-empty {
  padding: 1rem;
  color: #879589;
  font-size: 0.68rem;
  line-height: 1.5;
}

.compact-empty p {
  margin: 0;
}

.text-action {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  margin-top: 0.4rem;
  border: 0;
  background: transparent;
  color: #2f704a;
  font-size: 0.65rem;
  font-weight: 700;
  cursor: pointer;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.form-grid.three-col {
  grid-template-columns: repeat(3, minmax(0, 1fr));
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
  color: #48604f;
  font-size: 0.7rem;
  font-weight: 700;
}

.field.span-2 {
  grid-column: span 2;
}

.field.span-3 {
  grid-column: span 3;
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

@media (max-width: 1150px) {
  .crops-layout {
    grid-template-columns: 1fr;
  }

  .side-stack {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 900px) {
  .page-intro {
    flex-direction: column;
    align-items: flex-start;
  }

  .stat-row {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .side-stack {
    grid-template-columns: 1fr;
  }

  .panel-head {
    flex-direction: column;
  }

  .panel-tools {
    width: 100%;
  }

  .search-field {
    flex: 1;
    width: auto;
  }

  .form-grid.three-col {
    grid-template-columns: 1fr 1fr;
  }

  .field.span-3 {
    grid-column: span 2;
  }
}

@media (max-width: 620px) {
  .stat-row {
    grid-template-columns: 1fr;
  }

  .form-grid,
  .form-grid.three-col {
    grid-template-columns: 1fr;
  }

  .field.span-2,
  .field.span-3 {
    grid-column: auto;
  }
}
</style>
