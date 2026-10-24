<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { openRing } from '@/utils/plots'

const props = withDefaults(defineProps<{ modelValue: number[][]; height?: string }>(), {
  height: '340px'
})

const emit = defineEmits<{ 'update:modelValue': [value: number[][]] }>()

const mapEl = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null
let drawLayer: L.LayerGroup | null = null
let resizeObserver: ResizeObserver | null = null

function render() {
  if (!map || !drawLayer) return
  drawLayer.clearLayers()

  const points = openRing(props.modelValue).map(([lng, lat]) => [lat, lng] as [number, number])
  if (points.length >= 3) {
    L.polygon(points, {
      color: '#d98515',
      weight: 2.5,
      dashArray: '6 4',
      fillColor: '#f0b354',
      fillOpacity: 0.22
    }).addTo(drawLayer)
  }

  points.forEach((point, index) => {
    L.circleMarker(point, {
      radius: 5,
      color: '#1a472a',
      weight: 2,
      fillColor: '#ffffff',
      fillOpacity: 1
    })
      .bindTooltip(String(index + 1), { permanent: false, direction: 'top' })
      .addTo(drawLayer!)
  })
}

function addPoint(event: L.LeafletMouseEvent) {
  const next = [...openRing(props.modelValue), [event.latlng.lng, event.latlng.lat]]
  emit('update:modelValue', next)
}

function undo() {
  const points = openRing(props.modelValue)
  if (points.length === 0) return
  emit('update:modelValue', points.slice(0, -1))
}

function clear() {
  emit('update:modelValue', [])
}

function onResize() {
  map?.invalidateSize()
}

onMounted(async () => {
  await nextTick()
  if (!mapEl.value) return
  map = L.map(mapEl.value, { zoomControl: true }).setView([-33.9249, 18.4241], 11)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap contributors'
  }).addTo(map)
  drawLayer = L.layerGroup().addTo(map)
  map.on('click', addPoint)
  map.getContainer().style.cursor = 'crosshair'
  window.addEventListener('resize', onResize)
  if (typeof ResizeObserver !== 'undefined') {
    resizeObserver = new ResizeObserver(() => map?.invalidateSize())
    resizeObserver.observe(mapEl.value)
  }
  render()
  const initialPoints = openRing(props.modelValue)
  if (initialPoints.length >= 3) {
    const bounds = L.latLngBounds(initialPoints.map(([lng, lat]) => [lat, lng] as [number, number]))
    map.fitBounds(bounds.pad(0.3))
  }
})

watch(() => props.modelValue, render, { deep: true })

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  resizeObserver?.disconnect()
  map?.remove()
  map = null
  drawLayer = null
})
</script>

<template>
  <div class="draw-map">
    <div ref="mapEl" class="draw-canvas" :style="{ height }"></div>
    <div class="draw-toolbar">
      <span>{{ openRing(modelValue).length }} points</span>
      <button type="button" :disabled="modelValue.length === 0" @click="undo">Undo</button>
      <button type="button" :disabled="modelValue.length === 0" @click="clear">Clear</button>
    </div>
    <p class="draw-hint">Click the map to place boundary corners. Use the Farm map editor for detailed reshaping.</p>
  </div>
</template>

<style scoped>
.draw-map {
  position: relative;
  border: 1px solid #dbe5dc;
  border-radius: 14px;
  overflow: hidden;
}

.draw-canvas {
  width: 100%;
  z-index: 1;
}

.draw-toolbar {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  z-index: 500;
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.3rem 0.35rem 0.3rem 0.65rem;
  border: 1px solid #dfe8df;
  border-radius: 9px;
  background: rgba(255, 255, 255, 0.94);
  box-shadow: 0 5px 16px rgba(20, 50, 30, 0.13);
  font-size: 0.72rem;
  font-weight: 700;
  color: #45604f;
}

.draw-toolbar button {
  border: 1px solid #d6e2d7;
  border-radius: 6px;
  background: #f7faf7;
  color: #31503c;
  padding: 0.28rem 0.5rem;
  font-size: 0.7rem;
  font-weight: 700;
  cursor: pointer;
}

.draw-toolbar button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.draw-hint {
  position: absolute;
  left: 0.75rem;
  bottom: 0.75rem;
  z-index: 500;
  max-width: min(430px, calc(100% - 1.5rem));
  margin: 0;
  padding: 0.45rem 0.65rem;
  border-radius: 8px;
  background: rgba(18, 52, 33, 0.9);
  color: #f4fbf5;
  font-size: 0.72rem;
  line-height: 1.4;
}
</style>
