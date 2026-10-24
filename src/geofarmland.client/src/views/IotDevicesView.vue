<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { enterpriseApi, type LandParcel } from '@/api/enterprise'
import { devicesApi } from '@/api/devices'
import { monitoringApi } from '@/api/plots'
import { iotApi, type IotDevice, type IotOverview } from '@/api/iot'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatDateTime, formatNumber, percentage } from '@/utils/enterprise'
import { plotCenterFromCoordinates } from '@/utils/plots'

const workspace = useWorkspaceStore()

const overview = ref<IotOverview | null>(null)
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')

const showDeviceModal = ref(false)
const showReadingModal = ref(false)
const readingDevice = ref<IotDevice | null>(null)
const saving = ref(false)
const formError = ref('')

const deviceForm = reactive({
  name: '',
  deviceId: '',
  landParcelId: null as number | null
})

const readingForm = reactive({
  soilMoisture: 24,
  rainfall: 0
})

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const devices = computed(() => overview.value?.devices ?? [])
const filteredDevices = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return devices.value
  return devices.value.filter((device) =>
    device.name.toLowerCase().includes(term)
    || device.deviceId.toLowerCase().includes(term)
    || (device.landParcelName ?? '').toLowerCase().includes(term)
    || (device.farmName ?? '').toLowerCase().includes(term)
  )
})
const reportingShare = computed(() =>
  percentage(overview.value?.metrics.reportingLast24Hours ?? 0, overview.value?.metrics.devices ?? 0)
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
    const [iotResponse, parcelResponse] = await Promise.all([
      iotApi.overview(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = iotResponse.data
    parcels.value = parcelResponse.data.items
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function openDeviceModal() {
  formError.value = ''
  Object.assign(deviceForm, {
    name: '',
    deviceId: '',
    landParcelId: parcels.value[0]?.id ?? null
  })
  showDeviceModal.value = true
}

async function saveDevice() {
  formError.value = ''
  if (deviceForm.name.trim().length < 2 || deviceForm.deviceId.trim().length < 1) {
    formError.value = 'Enter a device name and a device ID.'
    return
  }
  saving.value = true
  try {
    await devicesApi.create({
      name: deviceForm.name.trim(),
      deviceId: deviceForm.deviceId.trim(),
      landParcelId: deviceForm.landParcelId
    })
    showDeviceModal.value = false
    message.value = `Device "${deviceForm.name.trim()}" registered.`
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

function openReadingModal(device: IotDevice) {
  formError.value = ''
  readingDevice.value = device
  Object.assign(readingForm, { soilMoisture: 24, rainfall: 0 })
  showReadingModal.value = true
}

async function saveReading() {
  if (!readingDevice.value) return
  formError.value = ''
  if (readingForm.soilMoisture < 0 || readingForm.soilMoisture > 100) {
    formError.value = 'Soil moisture must be between 0 and 100 percent.'
    return
  }
  if (readingForm.rainfall < 0 || readingForm.rainfall > 10_000) {
    formError.value = 'Rainfall must be between 0 and 10,000 mm.'
    return
  }
  const parcel = parcels.value.find((item) => item.id === readingDevice.value?.landParcelId)
  const center = parcel ? plotCenterFromCoordinates(parcel.coordinates) : [-33.9249, 18.4241]
  saving.value = true
  try {
    await monitoringApi.ingest({
      deviceId: readingDevice.value.deviceId,
      latitude: center[0],
      longitude: center[1],
      rainfall: readingForm.rainfall,
      soilMoisture: readingForm.soilMoisture
    })
    message.value = `Reading submitted for "${readingDevice.value.name}".`
    showReadingModal.value = false
    readingDevice.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function deleteDevice(device: IotDevice) {
  if (!window.confirm(`Remove the device "${device.name}"?`)) return
  try {
    await devicesApi.remove(device.id)
    message.value = `Device "${device.name}" removed.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

onMounted(load)
</script>

<template>
  <div class="iot-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>
    <div v-if="message" class="notice notice-success">
      <AppIcon name="device" :size="18" /> {{ message }}
      <button type="button" class="dismiss" @click="message = ''">Dismiss</button>
    </div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Field telemetry</p>
        <h2>IoT devices</h2>
        <p class="subtitle">Monitor field sensors, link devices to land parcels and inspect the latest telemetry.</p>
        <div class="scope-line">
          <span><AppIcon name="device" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!canManage" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button type="button" class="btn btn-secondary" @click="load">
          <AppIcon name="refresh" :size="16" /> Refresh
        </button>
        <button v-if="canManage" type="button" class="btn btn-primary" @click="openDeviceModal">
          <AppIcon name="plus" :size="16" /> Register device
        </button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading device fleet...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Registered devices</span><strong>{{ formatNumber(overview.metrics.devices) }}</strong><small>Across this organisation</small></div>
        <div class="stat"><span>Reporting · 24 hours</span><strong>{{ formatNumber(overview.metrics.reportingLast24Hours) }}</strong><small>{{ reportingShare }}% of the fleet</small></div>
        <div class="stat"><span>Readings · 24 hours</span><strong>{{ formatNumber(overview.metrics.readingsLast24Hours) }}</strong><small>Stored telemetry records</small></div>
        <div class="stat"><span>Average moisture</span><strong>{{ overview.metrics.averageSoilMoisture == null ? '—' : `${formatNumber(overview.metrics.averageSoilMoisture)}%` }}</strong><small>{{ formatNumber(overview.metrics.rainfallLast24Hours) }} mm rainfall captured</small></div>
      </section>

      <div class="iot-layout">
        <section class="panel fleet-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Device register</p>
              <h3>Field fleet</h3>
              <p class="panel-subtitle">{{ filteredDevices.length }} devices · {{ formatNumber(filteredDevices.reduce((total, device) => total + device.readingCount, 0)) }} readings</p>
            </div>
            <label class="search-field">
              <AppIcon name="search" :size="16" />
              <input v-model="search" type="search" placeholder="Search devices or parcels..." />
            </label>
          </header>

          <div v-if="filteredDevices.length === 0" class="empty-panel">
            <span class="empty-icon"><AppIcon name="device" :size="26" /></span>
            <h4>{{ devices.length === 0 ? 'No devices registered' : 'No devices match this search' }}</h4>
            <p>
              {{ devices.length === 0
                ? 'Register a field sensor and connect it to a land parcel to start collecting readings.'
                : 'Adjust the search term to find another device.' }}
            </p>
            <button v-if="canManage && devices.length === 0" type="button" class="btn btn-primary" @click="openDeviceModal">
              <AppIcon name="plus" :size="16" /> Register first device
            </button>
          </div>

          <div v-else class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Device</th>
                  <th>Location</th>
                  <th>Last reading</th>
                  <th class="numeric">Readings</th>
                  <th class="numeric">Moisture</th>
                  <th class="numeric">Rain</th>
                  <th>Heartbeat</th>
                  <th class="actions-col"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="device in filteredDevices" :key="device.id">
                  <td>
                    <div class="device-cell">
                      <span class="device-icon"><AppIcon name="device" :size="16" /></span>
                      <span>
                        <strong>{{ device.name }}</strong>
                        <small>{{ device.deviceId }}</small>
                      </span>
                    </div>
                  </td>
                  <td>
                    <div class="location-cell">
                      <strong>{{ device.landParcelName || device.plotName || 'Unassigned' }}</strong>
                      <small>{{ device.farmName || 'No farm link' }}</small>
                    </div>
                  </td>
                  <td class="date-cell">{{ device.lastReadingAt ? formatDateTime(device.lastReadingAt) : 'No readings' }}</td>
                  <td class="numeric strong-cell">{{ formatNumber(device.readingCount) }}</td>
                  <td class="numeric">{{ device.lastSoilMoisture == null ? '—' : `${formatNumber(device.lastSoilMoisture)}%` }}</td>
                  <td class="numeric">{{ device.lastRainfall == null ? '—' : `${formatNumber(device.lastRainfall)} mm` }}</td>
                  <td><span class="heartbeat" :class="{ live: device.reportingLast24h }"><span class="dot"></span>{{ device.reportingLast24h ? 'Reporting' : 'No recent data' }}</span></td>
                  <td class="actions-cell">
                    <button v-if="canManage" type="button" class="row-action" @click="openReadingModal(device)">Simulate reading</button>
                    <button v-if="canManage" type="button" class="row-action danger" @click="deleteDevice(device)">Remove</button>
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
                <p class="panel-eyebrow">Fleet health</p>
                <h3>Reporting coverage</h3>
              </div>
            </header>
            <div class="fleet-health">
              <div class="health-ring" :style="{ '--coverage': `${reportingShare}%` }">
                <span>{{ reportingShare }}%</span>
              </div>
              <div class="health-copy">
                <strong>{{ overview.metrics.reportingLast24Hours }} of {{ overview.metrics.devices }} devices reporting</strong>
                <p>Devices are marked as reporting when a reading has been stored in the last 24 hours.</p>
              </div>
            </div>
            <div class="health-note">
              <AppIcon name="warning" :size="14" />
              No hardware provisioning or device credentials are configured yet; readings are submitted through the authenticated telemetry endpoint.
            </div>
          </section>

          <section class="panel">
            <header class="panel-head">
              <div>
                <p class="panel-eyebrow">Telemetry log</p>
                <h3>Recent readings</h3>
              </div>
              <span class="count-badge">{{ overview.recentReadings.length }}</span>
            </header>
            <ul v-if="overview.recentReadings.length" class="reading-list">
              <li v-for="reading in overview.recentReadings.slice(0, 7)" :key="reading.id">
                <span class="reading-icon"><AppIcon name="device" :size="14" /></span>
                <span class="reading-copy">
                  <strong>{{ reading.deviceName || reading.deviceId }}</strong>
                  <small>{{ reading.landParcelName || 'Unassigned' }} · {{ formatDateTime(reading.timestamp) }}</small>
                </span>
                <span class="reading-values">{{ formatNumber(reading.soilMoisture) }}%<small>{{ formatNumber(reading.rainfall) }} mm</small></span>
              </li>
            </ul>
            <div v-else class="compact-empty"><p>No telemetry has been captured for this workspace.</p></div>
          </section>
        </aside>
      </div>
    </template>

    <AppModal v-if="showDeviceModal" title="Register a field device" subtitle="Link the device to the land parcel it monitors." @close="showDeviceModal = false">
      <form id="device-form" class="form-grid" @submit.prevent="saveDevice">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2">
          <span>Device name</span>
          <input v-model="deviceForm.name" maxlength="120" placeholder="e.g. North parcel soil probe" autofocus />
        </label>
        <label class="field span-2">
          <span>Device ID</span>
          <input v-model="deviceForm.deviceId" maxlength="80" placeholder="e.g. field-sensor-03" />
        </label>
        <label class="field span-2">
          <span>Land parcel</span>
          <select v-model.number="deviceForm.landParcelId">
            <option :value="null">No parcel assigned</option>
            <option v-for="parcel in parcels" :key="parcel.id" :value="parcel.id">{{ parcel.name }} · {{ parcel.farmName }}</option>
          </select>
        </label>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showDeviceModal = false">Cancel</button>
        <button type="submit" form="device-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : 'Register device' }}</button>
      </template>
    </AppModal>

    <AppModal
      v-if="showReadingModal && readingDevice"
      title="Submit a telemetry reading"
      :subtitle="`${readingDevice.name} · ${readingDevice.deviceId}`"
      @close="showReadingModal = false"
    >
      <form id="reading-form" class="form-grid" @submit.prevent="saveReading">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field">
          <span>Soil moisture (%)</span>
          <input v-model.number="readingForm.soilMoisture" type="number" min="0" max="100" step="0.1" />
        </label>
        <label class="field">
          <span>Rainfall (mm)</span>
          <input v-model.number="readingForm.rainfall" type="number" min="0" max="10000" step="0.1" />
        </label>
        <p class="reading-note span-2">
          The reading is stored against the device and broadcast over the authenticated SignalR hub.
        </p>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showReadingModal = false">Cancel</button>
        <button type="submit" form="reading-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Submitting...' : 'Submit reading' }}</button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.iot-page { color: #1d3525; }

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

.btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; min-height: 36px; border: 1px solid transparent; border-radius: 9px; padding: 0.48rem 0.75rem; font-size: 0.75rem; font-weight: 700; text-decoration: none; cursor: pointer; }
.btn-primary { background: #1d5a39; border-color: #1d5a39; color: #fff; box-shadow: 0 5px 13px rgba(29, 90, 57, 0.18); }
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

.iot-layout { display: grid; grid-template-columns: minmax(0, 1.75fr) minmax(300px, 0.8fr); gap: 0.8rem; align-items: start; }
.side-stack { display: flex; flex-direction: column; gap: 0.8rem; }
.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.panel-subtitle { margin: 0.18rem 0 0; color: #8a978c; font-size: 0.64rem; }
.search-field { display: flex; align-items: center; gap: 0.4rem; width: 250px; padding: 0.42rem 0.55rem; border: 1px solid #dfe7df; border-radius: 8px; background: #f7faf7; color: #7c8e80; }
.search-field input { min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; color: #24412d; font-size: 0.7rem; }

.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.7rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.7rem; border-top: 1px solid #eef2ee; color: #4c6353; white-space: nowrap; }
.device-cell { display: flex; align-items: center; gap: 0.45rem; }
.device-icon { display: grid; place-items: center; width: 28px; height: 28px; flex: 0 0 auto; border-radius: 8px; background: #eef4f9; color: #3e7298; }
.device-cell > span:last-child, .location-cell { display: flex; flex-direction: column; }
.device-cell strong, .location-cell strong { color: #294931; font-size: 0.7rem; }
.device-cell small, .location-cell small { color: #939f94; font-size: 0.58rem; }
.date-cell { color: #607765; font-size: 0.64rem; }
.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.heartbeat { display: inline-flex; align-items: center; gap: 0.3rem; color: #8a978c; font-size: 0.58rem; font-weight: 700; white-space: nowrap; }
.heartbeat .dot { width: 6px; height: 6px; border-radius: 50%; background: #b9c3bb; }
.heartbeat.live { color: #2a7a4e; }
.heartbeat.live .dot { background: #3da46c; box-shadow: 0 0 0 3px rgba(61, 164, 108, 0.13); }
.actions-col { width: 142px; }
.actions-cell { text-align: right; }
.row-action { border: 0; background: transparent; color: #2f704a; font-size: 0.6rem; font-weight: 700; cursor: pointer; }
.row-action + .row-action { margin-left: 0.4rem; }
.row-action.danger { color: #9b4141; }

.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 3rem 1.5rem; text-align: center; }
.empty-panel h4 { margin: 0; color: #2b4a34; font-size: 0.9rem; }
.empty-panel p { max-width: 420px; margin: 0; color: #849287; font-size: 0.72rem; line-height: 1.55; }
.empty-icon { display: grid; place-items: center; width: 50px; height: 50px; border-radius: 14px; background: #eef4f9; color: #4b7899; }

.fleet-health { display: flex; align-items: center; gap: 0.8rem; padding: 1rem; }
.health-ring { display: grid; place-items: center; width: 82px; height: 82px; flex: 0 0 auto; border-radius: 50%; background: conic-gradient(#4a9d70 var(--coverage), #edf2ed 0); position: relative; }
.health-ring::before { content: ''; position: absolute; inset: 7px; border-radius: 50%; background: #fff; }
.health-ring span { position: relative; color: #27553a; font-size: 0.82rem; font-weight: 800; }
.health-copy strong { display: block; color: #294931; font-size: 0.7rem; line-height: 1.45; }
.health-copy p { margin: 0.3rem 0 0; color: #849287; font-size: 0.63rem; line-height: 1.5; }
.health-note { display: flex; gap: 0.4rem; margin: 0 1rem 1rem; padding: 0.55rem 0.6rem; border-radius: 8px; background: #fbf8ed; color: #867347; font-size: 0.6rem; line-height: 1.45; }
.health-note svg { flex: 0 0 auto; margin-top: 0.05rem; }

.reading-list { list-style: none; margin: 0; padding: 0 0.7rem 0.7rem; }
.reading-list li { display: flex; align-items: center; gap: 0.5rem; padding: 0.55rem 0.2rem; border-top: 1px solid #eef2ee; }
.reading-list li:first-child { border-top: 0; }
.reading-icon { display: grid; place-items: center; width: 27px; height: 27px; flex: 0 0 auto; border-radius: 7px; background: #eef4f9; color: #4b7899; }
.reading-copy { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.reading-copy strong { overflow: hidden; color: #294931; font-size: 0.68rem; text-overflow: ellipsis; white-space: nowrap; }
.reading-copy small { overflow: hidden; color: #929e94; font-size: 0.57rem; text-overflow: ellipsis; white-space: nowrap; }
.reading-values { flex: 0 0 auto; color: #3e6049; font-size: 0.66rem; font-weight: 800; text-align: right; }
.reading-values small { display: block; color: #929e94; font-size: 0.55rem; font-weight: 600; }
.count-badge { display: inline-flex; align-items: center; justify-content: center; min-width: 22px; height: 22px; border-radius: 999px; background: #eef4f9; color: #3e7298; font-size: 0.6rem; font-weight: 800; }
.compact-empty { padding: 1rem; color: #879589; font-size: 0.68rem; line-height: 1.5; }
.compact-empty p { margin: 0; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
.reading-note { grid-column: 1 / -1; margin: 0; color: #849287; font-size: 0.65rem; line-height: 1.5; }

@media (max-width: 1150px) {
  .iot-layout { grid-template-columns: 1fr; }
  .side-stack { display: grid; grid-template-columns: 1fr 1fr; }
}

@media (max-width: 900px) {
  .page-intro { flex-direction: column; align-items: flex-start; }
  .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .side-stack { grid-template-columns: 1fr; }
  .panel-head { flex-direction: column; }
  .search-field { width: 100%; }
}

@media (max-width: 620px) {
  .stat-row { grid-template-columns: 1fr; }
  .form-grid { grid-template-columns: 1fr; }
  .field.span-2 { grid-column: auto; }
  .fleet-health { flex-direction: column; align-items: flex-start; }
}
</style>
