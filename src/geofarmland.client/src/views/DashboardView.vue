<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import * as signalR from '@microsoft/signalr'
import {
  monitoringApi,
  plotsApi,
  type ForecastItem,
  type LiveWeather,
  type Plot,
  type SensorReading,
  type WeatherHistoryItem
} from '@/api/plots'
import { devicesApi, type Device } from '@/api/devices'
import {
  openRing,
  plotCenterFromCoordinates,
  polygonAreaHectares,
  toGeoJsonCoordinates,
  toLeafletCoordinates
} from '@/utils/plots'

const mapEl = ref<HTMLDivElement | null>(null)
const plots = ref<Plot[]>([])
const selected = ref<Plot | null>(null)
const loadingPlots = ref(false)
const creating = ref(false)
const editing = ref(false)
const saving = ref(false)
const newPlotName = ref('')
const editName = ref('')
const draftPoints = ref<[number, number][]>([])
const editPoints = ref<[number, number][]>([])
const message = ref('')
const error = ref('')

const devices = ref<Device[]>([])
const selectedDeviceId = ref('')
const showDeviceForm = ref(false)
const newDeviceName = ref('')
const newDeviceId = ref('')
const newDevicePlotId = ref<number | ''>('')
const deviceSaving = ref(false)
const deviceError = ref('')

const sensors = ref<SensorReading[]>([])
const sensorsError = ref('')
const sendingReading = ref(false)
const readingMoisture = ref(24)
const readingRainfall = ref(0)

const weather = ref<LiveWeather | null>(null)
const weatherError = ref('')
const weatherLoading = ref(false)
const weatherHistory = ref<WeatherHistoryItem[]>([])
const weatherHistoryError = ref('')
const forecast = ref<ForecastItem[]>([])
const forecastError = ref('')

const signalrState = ref<'connecting' | 'connected' | 'reconnecting' | 'offline'>('connecting')

let map: L.Map | null = null
let layerGroup: L.LayerGroup | null = null
let draftLayer: L.LayerGroup | null = null
let draftPolygon: L.Polygon | null = null
let editLayer: L.LayerGroup | null = null
let editPolygon: L.Polygon | null = null
let signalrConnection: signalR.HubConnection | null = null
const plotLayers = new Map<number, L.Polygon>()
let hasFitted = false

const selectedDevice = computed(() =>
  devices.value.find((device) => device.deviceId === selectedDeviceId.value) ?? null
)

const signalrLabel = computed(() => {
  switch (signalrState.value) {
    case 'connected': return 'Live updates connected'
    case 'reconnecting': return 'Reconnecting to live updates'
    case 'offline': return 'Live updates offline'
    default: return 'Connecting to live updates'
  }
})

function errorMessage(err: unknown): string {
  const candidate = err as {
    response?: { status?: number; data?: { message?: string; condition?: string; detail?: string } }
    message?: string
  }
  if (candidate.response?.status === 503 && candidate.response.data?.condition) {
    return `${candidate.response.data.condition}. Add an OpenWeatherMap API key to enable live weather.`
  }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.response?.data?.condition
    || candidate.message
    || 'The request could not be completed.'
}

function formatArea(coordinates: number[][]) {
  return polygonAreaHectares(coordinates).toFixed(2)
}

function devicePlotName(device: Device) {
  return device.plotName || plots.value.find((plot) => plot.id === device.plotId)?.name || 'No plot linked'
}

function formatForecastTime(value: string) {
  return new Date(value).toLocaleString(undefined, { weekday: 'short', hour: '2-digit', minute: '2-digit' })
}

