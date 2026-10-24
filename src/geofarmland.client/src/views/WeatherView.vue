<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { monitoringApi, type ForecastItem, type LiveWeather, type WeatherHistoryItem } from '@/api/plots'
import { enterpriseApi, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import { formatDateTime, formatNumber } from '@/utils/enterprise'
import { plotCenterFromCoordinates } from '@/utils/plots'

const workspace = useWorkspaceStore()

const parcels = ref<LandParcel[]>([])
const history = ref<WeatherHistoryItem[]>([])
const forecast = ref<ForecastItem[]>([])
const selectedParcelId = ref<number | null>(null)
const liveWeather = ref<LiveWeather | null>(null)
const loading = ref(true)
const fetching = ref(false)
const error = ref('')
const liveError = ref('')

const selectedParcel = computed(() =>
  parcels.value.find((parcel) => parcel.id === selectedParcelId.value) ?? null
)
const latest = computed(() => history.value[0] ?? null)
const maxTemp = computed(() => Math.max(1, ...history.value.slice(0, 12).map((item) => item.temperatureC)))
const minTemp = computed(() => Math.min(...history.value.slice(0, 12).map((item) => item.temperatureC), 0))

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { status?: number; data?: { message?: string; condition?: string } }; message?: string }
  if (candidate.response?.status === 503 && candidate.response?.data?.condition) {
    return `${candidate.response.data.condition}. Add an OpenWeatherMap API key to enable live weather.`
  }
  return candidate.response?.data?.message
    || candidate.response?.data?.condition
    || candidate.message
    || 'The weather request could not be completed.'
}

