<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { enterpriseApi, type EnterpriseOverview, type LandParcel } from '@/api/enterprise'
import { useAuthStore } from '@/store/auth'
import { useWorkspaceStore } from '@/store/workspace'
import LandParcelMap from '@/components/LandParcelMap.vue'
import AppIcon from '@/components/AppIcon.vue'
import {
  formatDateTime,
  formatHectares,
  formatNumber,
  greeting,
  percentage,
  statusClass,
  titleCase
} from '@/utils/enterprise'

const router = useRouter()
const workspace = useWorkspaceStore()
const authStore = useAuthStore()

const overview = ref<EnterpriseOverview | null>(null)
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const selectedParcel = ref<LandParcel | null>(null)
const mapRef = ref<InstanceType<typeof LandParcelMap> | null>(null)

const landUseColors = ['#2d7a55', '#d49a3a', '#517fa4', '#8d6fa1', '#81a25a', '#c27352', '#708d81']

const displayName = computed(() =>
  workspace.context?.fullName || authStore.user?.fullName || authStore.user?.email || 'there'
)

const activeParcels = computed(() => parcels.value.filter((parcel) => parcel.status === 'Active').length)
const activeShare = computed(() => percentage(activeParcels.value, parcels.value.length))
const uniqueRegions = computed(() =>
  new Set(overview.value?.farms.map((farm) => farm.region).filter(Boolean)).size
)
const landUseTotal = computed(() =>
  (overview.value?.landUse ?? []).reduce((total, item) => total + Number(item.hectares), 0)
)
const overviewParcels = computed(() =>
  [...(overview.value?.recentParcels ?? [])].sort((a, b) => b.hectares - a.hectares).slice(0, 5)
)
const latestReading = computed(() => overview.value?.recentReadings[0] ?? null)

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.message
    || 'The workspace overview could not be loaded.'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const organizationId = workspace.organizationId
    const [overviewResponse, parcelsResponse] = await Promise.all([
      enterpriseApi.overview(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = overviewResponse.data
    parcels.value = parcelsResponse.data.items
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function selectParcel(parcel: LandParcel) {
  selectedParcel.value = parcel
}

function selectRecentParcel(id: number) {
  const parcel = parcels.value.find((item) => item.id === id)
  if (!parcel) return
  selectedParcel.value = parcel
  mapRef.value?.focusParcel(parcel.id)
}

function openParcelRegister() {
  router.push({ name: 'Farms', query: { new: 'parcel' } })
}

onMounted(load)
</script>

<template>
  <div class="dashboard-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" />
      <span>{{ error }}</span>
      <button type="button" @click="load">Retry</button>
    </div>

    <div v-if="loading" class="loading-grid">
      <div class="loading-hero"></div>
      <div v-for="index in 4" :key="index" class="loading-card"></div>
      <div class="loading-map"></div>
    </div>

    <template v-else-if="overview">
      <section class="hero">
        <div class="hero-copy">
          <p class="eyebrow">
            <span class="eyebrow-dot"></span>
            Enterprise overview · {{ overview.organization.name }}
          </p>
          <h2>{{ greeting() }}, {{ displayName }}</h2>
          <p class="hero-subtitle">
            A live view of land, production assets and field telemetry across
            {{ overview.metrics.farms }} {{ overview.metrics.farms === 1 ? 'farm' : 'farms' }}.
          </p>
          <div class="hero-meta">
            <span><AppIcon name="calendar" :size="14" /> {{ new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }) }}</span>
            <span><span class="live-dot"></span> Live workspace data</span>
          </div>
        </div>
        <div class="hero-actions">
          <router-link to="/gis" class="btn btn-secondary">
            <AppIcon name="map" :size="17" />
            Open GIS explorer
          </router-link>
          <button v-if="overview.canManageParcels" type="button" class="btn btn-primary" @click="openParcelRegister">
            <AppIcon name="plus" :size="17" />
            New land parcel
          </button>
        </div>
      </section>

      <section class="metric-grid" aria-label="Workspace metrics">
        <article class="metric-card">
          <div class="metric-top">
            <span class="metric-icon green"><AppIcon name="layers" :size="20" /></span>
            <span class="metric-label">Land under management</span>
          </div>
          <strong>{{ formatHectares(overview.metrics.hectares) }}</strong>
          <p>Across {{ formatNumber(overview.metrics.parcels) }} registered parcels</p>
          <span class="metric-foot">{{ activeShare }}% currently active</span>
        </article>

        <article class="metric-card">
          <div class="metric-top">
            <span class="metric-icon amber"><AppIcon name="farm" :size="20" /></span>
            <span class="metric-label">Farms</span>
          </div>
          <strong>{{ formatNumber(overview.metrics.farms) }}</strong>
          <p>{{ uniqueRegions }} {{ uniqueRegions === 1 ? 'region' : 'regions' }} represented</p>
          <span class="metric-foot">{{ overview.farms.length ? overview.farms.map((farm) => farm.name).slice(0, 2).join(' · ') : 'No farms yet' }}</span>
        </article>

        <article class="metric-card">
          <div class="metric-top">
            <span class="metric-icon blue"><AppIcon name="device" :size="20" /></span>
            <span class="metric-label">Registered devices</span>
          </div>
          <strong>{{ formatNumber(overview.metrics.devices) }}</strong>
          <p>{{ formatNumber(overview.metrics.readingsToday) }} readings captured today</p>
          <span class="metric-foot">
            {{ latestReading ? `Latest: ${latestReading.deviceId}` : 'No readings in this workspace' }}
          </span>
        </article>

        <article class="metric-card">
          <div class="metric-top">
            <span class="metric-icon purple"><AppIcon name="satellite" :size="20" /></span>
            <span class="metric-label">Weather observations</span>
          </div>
          <strong>{{ formatNumber(overview.metrics.weatherObservations) }}</strong>
          <p>{{ overview.latestWeather ? 'Latest observation stored' : 'No stored observations' }}</p>
          <span class="metric-foot">{{ overview.latestWeather ? formatDateTime(overview.latestWeather.fetchedAtUtc) : 'Connect a weather provider to collect data' }}</span>
        </article>
      </section>

      <section class="dashboard-grid primary-grid">
        <article class="panel map-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Spatial register</p>
              <h3>Land parcel map</h3>
              <p class="panel-subtitle">Select a parcel to inspect its farm, use and area.</p>
            </div>
            <router-link to="/gis" class="text-link">Expand map <AppIcon name="arrow" :size="14" /></router-link>
          </header>

          <div class="map-body">
            <LandParcelMap
              ref="mapRef"
              :parcels="parcels"
              :selected-id="selectedParcel?.id ?? null"
              height="390px"
              @select="selectParcel"
            >
              <template #empty>
                <button v-if="overview.canManageParcels" type="button" class="btn btn-primary small" @click="openParcelRegister">
                  Register the first parcel
                </button>
              </template>
            </LandParcelMap>
          </div>

          <div v-if="selectedParcel" class="selected-strip">
            <div class="selected-marker"><AppIcon name="pin" :size="17" /></div>
            <div class="selected-main">
              <strong>{{ selectedParcel.name }}</strong>
              <span>{{ selectedParcel.farmName }} · {{ titleCase(selectedParcel.landUse) }}</span>
            </div>
            <span class="selected-area">{{ formatHectares(selectedParcel.areaHectares) }}</span>
            <span class="status-pill" :class="statusClass(selectedParcel.status)">{{ titleCase(selectedParcel.status) }}</span>
          </div>
          <p v-else class="map-caption">Your organization's registered land parcels are shown above.</p>
        </article>

        <article class="panel land-use-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Composition</p>
              <h3>Land use</h3>
              <p class="panel-subtitle">Area distribution across the registered estate.</p>
            </div>
          </header>

          <div v-if="overview.landUse.length" class="land-use-list">
            <div v-for="(item, index) in overview.landUse" :key="item.landUse" class="land-use-item">
              <div class="land-use-top">
                <span class="land-use-name">
                  <span class="land-use-swatch" :style="{ background: landUseColors[index % landUseColors.length] }"></span>
                  {{ titleCase(item.landUse) }}
                </span>
                <strong>{{ formatHectares(item.hectares) }}</strong>
              </div>
              <div class="bar-track">
                <span
                  class="bar-fill"
                  :style="{
                    width: `${percentage(Number(item.hectares), landUseTotal)}%`,
                    background: landUseColors[index % landUseColors.length]
                  }"
                ></span>
              </div>
              <span class="land-use-count">{{ item.parcels }} {{ item.parcels === 1 ? 'parcel' : 'parcels' }}</span>
            </div>
          </div>
          <div v-else class="panel-empty">
            <span class="empty-icon"><AppIcon name="layers" :size="22" /></span>
            <p>Land use composition appears once parcels are registered.</p>
          </div>
        </article>
      </section>

      <section class="dashboard-grid secondary-grid">
        <article class="panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Farm register</p>
              <h3>Farm performance</h3>
            </div>
            <router-link to="/farms" class="text-link">Manage farms <AppIcon name="arrow" :size="14" /></router-link>
          </header>

          <div v-if="overview.farms.length" class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Farm</th>
                  <th>Region</th>
                  <th class="numeric">Parcels</th>
                  <th class="numeric">Area</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="farm in overview.farms" :key="farm.id">
                  <td>
                    <div class="farm-cell">
                      <span class="farm-badge">{{ farm.name.slice(0, 1).toUpperCase() }}</span>
                      <span><strong>{{ farm.name }}</strong><small>{{ farm.code || 'No farm code' }}</small></span>
                    </div>
                  </td>
                  <td class="muted-cell">{{ farm.region || 'Not set' }}</td>
                  <td class="numeric">{{ formatNumber(farm.parcelCount) }}</td>
                  <td class="numeric strong-cell">{{ formatHectares(farm.hectares) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <div v-else class="panel-empty aligned">
            <p>No farms have been registered in this organization.</p>
            <router-link to="/farms" class="btn btn-secondary small">Open farm register</router-link>
          </div>
        </article>

        <article class="panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Latest additions</p>
              <h3>Recent land parcels</h3>
            </div>
            <router-link to="/gis" class="text-link">View map <AppIcon name="arrow" :size="14" /></router-link>
          </header>

          <ul v-if="overviewParcels.length" class="parcel-list">
            <li v-for="parcel in overviewParcels" :key="parcel.id">
              <button type="button" class="parcel-row" @click="selectRecentParcel(parcel.id)">
                <span class="parcel-icon"><AppIcon name="map" :size="16" /></span>
                <span class="parcel-copy">
                  <strong>{{ parcel.name }}</strong>
                  <small>{{ parcel.farmName }} · {{ titleCase(parcel.landUse) }}</small>
                </span>
                <span class="parcel-area">{{ formatHectares(parcel.hectares) }}</span>
                <span class="status-pill" :class="statusClass(parcel.status)">{{ titleCase(parcel.status) }}</span>
              </button>
            </li>
          </ul>
          <div v-else class="panel-empty">
            <span class="empty-icon"><AppIcon name="farm" :size="22" /></span>
            <p>Recent parcels will appear here after registration.</p>
          </div>
        </article>
      </section>

      <section class="dashboard-grid telemetry-grid">
        <article class="panel weather-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Conditions</p>
              <h3>Weather</h3>
            </div>
            <span class="muted-label">Latest observation</span>
          </header>

          <div v-if="overview.latestWeather" class="weather-now">
            <div class="weather-main">
              <span class="weather-icon"><AppIcon name="weather" :size="30" /></span>
              <div>
                <strong>{{ overview.latestWeather.temperatureC.toFixed(1) }}°C</strong>
                <p>{{ overview.latestWeather.description || overview.latestWeather.condition }}</p>
              </div>
            </div>
            <div class="weather-stats">
              <span><small>Feels like</small><strong>{{ overview.latestWeather.feelsLikeC.toFixed(1) }}°C</strong></span>
              <span><small>Humidity</small><strong>{{ overview.latestWeather.humidity.toFixed(0) }}%</strong></span>
              <span><small>Wind</small><strong>{{ overview.latestWeather.windSpeed.toFixed(1) }} m/s</strong></span>
              <span><small>Location</small><strong>{{ overview.latestWeather.location }}</strong></span>
            </div>
            <p class="weather-time">Recorded {{ formatDateTime(overview.latestWeather.fetchedAtUtc) }}</p>
          </div>
          <div v-else class="panel-empty">
            <span class="empty-icon"><AppIcon name="weather" :size="22" /></span>
            <p>No weather observations are stored for this workspace yet.</p>
            <router-link to="/modules/weather" class="text-link">Weather module roadmap <AppIcon name="arrow" :size="13" /></router-link>
          </div>
        </article>

        <article class="panel telemetry-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Field devices</p>
              <h3>Recent telemetry</h3>
            </div>
            <router-link to="/modules/iot" class="text-link">IoT module <AppIcon name="arrow" :size="14" /></router-link>
          </header>

          <ul v-if="overview.recentReadings.length" class="reading-list">
            <li v-for="reading in overview.recentReadings" :key="reading.id">
              <span class="reading-icon"><AppIcon name="device" :size="16" /></span>
              <span class="reading-copy">
                <strong>{{ reading.deviceId }}</strong>
                <small>{{ formatDateTime(reading.timestamp) }}</small>
              </span>
              <span class="reading-stat"><small>Moisture</small><strong>{{ reading.soilMoisture.toFixed(1) }}%</strong></span>
              <span class="reading-stat"><small>Rain</small><strong>{{ reading.rainfall.toFixed(1) }} mm</strong></span>
            </li>
          </ul>
          <div v-else class="panel-empty">
            <span class="empty-icon"><AppIcon name="device" :size="22" /></span>
            <p>No field telemetry has been captured for this workspace.</p>
            <router-link to="/field-map" class="text-link">Open field sensor monitor <AppIcon name="arrow" :size="13" /></router-link>
          </div>
        </article>
      </section>

      <footer class="dashboard-foot">
        <span><span class="live-dot"></span> Data sourced from the GeoFarmland API</span>
        <button type="button" class="text-link" @click="load"><AppIcon name="refresh" :size="14" /> Refresh workspace</button>
      </footer>
    </template>
  </div>
</template>

<style scoped>
.dashboard-page {
  color: #1d3525;
}

.notice {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  margin-bottom: 1rem;
  padding: 0.75rem 0.9rem;
  border-radius: 10px;
  font-size: 0.82rem;
}

.notice-error {
  border: 1px solid #f0c9c9;
  background: #fdf0f0;
  color: #8d2f2f;
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

.hero {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1.5rem;
  margin-bottom: 1.2rem;
}

.eyebrow {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin: 0 0 0.35rem;
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

.hero h2 {
  margin: 0;
  color: #15311f;
  font-size: clamp(1.55rem, 2.2vw, 2.1rem);
  letter-spacing: -0.035em;
  line-height: 1.15;
}

.hero-subtitle {
  max-width: 690px;
  margin: 0.45rem 0 0;
  color: #6c7e70;
  font-size: 0.86rem;
  line-height: 1.55;
}

.hero-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  margin-top: 0.65rem;
  color: #7c8d80;
  font-size: 0.68rem;
  font-weight: 600;
}

.hero-meta span {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.live-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #39a267;
  box-shadow: 0 0 0 3px rgba(57, 162, 103, 0.13);
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
  flex: 0 0 auto;
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
  border-color: #b8ccbc;
  background: #f7faf7;
}

.btn.small {
  min-height: 31px;
  padding: 0.35rem 0.62rem;
  font-size: 0.7rem;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.75rem;
  margin-bottom: 0.85rem;
}

.metric-card {
  position: relative;
  min-width: 0;
  padding: 0.95rem 1rem;
  border: 1px solid #e1e9e1;
  border-radius: 13px;
  background: #fff;
  box-shadow: 0 4px 14px rgba(23, 57, 34, 0.045);
  overflow: hidden;
}

.metric-card::after {
  content: '';
  position: absolute;
  right: -32px;
  top: -40px;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  background: #f1f7f2;
  opacity: 0.8;
}

.metric-top {
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.7rem;
}

.metric-icon {
  display: grid;
  place-items: center;
  width: 31px;
  height: 31px;
  border-radius: 9px;
}

.metric-icon.green { background: #e6f3e9; color: #297a4d; }
.metric-icon.amber { background: #fff5dd; color: #a96f14; }
.metric-icon.blue { background: #eaf2f8; color: #3b7198; }
.metric-icon.purple { background: #f2edf8; color: #7d5d96; }

.metric-label {
  color: #738477;
  font-size: 0.66rem;
  font-weight: 800;
  letter-spacing: 0.055em;
  text-transform: uppercase;
}

.metric-card > strong {
  position: relative;
  z-index: 1;
  display: block;
  color: #15341f;
  font-size: 1.5rem;
  letter-spacing: -0.035em;
  line-height: 1.1;
}

.metric-card > p {
  position: relative;
  z-index: 1;
  margin: 0.28rem 0 0;
  color: #6d7f71;
  font-size: 0.73rem;
}

.metric-foot {
  display: block;
  margin-top: 0.7rem;
  padding-top: 0.55rem;
  border-top: 1px solid #edf2ed;
  color: #829187;
  font-size: 0.66rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dashboard-grid {
  display: grid;
  gap: 0.85rem;
  margin-bottom: 0.85rem;
}

.primary-grid {
  grid-template-columns: minmax(0, 2.05fr) minmax(290px, 0.95fr);
}

.secondary-grid {
  grid-template-columns: minmax(0, 1.45fr) minmax(320px, 1fr);
}

.telemetry-grid {
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
}

.panel {
  min-width: 0;
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
  padding: 0.95rem 1rem 0.7rem;
}

.panel-eyebrow {
  margin: 0 0 0.16rem;
  color: #829286;
  font-size: 0.6rem;
  font-weight: 800;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.panel-head h3 {
  margin: 0;
  color: #1b3824;
  font-size: 0.94rem;
  letter-spacing: -0.015em;
}

.panel-subtitle {
  margin: 0.22rem 0 0;
  color: #849286;
  font-size: 0.7rem;
  line-height: 1.45;
}

.text-link {
  display: inline-flex;
  align-items: center;
  gap: 0.28rem;
  border: 0;
  background: transparent;
  color: #34734f;
  font-size: 0.68rem;
  font-weight: 700;
  text-decoration: none;
  white-space: nowrap;
  cursor: pointer;
}

.text-link:hover {
  color: #184c2f;
}

.map-body {
  padding: 0 0.75rem;
}

.selected-strip {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  margin: 0.75rem;
  padding: 0.55rem 0.7rem;
  border: 1px solid #e4ece4;
  border-radius: 9px;
  background: #f8fbf8;
}

.selected-marker {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  border-radius: 8px;
  background: #e8f3eb;
  color: #2a8051;
}

.selected-main {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.selected-main strong {
  overflow: hidden;
  color: #24432f;
  font-size: 0.75rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.selected-main span {
  overflow: hidden;
  color: #829187;
  font-size: 0.66rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.selected-area {
  color: #36583f;
  font-size: 0.72rem;
  font-weight: 800;
  white-space: nowrap;
}

.map-caption {
  margin: 0;
  padding: 0.55rem 1rem 0.8rem;
  color: #889589;
  font-size: 0.66rem;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  padding: 0.18rem 0.45rem;
  font-size: 0.6rem;
  font-weight: 800;
  white-space: nowrap;
}

.status-active { background: #e8f5ec; color: #28734a; }
.status-fallow { background: #fff6df; color: #956315; }
.status-development { background: #eaf3fa; color: #346b93; }
.status-archived { background: #eff1ef; color: #6c786f; }
.status-neutral { background: #f1f4f1; color: #6d7a70; }

.land-use-list {
  padding: 0.35rem 1rem 0.95rem;
}

.land-use-item {
  padding: 0.55rem 0;
  border-bottom: 1px solid #f0f4f0;
}

.land-use-item:last-child {
  border-bottom: 0;
}

.land-use-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.6rem;
  color: #415d48;
  font-size: 0.7rem;
}

.land-use-name {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-weight: 700;
}

.land-use-swatch {
  width: 7px;
  height: 7px;
  border-radius: 2px;
}

.land-use-top strong {
  font-size: 0.68rem;
  white-space: nowrap;
}

.bar-track {
  height: 5px;
  margin-top: 0.4rem;
  border-radius: 999px;
  background: #f0f4f0;
  overflow: hidden;
}

.bar-fill {
  display: block;
  height: 100%;
  border-radius: inherit;
  min-width: 3px;
}

.land-use-count {
  display: block;
  margin-top: 0.22rem;
  color: #929e94;
  font-size: 0.6rem;
}

.table-wrap {
  overflow-x: auto;
  padding: 0 0.4rem 0.4rem;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.72rem;
}

th {
  padding: 0.55rem 0.65rem;
  color: #8b978d;
  font-size: 0.59rem;
  font-weight: 800;
  letter-spacing: 0.07em;
  text-align: left;
  text-transform: uppercase;
}

td {
  padding: 0.55rem 0.65rem;
  border-top: 1px solid #eef2ee;
  color: #4c6353;
}

.numeric {
  text-align: right;
}

.strong-cell {
  color: #24432e;
  font-weight: 700;
}

.muted-cell {
  color: #859287;
}

.farm-cell {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
}

.farm-badge {
  display: grid;
  place-items: center;
  width: 27px;
  height: 27px;
  flex: 0 0 auto;
  border-radius: 7px;
  background: #eaf3eb;
  color: #2f754d;
  font-size: 0.65rem;
  font-weight: 800;
}

.farm-cell span:last-child {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.farm-cell strong {
  overflow: hidden;
  color: #25442f;
  font-size: 0.72rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.farm-cell small {
  color: #929d93;
  font-size: 0.6rem;
}

.parcel-list,
.reading-list {
  list-style: none;
  margin: 0;
  padding: 0 0.65rem 0.65rem;
}

.parcel-list li + li,
.reading-list li + li {
  border-top: 1px solid #eef2ee;
}

.parcel-row {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  width: 100%;
  padding: 0.55rem 0.2rem;
  border: 0;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.parcel-row:hover .parcel-copy strong {
  color: #1f6b45;
}

.parcel-icon,
.reading-icon {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  flex: 0 0 auto;
  border-radius: 8px;
  background: #f0f6f1;
  color: #4a7c5b;
}

.parcel-copy,
.reading-copy {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.parcel-copy strong,
.reading-copy strong {
  overflow: hidden;
  color: #2a472f;
  font-size: 0.72rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.parcel-copy small,
.reading-copy small {
  overflow: hidden;
  color: #8d9b90;
  font-size: 0.62rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.parcel-area {
  color: #45624e;
  font-size: 0.68rem;
  font-weight: 700;
  white-space: nowrap;
}

.weather-now {
  padding: 0.2rem 1rem 1rem;
}

.weather-main {
  display: flex;
  align-items: center;
  gap: 0.7rem;
}

.weather-icon {
  display: grid;
  place-items: center;
  width: 50px;
  height: 50px;
  border-radius: 14px;
  background: #eef5f9;
  color: #427a9e;
}

.weather-main strong {
  display: block;
  color: #1b3b27;
  font-size: 1.65rem;
  letter-spacing: -0.04em;
}

.weather-main p {
  margin: 0.08rem 0 0;
  color: #798a7d;
  font-size: 0.7rem;
}

.weather-stats {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.45rem;
  margin-top: 0.75rem;
}

.weather-stats span {
  display: flex;
  flex-direction: column;
  padding: 0.45rem 0.55rem;
  border-radius: 8px;
  background: #f7faf7;
}

.weather-stats small,
.reading-stat small {
  color: #8c998e;
  font-size: 0.58rem;
}

.weather-stats strong {
  margin-top: 0.12rem;
  color: #355640;
  font-size: 0.7rem;
}

.weather-time {
  margin: 0.6rem 0 0;
  color: #929e93;
  font-size: 0.6rem;
}

.reading-list li {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.55rem 0.2rem;
}

.reading-stat {
  display: flex;
  flex-direction: column;
  min-width: 56px;
  text-align: right;
}

.reading-stat strong {
  color: #365a41;
  font-size: 0.69rem;
}

.panel-empty {
  display: grid;
  place-items: center;
  gap: 0.45rem;
  min-height: 150px;
  padding: 1.2rem;
  text-align: center;
}

.panel-empty.aligned {
  justify-items: start;
  text-align: left;
}

.panel-empty p {
  max-width: 280px;
  margin: 0;
  color: #849287;
  font-size: 0.72rem;
  line-height: 1.5;
}

.empty-icon {
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  border-radius: 13px;
  background: #f0f6f1;
  color: #558366;
}

.muted-label {
  color: #8d998f;
  font-size: 0.62rem;
  font-weight: 700;
  white-space: nowrap;
}

.dashboard-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.2rem 0.15rem;
  color: #849187;
  font-size: 0.63rem;
}

.dashboard-foot > span {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

.loading-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.75rem;
}

.loading-hero {
  grid-column: 1 / -1;
  height: 94px;
  border-radius: 12px;
  background: linear-gradient(100deg, #e9efe8 30%, #f5f8f4 50%, #e9efe8 70%);
  background-size: 220% 100%;
  animation: shimmer 1.4s infinite;
}

.loading-card {
  height: 126px;
  border-radius: 13px;
  background: linear-gradient(100deg, #e9efe8 30%, #f5f8f4 50%, #e9efe8 70%);
  background-size: 220% 100%;
  animation: shimmer 1.4s infinite;
}

.loading-map {
  grid-column: 1 / -1;
  height: 380px;
  border-radius: 13px;
  background: linear-gradient(100deg, #e9efe8 30%, #f5f8f4 50%, #e9efe8 70%);
  background-size: 220% 100%;
  animation: shimmer 1.4s infinite;
}

@keyframes shimmer {
  to { background-position: -220% 0; }
}

@media (max-width: 1200px) {
  .metric-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .primary-grid,
  .secondary-grid,
  .telemetry-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .hero {
    flex-direction: column;
    align-items: flex-start;
  }

  .hero-actions {
    width: 100%;
  }

  .hero-actions .btn {
    flex: 1;
  }

  .metric-grid {
    grid-template-columns: 1fr;
  }

  .weather-stats {
    grid-template-columns: 1fr;
  }

  .dashboard-foot {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