function formatHistoryTime(value: string) {
  return new Date(value).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

async function loadPlots() {
  loadingPlots.value = true
  error.value = ''
  try {
    const { data } = await plotsApi.list()
    plots.value = (Array.isArray(data) ? data : []).filter((plot) =>
      Array.isArray(plot.coordinates) && plot.coordinates.length >= 3
    )
    renderPlots()
    if (selected.value) {
      selected.value = plots.value.find((plot) => plot.id === selected.value?.id) ?? null
    }
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loadingPlots.value = false
  }
}

function renderPlots() {
  if (!map || !layerGroup) return
  layerGroup.clearLayers()
  plotLayers.clear()

  plots.value.forEach((plot) => {
    const polygon = L.polygon(toLeafletCoordinates(plot.coordinates), {
      color: selected.value?.id === plot.id ? '#b45309' : '#1a472a',
      weight: 2,
      fillColor: selected.value?.id === plot.id ? '#f59e0b' : '#2e7d5b',
      fillOpacity: 0.35
    })
    polygon.bindPopup(`<strong>${escapeHtml(plot.name)}</strong><br>${formatArea(plot.coordinates)} ha`)
    polygon.on('click', () => selectPlot(plot, false))
    polygon.addTo(layerGroup!)
    plotLayers.set(plot.id, polygon)
  })

  if (!hasFitted && plotLayers.size > 0) {
    fitAllPlots()
    hasFitted = true
  }
}

function escapeHtml(value: string) {
  const entities: Record<string, string> = {
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;'
  }
  return value.replace(/[&<>"']/g, (character) => entities[character])
}

function fitAllPlots() {
  if (!map || plotLayers.size === 0) return
  const group = L.featureGroup(Array.from(plotLayers.values()))
  map.fitBounds(group.getBounds().pad(0.15))
}

function selectPlot(plot: Plot, zoom = true) {
  if (creating.value || editing.value) return
  selected.value = plot
  weather.value = null
  weatherError.value = ''
  weatherHistory.value = []
  forecast.value = []
  forecastError.value = ''
  plotLayers.forEach((layer, id) => {
    const active = id === plot.id
    layer.setStyle({
      color: active ? '#b45309' : '#1a472a',
      fillColor: active ? '#f59e0b' : '#2e7d5b'
    })
  })
  const layer = plotLayers.get(plot.id)
  if (zoom && map && layer) map.fitBounds(layer.getBounds().pad(0.25))
  void loadWeatherHistory(plot.id)
}

function startCreate() {
  if (!map) return
  if (editing.value) cancelEdit()
  creating.value = true
  saving.value = false
  newPlotName.value = ''
  draftPoints.value = []
  error.value = ''
  message.value = 'Click the map to add the corners of the plot.'
  draftLayer?.clearLayers()
  draftPolygon = null
  map.getContainer().style.cursor = 'crosshair'
  nextTick(() => map?.invalidateSize())
}

function cancelCreate() {
  creating.value = false
  draftPoints.value = []
  draftLayer?.clearLayers()
  draftPolygon = null
  message.value = ''
  if (map) map.getContainer().style.cursor = ''
}

function startEdit() {
  if (!selected.value) return
  creating.value = false
  editing.value = true
  saving.value = false
  editName.value = selected.value.name
  editPoints.value = openRing(selected.value.coordinates).map(([lng, lat]) => [lat, lng])
  error.value = ''
  message.value = 'Drag a vertex to reshape, click the map to add a point, or right-click a vertex to remove it.'
  renderEditLayer()
  nextTick(() => map?.invalidateSize())
}

function cancelEdit() {
  editing.value = false
  editPoints.value = []
  editLayer?.clearLayers()
  editPolygon = null
  message.value = ''
}

function renderEditLayer() {
  if (!editLayer) return
  editLayer.clearLayers()
  editPolygon = null

  if (editPoints.value.length >= 3) {
    editPolygon = L.polygon(editPoints.value, {
      color: '#d97706',
      weight: 3,
      dashArray: '6 4',
      fillColor: '#f59e0b',
      fillOpacity: 0.18
    }).addTo(editLayer)
  }

  editPoints.value.forEach((point, index) => {
    const marker = L.marker(point, {
      draggable: true,
      bubblingMouseEvents: false,
      icon: L.divIcon({
        className: 'vertex-handle',
        html: '<span></span>',
        iconSize: [16, 16],
        iconAnchor: [8, 8]
      })
    }).addTo(editLayer!)

    marker.on('dragend', () => {
      const next = marker.getLatLng()
      editPoints.value[index] = [next.lat, next.lng]
      renderEditLayer()
    })
    marker.on('click', (event) => L.DomEvent.stopPropagation(event.originalEvent))
    marker.on('contextmenu', (event) => {
      L.DomEvent.stopPropagation(event.originalEvent)
      removeEditPoint(index)
    })
  })
}

function removeEditPoint(index: number) {
  if (editPoints.value.length <= 3) {
    error.value = 'A plot must keep at least three vertices.'
    return
  }
  editPoints.value.splice(index, 1)
  error.value = ''
  renderEditLayer()
}

function undoEditPoint() {
  if (editPoints.value.length <= 3) {
    error.value = 'A plot must keep at least three vertices.'
    return
  }
  editPoints.value.pop()
  error.value = ''
  renderEditLayer()
}

function onMapClick(event: L.LeafletMouseEvent) {
  if (creating.value && draftLayer) {
    draftPoints.value.push([event.latlng.lat, event.latlng.lng])
    L.circleMarker(event.latlng, {
      radius: 5,
      color: '#1a472a',
      fillColor: '#ffffff',
      fillOpacity: 1,
      weight: 2
    }).addTo(draftLayer)
    updateDraftPolygon()
  } else if (editing.value) {
    editPoints.value.push([event.latlng.lat, event.latlng.lng])
    renderEditLayer()
  }
}

function updateDraftPolygon() {
  if (!draftLayer) return
  if (draftPolygon) {
    draftLayer.removeLayer(draftPolygon)
    draftPolygon = null
  }
  if (draftPoints.value.length >= 3) {
    draftPolygon = L.polygon(draftPoints.value, {
      color: '#d97706',
      dashArray: '6 4',
      fillOpacity: 0.15
    }).addTo(draftLayer)
  }
}

async function savePlot() {
  if (newPlotName.value.trim().length < 2) {
    error.value = 'Enter a plot name (at least 2 characters).'
    return
  }
  if (draftPoints.value.length < 3) {
    error.value = 'Add at least three points on the map.'
    return
  }

  saving.value = true
  error.value = ''
  try {
    const savedName = newPlotName.value.trim()
    const response = await plotsApi.create(savedName, toGeoJsonCoordinates(draftPoints.value))
    const createdId = (response.data as { plotId?: number }).plotId
    creating.value = false
    draftPoints.value = []
    draftLayer?.clearLayers()
    draftPolygon = null
    if (map) map.getContainer().style.cursor = ''
    message.value = `Plot "${savedName}" saved.`
    await loadPlots()
    const created = plots.value.find((plot) => plot.id === createdId)
    if (created) selectPlot(created)
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function saveEdit() {
  if (!selected.value || !editing.value) return
  if (editName.value.trim().length < 2) {
    error.value = 'Enter a plot name (at least 2 characters).'
    return
  }
  if (editPoints.value.length < 3) {
    error.value = 'A plot must have at least three vertices.'
    return
  }

  saving.value = true
  error.value = ''
  try {
    const savedName = editName.value.trim()
    await plotsApi.update(selected.value.id, savedName, toGeoJsonCoordinates(editPoints.value))
    editing.value = false
    editLayer?.clearLayers()
    editPolygon = null
    message.value = `Plot "${savedName}" updated.`
    await loadPlots()
    const updated = plots.value.find((plot) => plot.id === selected.value?.id)
    if (updated) selectPlot(updated, false)
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function deleteSelected() {
  if (!selected.value) return
  if (!window.confirm(`Delete the plot "${selected.value.name}"? This cannot be undone.`)) return
  try {
    await plotsApi.remove(selected.value.id)
    message.value = 'Plot deleted.'
    selected.value = null
    weather.value = null
    await Promise.all([loadPlots(), loadDevices()])
  } catch (err) {
    error.value = errorMessage(err)
  }
}

async function loadDevices() {
  deviceError.value = ''
  try {
    const { data } = await devicesApi.list()
    devices.value = Array.isArray(data) ? data : []
    if (!devices.value.some((device) => device.deviceId === selectedDeviceId.value)) {
      selectedDeviceId.value = devices.value[0]?.deviceId ?? ''
    }
  } catch (err) {
    deviceError.value = errorMessage(err)
  }
}

function openDeviceForm() {
  showDeviceForm.value = true
  deviceError.value = ''
  newDeviceName.value = ''
  newDeviceId.value = ''
  newDevicePlotId.value = selected.value?.id ?? ''
}

async function saveDevice() {
  const name = newDeviceName.value.trim()
  const deviceId = newDeviceId.value.trim()
  if (name.length < 2 || deviceId.length < 1) {
    deviceError.value = 'Enter a device name and a device ID.'
    return
  }

  deviceSaving.value = true
  deviceError.value = ''
  try {
    const { data } = await devicesApi.create({
      name,
      deviceId,
      plotId: newDevicePlotId.value === '' ? null : Number(newDevicePlotId.value)
    })
    showDeviceForm.value = false
    message.value = `Device "${data.name}" registered.`
    await loadDevices()
    selectedDeviceId.value = data.deviceId
  } catch (err) {
    deviceError.value = errorMessage(err)
  } finally {
    deviceSaving.value = false
  }
}

async function deleteDevice(device: Device) {
  if (!window.confirm(`Remove device "${device.name}" from your account?`)) return
  try {
    await devicesApi.remove(device.id)
    message.value = 'Device removed.'
    await loadDevices()
  } catch (err) {
    deviceError.value = errorMessage(err)
  }
}

async function loadSensors() {
  sensorsError.value = ''
  try {
    const { data } = await monitoringApi.sensors()
    sensors.value = Array.isArray(data) ? data : []
  } catch (err) {
    sensorsError.value = errorMessage(err)
  }
}

async function sendReading() {
  if (!map) return
  if (!selectedDevice.value) {
    sensorsError.value = 'Register or select a device before sending a reading.'
    return
  }

  const linkedPlot = selectedDevice.value.plotId
    ? plots.value.find((plot) => plot.id === selectedDevice.value?.plotId)
    : null
  const center = linkedPlot
    ? plotCenterFromCoordinates(linkedPlot.coordinates)
    : selected.value
      ? plotCenterFromCoordinates(selected.value.coordinates)
      : [map.getCenter().lat, map.getCenter().lng]

  sendingReading.value = true
  sensorsError.value = ''
  try {
    await monitoringApi.ingest({
      deviceId: selectedDevice.value.deviceId,
      latitude: center[0],
      longitude: center[1],
      rainfall: Number(readingRainfall.value) || 0,
      soilMoisture: Number(readingMoisture.value) || 0
    })
    message.value = 'Synthetic sensor reading submitted.'
    await loadSensors()
  } catch (err) {
    sensorsError.value = errorMessage(err)
  } finally {
    sendingReading.value = false
  }
}

async function loadWeatherHistory(plotId?: number) {
  weatherHistoryError.value = ''
  try {
    const { data } = await monitoringApi.weatherHistory(plotId)
    weatherHistory.value = Array.isArray(data) ? data : []
  } catch (err) {
    weatherHistoryError.value = errorMessage(err)
  }
}

async function loadWeather() {
  if (!selected.value) return
  const center = plotCenterFromCoordinates(selected.value.coordinates)
  weatherLoading.value = true
  weatherError.value = ''
  weather.value = null
  forecast.value = []
  forecastError.value = ''
  try {
    const { data } = await monitoringApi.weather(center[0], center[1], selected.value.id)
    weather.value = data
    await loadWeatherHistory(selected.value.id)
    try {
      const forecastResponse = await monitoringApi.forecast(center[0], center[1])
      forecast.value = Array.isArray(forecastResponse.data) ? forecastResponse.data : []
    } catch (err) {
      forecastError.value = errorMessage(err)
    }
  } catch (err) {
    weatherError.value = errorMessage(err)
  } finally {
    weatherLoading.value = false
  }
}

async function connectRealtime() {
  if (signalrConnection) return
  signalrState.value = 'connecting'
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/signalr/hub', {
      accessTokenFactory: () => localStorage.getItem('accessToken') || ''
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  connection.on('ReceiveSensorData', (reading: SensorReading) => {
    if (!reading || sensors.value.some((item) => item.id === reading.id)) return
    sensors.value = [reading, ...sensors.value]
  })
  connection.on('ReceiveWeatherData', (data: LiveWeather) => {
    weather.value = data
  })
  connection.onreconnecting(() => { signalrState.value = 'reconnecting' })
  connection.onreconnected(async () => {
    signalrState.value = 'connected'
    await loadSensors()
  })
  connection.onclose(() => { signalrState.value = 'offline' })

  signalrConnection = connection
  try {
    await connection.start()
    signalrState.value = 'connected'
  } catch {
    signalrState.value = 'offline'
  }
}

function onResize() {
  map?.invalidateSize()
}

onMounted(async () => {
  await nextTick()
  if (!mapEl.value) return

  map = L.map(mapEl.value, { zoomControl: true }).setView([-33.9249, 18.4241], 10)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap contributors'
  }).addTo(map)
  layerGroup = L.layerGroup().addTo(map)
  draftLayer = L.layerGroup().addTo(map)
  editLayer = L.layerGroup().addTo(map)
  map.on('click', onMapClick)
  window.addEventListener('resize', onResize)

  await Promise.all([loadPlots(), loadDevices(), loadSensors()])
  await connectRealtime()
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  if (signalrConnection) {
    signalrConnection.stop()
    signalrConnection = null
  }
  map?.remove()
  map = null
})
</script>

<template>
  <div class="dashboard">
    <div v-if="message" class="notice notice-success">{{ message }}</div>
    <div v-if="error" class="notice notice-error">{{ error }}</div>

    <div class="workspace">
      <aside class="sidebar">
        <section class="panel">
          <div class="panel-head">
            <div>
              <h2>Farmland plots</h2>
              <p class="muted">{{ plots.length }} recorded</p>
            </div>
            <button v-if="!creating && !editing" class="btn-primary" @click="startCreate">+ Draw plot</button>
          </div>

          <p v-if="loadingPlots" class="state">Loading plots...</p>
          <p v-else-if="plots.length === 0" class="state">
            No plots yet. Use <strong>Draw plot</strong> and click at least three corners on the map.
          </p>
          <ul v-else class="plot-list">
            <li
              v-for="plot in plots"
              :key="plot.id"
              :class="['plot-item', { active: selected?.id === plot.id }]"
              @click="selectPlot(plot)"
            >
              <span class="plot-name">{{ plot.name }}</span>
              <span class="plot-area">{{ formatArea(plot.coordinates) }} ha</span>
            </li>
          </ul>
        </section>

        <section v-if="creating" class="panel">
          <h2>Draw a new plot</h2>
          <label class="field">
            <span>Plot name</span>
            <input v-model="newPlotName" maxlength="120" placeholder="e.g. North field" />
          </label>
          <p class="hint">Click the map to add corners. Points added: {{ draftPoints.length }}.</p>
          <div class="actions">
            <button class="btn-primary" :disabled="saving" @click="savePlot">
              {{ saving ? 'Saving...' : 'Save plot' }}
            </button>
            <button class="btn-secondary" @click="cancelCreate">Cancel</button>
          </div>
        </section>

        <section v-if="editing" class="panel">
          <h2>Edit plot shape</h2>
          <label class="field">
            <span>Plot name</span>
            <input v-model="editName" maxlength="120" />
          </label>
          <p class="hint">
            Drag a vertex to reshape. Click the map to add a point; right-click a vertex to remove it.
            {{ editPoints.length }} vertices.
          </p>
          <div class="actions">
            <button class="btn-primary" :disabled="saving" @click="saveEdit">
              {{ saving ? 'Saving...' : 'Save changes' }}
            </button>
            <button class="btn-secondary" @click="undoEditPoint">Undo point</button>
            <button class="btn-secondary" @click="cancelEdit">Cancel</button>
          </div>
        </section>

        <section v-if="selected && !creating && !editing" class="panel">
          <div class="panel-head">
            <div>
              <h2>{{ selected.name }}</h2>
              <p class="muted">{{ formatArea(selected.coordinates) }} hectares</p>
            </div>
            <div class="actions compact">
              <button class="btn-secondary" @click="startEdit">Edit shape</button>
              <button class="btn-danger" @click="deleteSelected">Delete</button>
            </div>
          </div>
          <dl class="details">
            <div><dt>Plot ID</dt><dd>{{ selected.id }}</dd></div>
            <div><dt>Vertices</dt><dd>{{ openRing(selected.coordinates).length }}</dd></div>
            <div><dt>Centre</dt><dd>{{ plotCenterFromCoordinates(selected.coordinates)[0].toFixed(4) }}, {{ plotCenterFromCoordinates(selected.coordinates)[1].toFixed(4) }}</dd></div>
          </dl>

          <button class="btn-secondary wide" :disabled="weatherLoading" @click="loadWeather">
            {{ weatherLoading ? 'Checking weather...' : 'Check live weather at plot' }}
          </button>

          <div v-if="weather" class="weather-grid">
            <div><span>Temperature</span><strong>{{ weather.temperature }}</strong></div>
            <div><span>Feels like</span><strong>{{ weather.feelsLike }}</strong></div>
            <div><span>Humidity</span><strong>{{ weather.humidity }}</strong></div>
            <div><span>Pressure</span><strong>{{ weather.pressure }}</strong></div>
            <div><span>Wind</span><strong>{{ weather.windSpeed }}</strong></div>
            <div><span>Conditions</span><strong>{{ weather.condition }}</strong></div>
            <div><span>Description</span><strong>{{ weather.description }}</strong></div>
            <div><span>Location</span><strong>{{ weather.location }}</strong></div>
          </div>
          <p v-else-if="weatherError" class="state state-error">{{ weatherError }}</p>

          <div v-if="forecast.length" class="sub-panel">
            <h3>Next forecast</h3>
            <ul class="forecast-list">
              <li v-for="item in forecast" :key="item.timeUtc">
                <span>{{ formatForecastTime(item.timeUtc) }}</span>
                <strong>{{ item.temperatureC.toFixed(1) }} °C · {{ item.description }}</strong>
              </li>
            </ul>
          </div>
          <p v-else-if="forecastError" class="state state-error">{{ forecastError }}</p>

          <div v-if="weatherHistory.length" class="sub-panel">
            <h3>Weather history</h3>
            <ul class="history-list">
              <li v-for="item in weatherHistory.slice(0, 5)" :key="item.id">
                <span>{{ formatHistoryTime(item.fetchedAtUtc) }}</span>
                <strong>{{ item.temperatureC.toFixed(1) }} °C · {{ item.description }}</strong>
              </li>
            </ul>
          </div>
          <p v-else-if="weatherHistoryError" class="state state-error">{{ weatherHistoryError }}</p>
        </section>

        <section class="panel">
          <div class="panel-head">
            <div>
              <h2>Devices</h2>
              <p class="muted">{{ devices.length }} registered</p>
            </div>
            <button v-if="!showDeviceForm" class="btn-secondary" @click="openDeviceForm">+ Register</button>
          </div>

          <p v-if="deviceError" class="state state-error">{{ deviceError }}</p>
          <div v-if="devices.length" class="device-list">
            <label v-for="device in devices" :key="device.id" class="device-item" :class="{ active: selectedDeviceId === device.deviceId }">
              <input v-model="selectedDeviceId" type="radio" :value="device.deviceId" />
              <span class="device-copy">
                <strong>{{ device.name }}</strong>
                <small>{{ device.deviceId }} · {{ devicePlotName(device) }}</small>
              </span>
              <button type="button" class="device-remove" @click.stop="deleteDevice(device)">Remove</button>
            </label>
          </div>
          <p v-else class="state">Register a field sensor to start posting readings.</p>

          <form v-if="showDeviceForm" class="device-form" @submit.prevent="saveDevice">
            <label class="field">
              <span>Device name</span>
              <input v-model="newDeviceName" maxlength="120" placeholder="e.g. North field sensor" />
            </label>
            <label class="field">
              <span>Device ID</span>
              <input v-model="newDeviceId" maxlength="80" placeholder="e.g. field-sensor-03" />
            </label>
            <label class="field">
              <span>Link to plot</span>
              <select v-model="newDevicePlotId">
                <option value="">No plot</option>
                <option v-for="plot in plots" :key="plot.id" :value="plot.id">{{ plot.name }}</option>
              </select>
            </label>
            <div class="actions">
              <button class="btn-primary" type="submit" :disabled="deviceSaving">
                {{ deviceSaving ? 'Registering...' : 'Register device' }}
              </button>
              <button class="btn-secondary" type="button" @click="showDeviceForm = false">Cancel</button>
            </div>
          </form>
        </section>

        <section class="panel">
          <div class="panel-head">
            <div>
              <h2>IoT sensor monitor</h2>
              <p class="hint">Synthetic readings from registered devices; no hardware is connected by default.</p>
            </div>
            <span class="signal-status" :class="`signal-${signalrState}`">{{ signalrLabel }}</span>
          </div>

          <form class="sensor-form" @submit.prevent="sendReading">
            <label class="field">
              <span>Device</span>
              <select v-model="selectedDeviceId" :disabled="devices.length === 0">
                <option value="" disabled>Select a device</option>
                <option v-for="device in devices" :key="device.id" :value="device.deviceId">{{ device.name }} ({{ device.deviceId }})</option>
              </select>
            </label>
            <div class="field-row">
              <label class="field">
                <span>Soil moisture (%)</span>
                <input v-model.number="readingMoisture" type="number" min="0" max="100" step="0.1" />
              </label>
              <label class="field">
                <span>Rainfall (mm)</span>
                <input v-model.number="readingRainfall" type="number" min="0" step="0.1" />
              </label>
            </div>
            <button class="btn-secondary wide" :disabled="sendingReading || !selectedDevice" type="submit">
              {{ sendingReading ? 'Sending...' : 'Send synthetic reading' }}
            </button>
          </form>

          <p v-if="sensorsError" class="state state-error">{{ sensorsError }}</p>
          <p v-else-if="sensors.length === 0" class="state">No readings stored yet.</p>
          <ul v-else class="sensor-list">
            <li v-for="reading in sensors" :key="reading.id">
              <div>
                <strong>{{ reading.deviceId }}</strong>
                <span class="muted">{{ new Date(reading.timestamp).toLocaleString() }}</span>
              </div>
              <span class="sensor-values">
                {{ reading.soilMoisture.toFixed(1) }}% moisture
                · {{ reading.rainfall.toFixed(1) }} mm rain
              </span>
            </li>
          </ul>
        </section>
      </aside>

      <section class="map-wrap">
        <div ref="mapEl" class="map"></div>
        <div v-if="creating" class="map-hint">
          Drawing mode: click the map to add plot corners, then save from the sidebar.
        </div>
        <div v-else-if="editing" class="map-hint">
          Edit mode: drag vertices, click the map to add one, right-click a vertex to remove it.
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.dashboard {
  color: #183024;
}

.notice {
  border-radius: 8px;
  padding: 0.8rem 1rem;
  font-size: 0.9rem;
  margin-bottom: 1rem;
}

.notice-success {
  background: #e7f6ec;
  color: #1a6b3c;
  border: 1px solid #b7e2c6;
}

.notice-error {
  background: #fdecec;
  color: #9b1c1c;
  border: 1px solid #f5c2c2;
}

.workspace {
  display: grid;
  grid-template-columns: 390px minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-width: 0;
}

.panel {
  background: white;
  border: 1px solid #e2e8e2;
  border-radius: 10px;
  padding: 1rem;
  box-shadow: 0 2px 8px rgba(26, 71, 42, 0.06);
}

.panel h2 {
  font-size: 1.05rem;
  margin: 0;
}

.panel h3 {
  font-size: 0.9rem;
  margin: 0 0 0.5rem;
}

.panel-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.muted {
  color: #6b7c70;
  font-size: 0.8rem;
  margin: 0.15rem 0 0;
}

.state {
  color: #6b7c70;
  font-size: 0.88rem;
  margin: 0.75rem 0 0;
}

.state-error {
  color: #9b1c1c;
}

.hint {
  color: #6b7c70;
  font-size: 0.82rem;
  margin: 0.5rem 0 0.75rem;
  line-height: 1.45;
}

.plot-list {
  list-style: none;
  margin: 0;
  padding: 0;
  max-height: 240px;
  overflow-y: auto;
}

.plot-item {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0.65rem 0.5rem;
  border-radius: 7px;
  cursor: pointer;
  border-bottom: 1px solid #eef2ee;
}

.plot-item:hover,
.plot-item.active {
  background: #edf6ef;
}

.plot-name {
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.plot-area {
  color: #5a7160;
  font-size: 0.8rem;
  white-space: nowrap;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  margin-bottom: 0.7rem;
  font-size: 0.82rem;
  font-weight: 600;
}

.field input,
.field select {
  border: 1px solid #cdd8cf;
  border-radius: 7px;
  padding: 0.5rem 0.6rem;
  font-size: 0.9rem;
  font-weight: 400;
  background: white;
}

.field-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.6rem;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.actions.compact {
  justify-content: flex-end;
}

.btn-primary,
.btn-secondary,
.btn-danger {
  border-radius: 7px;
  padding: 0.5rem 0.8rem;
  font-weight: 600;
  cursor: pointer;
  border: 1px solid transparent;
}

.btn-primary {
  background: #1a472a;
  color: white;
}

.btn-secondary {
  background: white;
  color: #1a472a;
  border-color: #b9cbbd;
}

.btn-danger {
  background: white;
  color: #a12b2b;
  border-color: #e1b7b7;
}

.wide {
  width: 100%;
  margin-top: 0.25rem;
}

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.details {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
  margin: 0.75rem 0;
}

.details div {
  background: #f6f8f6;
  border-radius: 7px;
  padding: 0.5rem;
}

.details dt {
  font-size: 0.72rem;
  text-transform: uppercase;
  color: #6b7c70;
  letter-spacing: 0.03em;
}

.details dd {
  margin: 0.15rem 0 0;
  font-size: 0.85rem;
  font-weight: 600;
}

.weather-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.weather-grid div {
  background: #eef6f0;
  border-radius: 7px;
  padding: 0.5rem;
}

.weather-grid span {
  display: block;
  font-size: 0.72rem;
  color: #5e7464;
}

.weather-grid strong {
  font-size: 0.85rem;
}

.sub-panel {
  margin-top: 1rem;
  border-top: 1px solid #eef2ee;
  padding-top: 0.8rem;
}

.forecast-list,
.history-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.forecast-list li,
.history-list li {
  display: flex;
  justify-content: space-between;
  gap: 0.6rem;
  padding: 0.35rem 0;
  font-size: 0.78rem;
  color: #6b7c70;
}

.forecast-list strong,
.history-list strong {
  color: #274332;
  text-align: right;
}

.device-list {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.device-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  border: 1px solid #eef2ee;
  border-radius: 8px;
  padding: 0.5rem;
  cursor: pointer;
}

.device-item.active {
  border-color: #a7cdb4;
  background: #f0f8f2;
}

.device-copy {
  min-width: 0;
  flex: 1;
}

.device-copy strong,
.device-copy small {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.device-copy strong {
  font-size: 0.82rem;
}

.device-copy small {
  margin-top: 0.12rem;
  color: #6b7c70;
  font-size: 0.72rem;
}

.device-remove {
  border: 0;
  background: transparent;
  color: #a12b2b;
  font-size: 0.72rem;
  font-weight: 600;
  cursor: pointer;
}

.device-form {
  margin-top: 0.8rem;
  border-top: 1px solid #eef2ee;
  padding-top: 0.8rem;
}

.sensor-form {
  margin-top: 0.5rem;
}

.sensor-list {
  list-style: none;
  margin: 0.7rem 0 0;
  padding: 0;
  max-height: 220px;
  overflow-y: auto;
}

.sensor-list li {
  display: flex;
  justify-content: space-between;
  gap: 0.6rem;
  padding: 0.55rem 0;
  border-top: 1px solid #eef2ee;
  font-size: 0.82rem;
}

.sensor-list .muted {
  display: block;
}

.sensor-values {
  color: #43604b;
  font-weight: 600;
  text-align: right;
  white-space: nowrap;
}

.signal-status {
  border-radius: 999px;
  padding: 0.18rem 0.55rem;
  font-size: 0.68rem;
  font-weight: 700;
  white-space: nowrap;
  border: 1px solid transparent;
}

.signal-connected {
  background: #e7f6ec;
  color: #1a6b3c;
  border-color: #b7e2c6;
}

.signal-connecting,
.signal-reconnecting {
  background: #fff7e6;
  color: #8a5a00;
  border-color: #f0d79a;
}

.signal-offline {
  background: #fdecec;
  color: #9b1c1c;
  border-color: #f5c2c2;
}

.map-wrap {
  min-height: 820px;
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid #dbe4dc;
  position: relative;
}

.map {
  width: 100%;
  height: 100%;
  min-height: 820px;
}

.map-hint {
  position: absolute;
  left: 1rem;
  bottom: 1rem;
  background: rgba(26, 71, 42, 0.92);
  color: white;
  padding: 0.55rem 0.8rem;
  border-radius: 6px;
  font-size: 0.82rem;
  max-width: 420px;
  z-index: 500;
}

:deep(.vertex-handle) {
  background: transparent;
  border: 0;
}

:deep(.vertex-handle span) {
  display: block;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: #fff7ed;
  border: 3px solid #b45309;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
}

@media (max-width: 1000px) {
  .workspace {
    grid-template-columns: 1fr;
  }

  .map-wrap,
  .map {
    min-height: 520px;
  }

  .sidebar {
    order: 2;
  }
}
</style>