async function loadHistory() {
  try {
    const { data } = await monitoringApi.weatherHistory()
    history.value = Array.isArray(data) ? data : []
  } catch (err) {
    error.value = errorMessage(err)
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const { data } = await enterpriseApi.parcels({ pageSize: 250 }, workspace.organizationId)
    parcels.value = data.items
    selectedParcelId.value = parcels.value[0]?.id ?? null
    await loadHistory()
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function onParcelChange() {
  liveWeather.value = null
  forecast.value = []
  liveError.value = ''
}

async function fetchLive() {
  if (!selectedParcel.value) {
    liveError.value = 'Select a land parcel before requesting live weather.'
    return
  }
  const center = plotCenterFromCoordinates(selectedParcel.value.coordinates)
  fetching.value = true
  liveError.value = ''
  liveWeather.value = null
  forecast.value = []
  try {
    const { data } = await monitoringApi.weather(center[0], center[1])
    liveWeather.value = data
    await loadHistory()
    try {
      const response = await monitoringApi.forecast(center[0], center[1])
      forecast.value = Array.isArray(response.data) ? response.data : []
    } catch (err) {
      liveError.value = errorMessage(err)
    }
  } catch (err) {
    liveError.value = errorMessage(err)
  } finally {
    fetching.value = false
  }
}

function barHeight(temp: number) {
  const span = Math.max(1, maxTemp.value - minTemp.value)
  return Math.max(4, Math.round(((temp - minTemp.value) / span) * 100))
}

onMounted(load)
</script>

<template>
  <div class="weather-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Environmental monitoring</p>
        <h2>Weather monitoring</h2>
        <p class="subtitle">Inspect stored weather observations and request live provider conditions for a land parcel.</p>
        <div class="scope-line">
          <span><AppIcon name="weather" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span class="provider-note">Provider: OpenWeatherMap</span>
        </div>
      </div>
      <div class="head-actions">
        <button type="button" class="btn btn-secondary" @click="loadHistory">
          <AppIcon name="refresh" :size="16" /> Refresh history
        </button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading weather observations...</div>

    <template v-else>
      <section class="weather-hero">
        <div class="live-panel">
          <div class="live-copy">
            <p class="panel-eyebrow">Live provider lookup</p>
            <h3>{{ selectedParcel ? selectedParcel.name : 'Select a land parcel' }}</h3>
            <p class="live-subtitle">A live lookup stores a weather snapshot in the workspace timeline.</p>
            <div class="selector-row">
              <select v-model.number="selectedParcelId" @change="onParcelChange">
                <option :value="null" disabled>Select a land parcel</option>
                <option v-for="parcel in parcels" :key="parcel.id" :value="parcel.id">{{ parcel.name }} · {{ parcel.farmName }}</option>
              </select>
              <button type="button" class="btn btn-primary" :disabled="fetching || !selectedParcel" @click="fetchLive">
                {{ fetching ? 'Fetching...' : 'Fetch live weather' }}
              </button>
            </div>
            <p v-if="liveError" class="live-error">{{ liveError }}</p>
          </div>

          <div class="live-readout">
            <template v-if="liveWeather">
              <span class="live-icon"><AppIcon name="weather" :size="30" /></span>
              <strong>{{ liveWeather.temperature }}</strong>
              <p>{{ liveWeather.description }}</p>
              <div class="live-stats">
                <span>Humidity<strong>{{ liveWeather.humidity }}</strong></span>
                <span>Wind<strong>{{ liveWeather.windSpeed }}</strong></span>
                <span>Location<strong>{{ liveWeather.location }}</strong></span>
              </div>
            </template>
            <template v-else-if="latest">
              <span class="live-icon"><AppIcon name="weather" :size="30" /></span>
              <strong>{{ latest.temperatureC.toFixed(1) }}°C</strong>
              <p>{{ latest.description }} · last stored observation</p>
              <div class="live-stats">
                <span>Humidity<strong>{{ latest.humidity.toFixed(0) }}%</strong></span>
                <span>Wind<strong>{{ latest.windSpeed.toFixed(1) }} m/s</strong></span>
                <span>Location<strong>{{ latest.location }}</strong></span>
              </div>
            </template>
            <template v-else>
              <span class="empty-readout-icon"><AppIcon name="weather" :size="30" /></span>
              <strong>No observation</strong>
              <p>No weather snapshot has been stored for this workspace.</p>
            </template>
          </div>
        </div>
      </section>

      <div class="weather-grid">
        <section class="panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Observation timeline</p>
              <h3>Stored weather history</h3>
            </div>
            <span class="count-badge">{{ history.length }}</span>
          </header>
          <div v-if="history.length" class="history-table-wrap">
            <table>
              <thead>
                <tr><th>Recorded</th><th class="numeric">Temp</th><th class="numeric">Humidity</th><th class="numeric">Wind</th><th>Condition</th><th>Location</th></tr>
              </thead>
              <tbody>
                <tr v-for="item in history.slice(0, 12)" :key="item.id">
                  <td>{{ formatDateTime(item.fetchedAtUtc) }}</td>
                  <td class="numeric strong-cell">{{ item.temperatureC.toFixed(1) }}°C</td>
                  <td class="numeric">{{ item.humidity.toFixed(0) }}%</td>
                  <td class="numeric">{{ item.windSpeed.toFixed(1) }} m/s</td>
                  <td>{{ item.description }}</td>
                  <td class="muted-cell">{{ item.location }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <div v-else class="empty-panel">
            <span class="empty-icon"><AppIcon name="weather" :size="25" /></span>
            <p>Weather history is empty. Configure <code>ExternalApis:OpenWeatherMap:ApiKey</code>, then fetch live weather for a parcel.</p>
          </div>
        </section>

        <aside class="side-stack">
          <section class="panel">
            <header class="panel-head">
              <div><p class="panel-eyebrow">Temperature shape</p><h3>Last 12 observations</h3></div>
            </header>
            <div v-if="history.length" class="mini-chart">
              <div v-for="item in history.slice(0, 12).reverse()" :key="item.id" class="mini-bar" :title="`${formatDateTime(item.fetchedAtUtc)} · ${item.temperatureC.toFixed(1)}°C`">
                <span :style="{ height: `${barHeight(item.temperatureC)}%` }"></span>
              </div>
            </div>
            <div v-else class="compact-empty"><p>No observations to chart.</p></div>
          </section>

          <section class="panel">
            <header class="panel-head">
              <div><p class="panel-eyebrow">Short-term</p><h3>Forecast</h3></div>
            </header>
            <ul v-if="forecast.length" class="forecast-list">
              <li v-for="item in forecast" :key="item.timeUtc">
                <span>{{ formatDateTime(item.timeUtc) }}</span>
                <strong>{{ item.temperatureC.toFixed(1) }}°C · {{ item.description }}</strong>
              </li>
            </ul>
            <div v-else class="compact-empty">
              <p>{{ liveError ? 'Forecast data is unavailable until a provider key is configured.' : 'Fetch live weather to request the next forecast window.' }}</p>
            </div>
          </section>
        </aside>
      </div>

      <section class="provider-banner">
        <span class="provider-icon"><AppIcon name="weather" :size="20" /></span>
        <div>
          <strong>Provider-backed observations only</strong>
          <p>GeoFarmland never fabricates weather readings. Live conditions and forecasts require a configured OpenWeatherMap key. Stored snapshots remain available without the provider.</p>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.weather-page { color: #1d3525; }
.notice { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.85rem; padding: 0.7rem 0.85rem; border-radius: 9px; font-size: 0.78rem; }
.notice-error { border: 1px solid #f0c9c9; background: #fdf0f0; color: #8d2f2f; }
.notice button { margin-left: auto; border: 0; background: transparent; color: inherit; font-weight: 700; cursor: pointer; text-decoration: underline; }
.page-intro { display: flex; align-items: flex-end; justify-content: space-between; gap: 1.2rem; margin-bottom: 0.85rem; }
.eyebrow { display: flex; align-items: center; gap: 0.4rem; margin: 0 0 0.3rem; color: #6e8273; font-size: 0.64rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.eyebrow-dot { width: 6px; height: 6px; border-radius: 50%; background: #3b9560; box-shadow: 0 0 0 3px rgba(59, 149, 96, 0.12); }
.page-intro h2 { margin: 0; color: #15311f; font-size: clamp(1.5rem, 2vw, 1.95rem); letter-spacing: -0.035em; }
.subtitle { margin: 0.35rem 0 0; color: #6c7e70; font-size: 0.82rem; }
.scope-line { display: flex; flex-wrap: wrap; gap: 0.7rem; margin-top: 0.55rem; color: #859388; font-size: 0.65rem; font-weight: 600; }
.scope-line span { display: inline-flex; align-items: center; gap: 0.3rem; }
.provider-note { padding: 0.18rem 0.45rem; border-radius: 999px; background: #f2f5f2; color: #728377; }
.head-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; min-height: 36px; border: 1px solid transparent; border-radius: 9px; padding: 0.48rem 0.75rem; font-size: 0.75rem; font-weight: 700; cursor: pointer; }
.btn-primary { background: #1d5a39; border-color: #1d5a39; color: #fff; }
.btn-primary:hover { background: #16482d; }
.btn-secondary { background: #fff; border-color: #d9e4da; color: #2f513c; }
.btn-secondary:hover { background: #f7faf7; }
.btn:disabled { opacity: 0.6; cursor: not-allowed; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }
.weather-hero { margin-bottom: 0.8rem; }
.live-panel { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(280px, 0.8fr); border: 1px solid #dce8e4; border-radius: 14px; background: radial-gradient(circle at 92% 10%, rgba(96, 150, 183, 0.13), transparent 30%), #fff; box-shadow: 0 4px 16px rgba(23, 57, 34, 0.045); overflow: hidden; }
.live-copy { padding: 1.25rem 1.35rem; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.live-copy h3 { margin: 0; color: #1a3a25; font-size: 1.15rem; letter-spacing: -0.02em; }
.live-subtitle { margin: 0.25rem 0 0; color: #7f8f83; font-size: 0.7rem; }
.selector-row { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 1rem; }
.selector-row select { flex: 1; min-width: 220px; border: 1px solid #d8e3da; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #31503c; font-size: 0.72rem; }
.live-error { margin: 0.65rem 0 0; color: #9b4141; font-size: 0.66rem; line-height: 1.45; }
.live-readout { display: flex; flex-direction: column; justify-content: center; padding: 1.2rem; border-left: 1px solid #e7efeb; background: rgba(246, 250, 250, 0.7); }
.live-icon, .empty-readout-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; background: #eaf3f8; color: #427a9e; }
.empty-readout-icon { background: #f2f5f2; color: #8a978c; }
.live-readout > strong { margin-top: 0.45rem; color: #1c3d28; font-size: 2rem; letter-spacing: -0.05em; line-height: 1; }
.live-readout > p { margin: 0.2rem 0 0; color: #738477; font-size: 0.68rem; }
.live-stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.35rem; margin-top: 0.7rem; width: 100%; }
.live-stats span { display: flex; flex-direction: column; padding: 0.4rem; border-radius: 7px; background: #fff; color: #8a978c; font-size: 0.55rem; }
.live-stats strong { margin-top: 0.1rem; color: #355640; font-size: 0.62rem; }
.weather-grid { display: grid; grid-template-columns: minmax(0, 1.6fr) minmax(300px, 0.8fr); gap: 0.8rem; align-items: start; }
.side-stack { display: flex; flex-direction: column; gap: 0.8rem; }
.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.count-badge { display: inline-flex; align-items: center; justify-content: center; min-width: 22px; height: 22px; border-radius: 999px; background: #eef5f8; color: #3e7298; font-size: 0.6rem; font-weight: 800; }
.history-table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.8rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.8rem; border-top: 1px solid #eef2ee; color: #4c6353; white-space: nowrap; }
.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.muted-cell { color: #89968c; }
.mini-chart { display: flex; align-items: flex-end; gap: 0.3rem; height: 150px; padding: 1rem; }
.mini-bar { display: flex; align-items: flex-end; height: 100%; flex: 1; border-radius: 4px; background: #f2f6f6; overflow: hidden; }
.mini-bar span { display: block; width: 100%; border-radius: 4px 4px 0 0; background: linear-gradient(180deg, #6ba4c3, #3c789d); }
.forecast-list { list-style: none; margin: 0; padding: 0.3rem 1rem 0.8rem; }
.forecast-list li { display: flex; justify-content: space-between; gap: 0.5rem; padding: 0.42rem 0; border-top: 1px solid #eef2ee; color: #7c8d80; font-size: 0.62rem; }
.forecast-list li:first-child { border-top: 0; }
.forecast-list strong { color: #355640; font-size: 0.62rem; text-align: right; }
.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 2.5rem 1.5rem; text-align: center; }
.empty-panel p { max-width: 380px; margin: 0; color: #849287; font-size: 0.7rem; line-height: 1.5; }
.empty-panel code { border-radius: 4px; background: #f1f5f1; padding: 0.05rem 0.2rem; color: #566e5d; font-size: 0.62rem; }
.empty-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; background: #eef5f8; color: #557d98; }
.compact-empty { padding: 1rem; color: #879589; font-size: 0.68rem; line-height: 1.5; }
.compact-empty p { margin: 0; }
.provider-banner { display: flex; align-items: flex-start; gap: 0.6rem; margin-top: 0.8rem; padding: 0.8rem 0.9rem; border: 1px solid #dfe9e3; border-radius: 10px; background: #f8fbf8; }
.provider-icon { display: grid; place-items: center; width: 33px; height: 33px; flex: 0 0 auto; border-radius: 9px; background: #eaf3f8; color: #427a9e; }
.provider-banner strong { color: #31543d; font-size: 0.7rem; }
.provider-banner p { margin: 0.2rem 0 0; color: #7d8d81; font-size: 0.63rem; line-height: 1.45; }

@media (max-width: 1050px) {
  .live-panel, .weather-grid { grid-template-columns: 1fr; }
  .live-readout { border-left: 0; border-top: 1px solid #e7efeb; }
  .side-stack { display: grid; grid-template-columns: 1fr 1fr; }
}

@media (max-width: 760px) {
  .page-intro { flex-direction: column; align-items: flex-start; }
  .side-stack { grid-template-columns: 1fr; }
  .live-stats { grid-template-columns: 1fr; }
}

@media (max-width: 540px) {
  .selector-row { flex-direction: column; }
}
</style>
