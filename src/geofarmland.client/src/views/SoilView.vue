<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  soilApi,
  type IrrigationEvent,
  type IrrigationEventRequest,
  type SoilOverview,
  type SoilSample,
  type SoilSampleRequest
} from '@/api/soil'
import { enterpriseApi, type Farm, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatDateOnly, formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()

const overview = ref<SoilOverview | null>(null)
const farms = ref<Farm[]>([])
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const saving = ref(false)
const formError = ref('')

const showSampleModal = ref(false)
const showIrrigationModal = ref(false)
const editingSample = ref<SoilSample | null>(null)
const editingIrrigation = ref<IrrigationEvent | null>(null)

const sampleForm = reactive({
  farmId: null as number | null,
  landParcelId: null as number | null,
  sampleDate: '',
  ph: 6.5,
  organicMatterPct: 2.5,
  nitrogenPpm: null as number | null,
  phosphorusPpm: null as number | null,
  potassiumPpm: null as number | null,
  laboratory: '',
  notes: ''
})

const irrigationForm = reactive({
  farmId: null as number | null,
  landParcelId: null as number | null,
  eventDate: '',
  volumeM3: null as number | null,
  durationMinutes: null as number | null,
  source: 'Borehole',
  notes: ''
})

const sourceOptions = ['Borehole', 'Canal', 'River', 'Dam', 'Municipal', 'Rainwater', 'Other']

const todayIso = () => {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const samples = computed(() => overview.value?.samples ?? [])
const irrigationEvents = computed(() => overview.value?.irrigationEvents ?? [])
const sampleParcels = computed(() => parcels.value.filter((parcel) => !sampleForm.farmId || parcel.farmId === sampleForm.farmId))
const irrigationParcels = computed(() => parcels.value.filter((parcel) => !irrigationForm.farmId || parcel.farmId === irrigationForm.farmId))

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.message
    || 'The request could not be completed.'
}

function phTone(ph: number) {
  if (ph < 5.5) return 'ph-acid'
  if (ph > 7.5) return 'ph-alkaline'
  return 'ph-optimal'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const organizationId = workspace.organizationId
    const [soilResponse, farmResponse, parcelResponse] = await Promise.all([
      soilApi.overview(organizationId),
      enterpriseApi.farms(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = soilResponse.data
    farms.value = farmResponse.data
    parcels.value = parcelResponse.data.items
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function openSampleModal() {
  formError.value = ''
  editingSample.value = null
  const firstFarm = farms.value[0]?.id ?? null
  const firstParcel = parcels.value.find((parcel) => parcel.farmId === firstFarm)
  Object.assign(sampleForm, {
    farmId: firstFarm,
    landParcelId: firstParcel?.id ?? null,
    sampleDate: todayIso(),
    ph: 6.5,
    organicMatterPct: 2.5,
    nitrogenPpm: null,
    phosphorusPpm: null,
    potassiumPpm: null,
    laboratory: '',
    notes: ''
  })
  showSampleModal.value = true
}

function editSample(sample: SoilSample) {
  formError.value = ''
  editingSample.value = sample
  Object.assign(sampleForm, {
    farmId: sample.farmId,
    landParcelId: sample.landParcelId,
    sampleDate: sample.sampleDate,
    ph: Number(sample.ph),
    organicMatterPct: Number(sample.organicMatterPct),
    nitrogenPpm: sample.nitrogenPpm == null ? null : Number(sample.nitrogenPpm),
    phosphorusPpm: sample.phosphorusPpm == null ? null : Number(sample.phosphorusPpm),
    potassiumPpm: sample.potassiumPpm == null ? null : Number(sample.potassiumPpm),
    laboratory: sample.laboratory ?? '',
    notes: sample.notes ?? ''
  })
  showSampleModal.value = true
}

function onSampleFarmChange() {
  sampleForm.landParcelId = sampleParcels.value[0]?.id ?? null
}

async function saveSample() {
  formError.value = ''
  if (!sampleForm.farmId || !sampleForm.landParcelId) {
    formError.value = 'Select a farm and land parcel.'
    return
  }
  if (!sampleForm.sampleDate) {
    formError.value = 'Enter the sample date.'
    return
  }
  saving.value = true
  const payload: SoilSampleRequest = {
    farmId: sampleForm.farmId,
    landParcelId: sampleForm.landParcelId,
    sampleDate: sampleForm.sampleDate,
    ph: sampleForm.ph,
    organicMatterPct: sampleForm.organicMatterPct,
    nitrogenPpm: sampleForm.nitrogenPpm,
    phosphorusPpm: sampleForm.phosphorusPpm,
    potassiumPpm: sampleForm.potassiumPpm,
    laboratory: sampleForm.laboratory.trim() || undefined,
    notes: sampleForm.notes.trim() || undefined
  }
  try {
    if (editingSample.value) {
      await soilApi.updateSample(editingSample.value.id, payload)
      message.value = 'Soil sample updated.'
    } else {
      await soilApi.createSample(payload, workspace.organizationId)
      message.value = 'Soil sample recorded.'
    }
    showSampleModal.value = false
    editingSample.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function deleteSample(sample: SoilSample) {
  if (!window.confirm(`Delete the soil sample from ${formatDateOnly(sample.sampleDate)}?`)) return
  try {
    await soilApi.deleteSample(sample.id)
    message.value = 'Soil sample deleted.'
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

function openIrrigationModal() {
  formError.value = ''
  editingIrrigation.value = null
  const firstFarm = farms.value[0]?.id ?? null
  const firstParcel = parcels.value.find((parcel) => parcel.farmId === firstFarm)
  Object.assign(irrigationForm, {
    farmId: firstFarm,
    landParcelId: firstParcel?.id ?? null,
    eventDate: todayIso(),
    volumeM3: null,
    durationMinutes: null,
    source: 'Borehole',
    notes: ''
  })
  showIrrigationModal.value = true
}

function editIrrigation(event: IrrigationEvent) {
  formError.value = ''
  editingIrrigation.value = event
  Object.assign(irrigationForm, {
    farmId: event.farmId,
    landParcelId: event.landParcelId,
    eventDate: event.eventDate,
    volumeM3: Number(event.volumeM3),
    durationMinutes: event.durationMinutes ?? null,
    source: event.source,
    notes: event.notes ?? ''
  })
  showIrrigationModal.value = true
}

function onIrrigationFarmChange() {
  irrigationForm.landParcelId = irrigationParcels.value[0]?.id ?? null
}

async function saveIrrigation() {
  formError.value = ''
  if (!irrigationForm.farmId || !irrigationForm.landParcelId) {
    formError.value = 'Select a farm and land parcel.'
    return
  }
  if (!irrigationForm.eventDate) {
    formError.value = 'Enter the irrigation date.'
    return
  }
  if (!irrigationForm.volumeM3 || irrigationForm.volumeM3 <= 0) {
    formError.value = 'Enter a water volume greater than zero.'
    return
  }
  saving.value = true
  const payload: IrrigationEventRequest = {
    farmId: irrigationForm.farmId,
    landParcelId: irrigationForm.landParcelId,
    eventDate: irrigationForm.eventDate,
    volumeM3: irrigationForm.volumeM3,
    durationMinutes: irrigationForm.durationMinutes,
    source: irrigationForm.source,
    notes: irrigationForm.notes.trim() || undefined
  }
  try {
    if (editingIrrigation.value) {
      await soilApi.updateIrrigation(editingIrrigation.value.id, payload)
      message.value = 'Irrigation event updated.'
    } else {
      await soilApi.createIrrigation(payload, workspace.organizationId)
      message.value = 'Irrigation event recorded.'
    }
    showIrrigationModal.value = false
    editingIrrigation.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function deleteIrrigation(event: IrrigationEvent) {
  if (!window.confirm(`Delete the irrigation event from ${formatDateOnly(event.eventDate)}?`)) return
  try {
    await soilApi.deleteIrrigation(event.id)
    message.value = 'Irrigation event deleted.'
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

onMounted(load)
</script>

<template>
  <div class="soil-page">
    <div v-if="error" class="notice notice-error"><AppIcon name="warning" :size="18" /> {{ error }}<button type="button" @click="load">Retry</button></div>
    <div v-if="message" class="notice notice-success"><AppIcon name="soil" :size="18" /> {{ message }}<button type="button" class="dismiss" @click="message = ''">Dismiss</button></div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Soil and water</p>
        <h2>Soil &amp; irrigation</h2>
        <p class="subtitle">Track soil chemistry, laboratory results and irrigation water applications against land parcels.</p>
        <div class="scope-line">
          <span><AppIcon name="soil" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!canManage" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button v-if="canManage" type="button" class="btn btn-secondary" @click="openSampleModal"><AppIcon name="plus" :size="16" /> Soil sample</button>
        <button v-if="canManage" type="button" class="btn btn-primary" @click="openIrrigationModal"><AppIcon name="plus" :size="16" /> Irrigation event</button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading soil and irrigation records...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Soil samples</span><strong>{{ formatNumber(overview.metrics.samples) }}</strong><small>Laboratory and field records</small></div>
        <div class="stat"><span>Average pH</span><strong>{{ overview.metrics.averagePH == null ? '—' : formatNumber(overview.metrics.averagePH) }}</strong><small>Across recorded samples</small></div>
        <div class="stat"><span>Organic matter</span><strong>{{ overview.metrics.averageOrganicMatterPct == null ? '—' : `${formatNumber(overview.metrics.averageOrganicMatterPct)}%` }}</strong><small>Average across samples</small></div>
        <div class="stat"><span>Water · 30 days</span><strong>{{ formatNumber(overview.metrics.waterVolumeLast30DaysM3) }} m³</strong><small>{{ formatNumber(overview.metrics.irrigationEventsLast30Days) }} irrigation events</small></div>
      </section>

      <section class="panel">
        <header class="panel-head">
          <div><p class="panel-eyebrow">Soil chemistry</p><h3>Soil sample register</h3></div>
          <button v-if="canManage" type="button" class="btn btn-secondary small" @click="openSampleModal"><AppIcon name="plus" :size="14" /> Add sample</button>
        </header>
        <div v-if="samples.length === 0" class="empty-panel"><span class="empty-icon"><AppIcon name="soil" :size="26" /></span><p>No soil samples yet. Record pH, organic matter and nutrient results to build a parcel history.</p></div>
        <div v-else class="table-wrap">
          <table>
            <thead><tr><th>Sample date</th><th>Parcel</th><th class="numeric">pH</th><th class="numeric">Organic matter</th><th class="numeric">N</th><th class="numeric">P</th><th class="numeric">K</th><th>Laboratory</th><th class="actions-col"></th></tr></thead>
            <tbody>
              <tr v-for="sample in samples" :key="sample.id">
                <td>{{ formatDateOnly(sample.sampleDate) }}</td>
                <td><div class="parcel-cell"><strong>{{ sample.landParcelName }}</strong><small>{{ sample.farmName }}</small></div></td>
                <td class="numeric"><span class="ph-pill" :class="phTone(sample.ph)">{{ sample.ph.toFixed(2) }}</span></td>
                <td class="numeric">{{ sample.organicMatterPct.toFixed(2) }}%</td>
                <td class="numeric">{{ sample.nitrogenPpm == null ? '—' : formatNumber(sample.nitrogenPpm) }}</td>
                <td class="numeric">{{ sample.phosphorusPpm == null ? '—' : formatNumber(sample.phosphorusPpm) }}</td>
                <td class="numeric">{{ sample.potassiumPpm == null ? '—' : formatNumber(sample.potassiumPpm) }}</td>
                <td class="muted-cell">{{ sample.laboratory || '—' }}</td>
                <td class="actions-cell"><template v-if="canManage"><button type="button" class="row-action" @click="editSample(sample)">Edit</button><button type="button" class="row-action danger" @click="deleteSample(sample)">Delete</button></template></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="panel events-panel">
        <header class="panel-head">
          <div><p class="panel-eyebrow">Water applications</p><h3>Irrigation events</h3></div>
          <button v-if="canManage" type="button" class="btn btn-secondary small" @click="openIrrigationModal"><AppIcon name="plus" :size="14" /> Log event</button>
        </header>
        <div v-if="irrigationEvents.length === 0" class="empty-panel"><span class="empty-icon"><AppIcon name="soil" :size="26" /></span><p>No irrigation events yet. Log water volume and duration to track application history.</p></div>
        <div v-else class="table-wrap">
          <table>
            <thead><tr><th>Event date</th><th>Parcel</th><th class="numeric">Volume</th><th class="numeric">Duration</th><th>Source</th><th>Notes</th><th class="actions-col"></th></tr></thead>
            <tbody>
              <tr v-for="event in irrigationEvents" :key="event.id">
                <td>{{ formatDateOnly(event.eventDate) }}</td>
                <td><div class="parcel-cell"><strong>{{ event.landParcelName }}</strong><small>{{ event.farmName }}</small></div></td>
                <td class="numeric strong-cell">{{ formatNumber(event.volumeM3) }} m³</td>
                <td class="numeric">{{ event.durationMinutes == null ? '—' : `${event.durationMinutes} min` }}</td>
                <td>{{ titleCase(event.source) }}</td>
                <td class="muted-cell notes-cell">{{ event.notes || '—' }}</td>
                <td class="actions-cell"><template v-if="canManage"><button type="button" class="row-action" @click="editIrrigation(event)">Edit</button><button type="button" class="row-action danger" @click="deleteIrrigation(event)">Delete</button></template></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>

    <AppModal v-if="showSampleModal" :title="editingSample ? 'Edit soil sample' : 'Record a soil sample'" subtitle="Capture laboratory or field soil chemistry results." @close="showSampleModal = false">
      <form id="sample-form" class="form-grid" @submit.prevent="saveSample">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field"><span>Farm</span><select v-model.number="sampleForm.farmId" @change="onSampleFarmChange"><option :value="null" disabled>Select a farm</option><option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option></select></label>
        <label class="field"><span>Land parcel</span><select v-model.number="sampleForm.landParcelId"><option :value="null" disabled>Select a parcel</option><option v-for="parcel in sampleParcels" :key="parcel.id" :value="parcel.id">{{ parcel.name }}</option></select></label>
        <label class="field"><span>Sample date</span><input v-model="sampleForm.sampleDate" type="date" :max="todayIso()" /></label>
        <label class="field"><span>pH</span><input v-model.number="sampleForm.ph" type="number" min="0" max="14" step="0.01" /></label>
        <label class="field"><span>Organic matter (%)</span><input v-model.number="sampleForm.organicMatterPct" type="number" min="0" max="100" step="0.01" /></label>
        <label class="field"><span>Laboratory</span><input v-model="sampleForm.laboratory" maxlength="160" placeholder="Optional laboratory name" /></label>
        <label class="field"><span>Nitrogen (ppm)</span><input v-model.number="sampleForm.nitrogenPpm" type="number" min="0" step="0.01" /></label>
        <label class="field"><span>Phosphorus (ppm)</span><input v-model.number="sampleForm.phosphorusPpm" type="number" min="0" step="0.01" /></label>
        <label class="field"><span>Potassium (ppm)</span><input v-model.number="sampleForm.potassiumPpm" type="number" min="0" step="0.01" /></label>
        <label class="field span-2"><span>Notes</span><input v-model="sampleForm.notes" maxlength="1000" placeholder="Optional sample notes" /></label>
      </form>
      <template #footer><button type="button" class="btn btn-secondary" @click="showSampleModal = false">Cancel</button><button type="submit" form="sample-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editingSample ? 'Save sample' : 'Record sample' }}</button></template>
    </AppModal>

    <AppModal v-if="showIrrigationModal" :title="editingIrrigation ? 'Edit irrigation event' : 'Log an irrigation event'" subtitle="Record water applied to a land parcel." @close="showIrrigationModal = false">
      <form id="irrigation-form" class="form-grid" @submit.prevent="saveIrrigation">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field"><span>Farm</span><select v-model.number="irrigationForm.farmId" @change="onIrrigationFarmChange"><option :value="null" disabled>Select a farm</option><option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option></select></label>
        <label class="field"><span>Land parcel</span><select v-model.number="irrigationForm.landParcelId"><option :value="null" disabled>Select a parcel</option><option v-for="parcel in irrigationParcels" :key="parcel.id" :value="parcel.id">{{ parcel.name }}</option></select></label>
        <label class="field"><span>Event date</span><input v-model="irrigationForm.eventDate" type="date" :max="todayIso()" /></label>
        <label class="field"><span>Source</span><select v-model="irrigationForm.source"><option v-for="source in sourceOptions" :key="source" :value="source">{{ source }}</option></select></label>
        <label class="field"><span>Volume (m³)</span><input v-model.number="irrigationForm.volumeM3" type="number" min="0.01" step="0.01" /></label>
        <label class="field"><span>Duration (minutes)</span><input v-model.number="irrigationForm.durationMinutes" type="number" min="1" step="1" /></label>
        <label class="field span-2"><span>Notes</span><input v-model="irrigationForm.notes" maxlength="1000" placeholder="Optional irrigation note" /></label>
      </form>
      <template #footer><button type="button" class="btn btn-secondary" @click="showIrrigationModal = false">Cancel</button><button type="submit" form="irrigation-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editingIrrigation ? 'Save event' : 'Log event' }}</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.soil-page { color: #1d3525; }
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
.head-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; min-height: 36px; border: 1px solid transparent; border-radius: 9px; padding: 0.48rem 0.75rem; font-size: 0.75rem; font-weight: 700; cursor: pointer; }
.btn.small { min-height: 30px; padding: 0.33rem 0.6rem; font-size: 0.68rem; }
.btn-primary { background: #1d5a39; border-color: #1d5a39; color: #fff; }
.btn-primary:hover { background: #16482d; }
.btn-secondary { background: #fff; border-color: #d9e4da; color: #2f513c; }
.btn-secondary:hover { background: #f7faf7; }
.btn:disabled { opacity: 0.62; cursor: not-allowed; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }
.stat-row { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.65rem; margin-bottom: 0.75rem; }
.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }
.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.events-panel { margin-top: 0.8rem; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.8rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.8rem; border-top: 1px solid #eef2ee; color: #4c6353; white-space: nowrap; }
.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.muted-cell { color: #89968c; }
.notes-cell { max-width: 220px; overflow: hidden; text-overflow: ellipsis; }
.parcel-cell { display: flex; flex-direction: column; }
.parcel-cell strong { color: #294931; font-size: 0.7rem; }
.parcel-cell small { color: #939f94; font-size: 0.58rem; }
.ph-pill { display: inline-flex; border-radius: 999px; padding: 0.14rem 0.4rem; font-size: 0.58rem; font-weight: 800; }
.ph-optimal { background: #e8f5ec; color: #28734a; }
.ph-acid { background: #fff6df; color: #956315; }
.ph-alkaline { background: #eaf3fa; color: #346b93; }
.actions-col { width: 100px; }
.actions-cell { text-align: right; }
.row-action { border: 0; background: transparent; color: #2f704a; font-size: 0.6rem; font-weight: 700; cursor: pointer; }
.row-action + .row-action { margin-left: 0.4rem; }
.row-action.danger { color: #9b4141; }
.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 2.5rem 1.5rem; text-align: center; }
.empty-panel p { max-width: 420px; margin: 0; color: #849287; font-size: 0.7rem; line-height: 1.5; }
.empty-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; background: #f2f6ef; color: #6f8448; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
@media (max-width: 900px) { .page-intro { flex-direction: column; align-items: flex-start; } .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } .panel-head { flex-direction: column; } }
@media (max-width: 620px) { .stat-row { grid-template-columns: 1fr; } .form-grid { grid-template-columns: 1fr; } .field.span-2 { grid-column: auto; } }
</style>
