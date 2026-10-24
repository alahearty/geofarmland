<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { LandParcel } from '@/api/enterprise'
import { toLeafletCoordinates } from '@/utils/plots'
import { formatHectares, titleCase } from '@/utils/enterprise'

const props = withDefaults(defineProps<{
  parcels: LandParcel[]
  selectedId?: number | null
  height?: string
  emptyMessage?: string
}>(), {
  selectedId: null,
  height: '100%',
  emptyMessage: 'No land parcels are available in this workspace yet.'
})

const emit = defineEmits<{ select: [parcel: LandParcel] }>()

const mapEl = ref<HTMLDivElement | null>(null)
const mapReady = ref(false)
let map: L.Map | null = null
let layerGroup: L.LayerGroup | null = null
let resizeObserver: ResizeObserver | null = null
const layers = new Map<number, L.Polygon>()
let initialFitDone = false

const colors: Record<string, { line: string; fill: string }> = {
  Active: { line: '#1d6f48', fill: '#4a9d70' },
  Fallow: { line: '#a96509', fill: '#dca33a' },
  UnderDevelopment: { line: '#23658f', fill: '#4c91b7' },
  Archived: { line: '#68756c', fill: '#98a49b' }
}

function escapeHtml(value: string) {
  const entities: Record<string, string> = {
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;'
  }
  return value.replace(/[&<>"']/g, (character) => entities[character])
}

function styleFor(parcel: LandParcel, active: boolean): L.PolylineOptions {
  const palette = colors[parcel.status] ?? { line: '#446854', fill: '#7ba58b' }
  return active
    ? { color: '#d98515', weight: 3.5, fillColor: '#f0b354', fillOpacity: 0.5 }
    : { color: palette.line, weight: 2, fillColor: palette.fill, fillOpacity: 0.38 }
}

function renderLayers() {
  if (!map || !layerGroup) return
  layerGroup.clearLayers()
  layers.clear()

  props.parcels.forEach((parcel) => {
    if (!Array.isArray(parcel.coordinates) || parcel.coordinates.length < 3) return
    const active = props.selectedId === parcel.id
    const polygon = L.polygon(toLeafletCoordinates(parcel.coordinates), styleFor(parcel, active))
    polygon.bindPopup(
      `<strong>${escapeHtml(parcel.name)}</strong><br>${escapeHtml(parcel.farmName)}<br>${formatHectares(parcel.areaHectares)} · ${escapeHtml(titleCase(parcel.landUse))}`
    )
    polygon.on('click', () => emit('select', parcel))
    polygon.addTo(layerGroup!)
    layers.set(parcel.id, polygon)
  })

  if (!initialFitDone && layers.size > 0) {
    fitAll()
    initialFitDone = true
  }
}

function fitAll() {
  if (!map || layers.size === 0) return
  const group = L.featureGroup(Array.from(layers.values()))
  map.fitBounds(group.getBounds().pad(0.18))
}

function focusParcel(id: number) {
  const layer = layers.get(id)
  if (!map || !layer) return
  map.fitBounds(layer.getBounds().pad(0.35))
  layer.openPopup()
}

function onResize() {
  map?.invalidateSize()
}

onMounted(async () => {
  await nextTick()
  if (!mapEl.value) return
  map = L.map(mapEl.value, { zoomControl: true, attributionControl: true })
    .setView([-33.9249, 18.4241], 10)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap contributors'
  }).addTo(map)
  L.control.scale({ imperial: false, position: 'bottomleft' }).addTo(map)
  layerGroup = L.layerGroup().addTo(map)
  mapReady.value = true
  window.addEventListener('resize', onResize)
  if (typeof ResizeObserver !== 'undefined') {
    resizeObserver = new ResizeObserver(() => map?.invalidateSize())
    resizeObserver.observe(mapEl.value)
  }
  renderLayers()
  if (props.parcels.length === 0) initialFitDone = false
})

watch(() => props.parcels, () => {
  renderLayers()
}, { deep: true })

watch(() => props.selectedId, () => {
  if (!map || !layerGroup) return
  props.parcels.forEach((parcel) => {
    layers.get(parcel.id)?.setStyle(styleFor(parcel, props.selectedId === parcel.id))
  })
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  resizeObserver?.disconnect()
  map?.remove()
  map = null
  layerGroup = null
})

defineExpose({ focusParcel, fitAll })
</script>

<template>
  <div class="parcel-map">
    <div ref="mapEl" class="map-canvas" :style="{ height }"></div>
    <div v-if="mapReady && parcels.length === 0" class="map-empty">
      <span class="empty-icon">◎</span>
      <p>{{ emptyMessage }}</p>
      <slot name="empty" />
    </div>
  </div>
</template>

<style scoped>
.parcel-map {
  position: relative;
  min-height: 280px;
  height: 100%;
  overflow: hidden;
  border-radius: inherit;
  background: #e8ede7;
}

.map-canvas {
  width: 100%;
  min-height: 280px;
  z-index: 1;
}

.map-empty {
  position: absolute;
  inset: 0;
  z-index: 450;
  display: grid;
  place-content: center;
  justify-items: center;
  gap: 0.35rem;
  padding: 2rem;
  text-align: center;
  background: rgba(244, 248, 243, 0.82);
  color: #55705f;
}

.empty-icon {
  display: grid;
  place-items: center;
  width: 48px;
  height: 48px;
  border-radius: 16px;
  background: #e1eee4;
  color: #2d7b52;
  font-size: 1.4rem;
}

.map-empty p {
  max-width: 320px;
  margin: 0;
  font-size: 0.88rem;
  line-height: 1.5;
}

:deep(.leaflet-control-attribution) {
  font-size: 9px;
}
</style>
