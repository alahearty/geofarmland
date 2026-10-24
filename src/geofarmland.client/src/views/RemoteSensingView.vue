<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { enterpriseApi, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import LandParcelMap from '@/components/LandParcelMap.vue'
import { formatHectares, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const selected = ref<LandParcel | null>(null)

const totalHectares = computed(() => parcels.value.reduce((total, parcel) => total + Number(parcel.areaHectares), 0))
const activeParcels = computed(() => parcels.value.filter((parcel) => parcel.status === 'Active').length)
const farmCount = computed(() => new Set(parcels.value.map((parcel) => parcel.farmName)).size)

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string } }; message?: string }
  return candidate.response?.data?.message || candidate.message || 'The remote sensing readiness view could not be loaded.'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const { data } = await enterpriseApi.parcels({ pageSize: 250 }, workspace.organizationId)
    parcels.value = data.items
    selected.value = parcels.value[0] ?? null
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="remote-page">
    <div v-if="error" class="notice notice-error"><AppIcon name="warning" :size="18" /> {{ error }}<button type="button" @click="load">Retry</button></div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Earth observation</p>
        <h2>Remote sensing</h2>
        <p class="subtitle">Parcel-ready spatial coverage for satellite imagery, vegetation indices and change detection.</p>
        <div class="scope-line"><span><AppIcon name="satellite" :size="14" /> {{ workspace.activeOrganization?.name }}</span><span class="provider-note">Imagery provider not configured</span></div>
      </div>
      <button type="button" class="btn btn-secondary" @click="load"><AppIcon name="refresh" :size="16" /> Refresh readiness</button>
    </section>

    <div v-if="loading" class="loading-state">Assessing parcel readiness...</div>

    <template v-else>
      <section class="stat-row">
        <div class="stat"><span>Parcels ready</span><strong>{{ parcels.length }}</strong><small>Valid PostGIS boundaries</small></div>
        <div class="stat"><span>Observable area</span><strong>{{ formatHectares(totalHectares) }}</strong><small>{{ farmCount }} farms represented</small></div>
        <div class="stat"><span>Active parcels</span><strong>{{ activeParcels }}</strong><small>Eligible for monitoring</small></div>
        <div class="stat provider-stat"><span>Imagery scenes</span><strong>0</strong><small>No provider connected</small></div>
      </section>

      <div class="remote-layout">
        <section class="panel map-panel">
          <header class="panel-head">
            <div><p class="panel-eyebrow">Coverage</p><h3>Parcel boundaries ready for imagery</h3></div>
            <span class="map-count">{{ parcels.length }} features</span>
          </header>
          <LandParcelMap :parcels="parcels" :selected-id="selected?.id ?? null" height="430px" @select="selected = $event" />
          <div v-if="selected" class="selected-strip">
            <span class="strip-icon"><AppIcon name="satellite" :size="16" /></span>
            <span><strong>{{ selected.name }}</strong><small>{{ selected.farmName }} · {{ titleCase(selected.landUse) }}</small></span>
            <span class="strip-area">{{ formatHectares(selected.areaHectares) }}</span>
          </div>
        </section>

        <aside class="side-stack">
          <section class="panel">
            <header class="panel-head">
              <div><p class="panel-eyebrow">Integration status</p><h3>Imagery pipeline</h3></div>
              <span class="status-badge">Not connected</span>
            </header>
            <div class="pipeline">
              <div class="pipeline-step done"><span class="step-dot"></span><div><strong>Parcel geometry</strong><p>{{ parcels.length }} land parcels have valid boundaries in PostGIS.</p></div></div>
              <div class="pipeline-step done"><span class="step-dot"></span><div><strong>Organisation scope</strong><p>Access is resolved through the authenticated workspace.</p></div></div>
              <div class="pipeline-step pending"><span class="step-dot"></span><div><strong>Imagery provider</strong><p>No Sentinel, Landsat or commercial provider credentials are configured.</p></div></div>
              <div class="pipeline-step pending"><span class="step-dot"></span><div><strong>Index processing</strong><p>NDVI, NDMI and scene clipping will run once imagery is available.</p></div></div>
            </div>
            <div class="pipeline-note">
              <AppIcon name="warning" :size="15" />
              <p>No vegetation index or imagery values are shown because no provider is connected. GeoFarmland does not display synthetic satellite data.</p>
            </div>
          </section>

          <section class="panel">
            <header class="panel-head"><div><p class="panel-eyebrow">Parcel readiness</p><h3>Assets in scope</h3></div></header>
            <ul v-if="parcels.length" class="parcel-list">
              <li v-for="parcel in parcels.slice(0, 6)" :key="parcel.id" @click="selected = parcel">
                <span class="parcel-icon"><AppIcon name="map" :size="14" /></span>
                <span class="parcel-copy"><strong>{{ parcel.name }}</strong><small>{{ parcel.farmName }} · {{ titleCase(parcel.landUse) }}</small></span>
                <span class="parcel-area">{{ formatHectares(parcel.areaHectares) }}</span>
              </li>
            </ul>
            <div v-else class="compact-empty"><p>No parcels are available to prepare for remote sensing.</p></div>
          </section>
        </aside>
      </div>
    </template>
  </div>
</template>

<style scoped>
.remote-page { color: #1d3525; }
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
.provider-note { padding: 0.18rem 0.45rem; border-radius: 999px; background: #fff6df; color: #956315; }
.btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; min-height: 36px; border: 1px solid #d9e4da; border-radius: 9px; padding: 0.48rem 0.75rem; background: #fff; color: #2f513c; font-size: 0.75rem; font-weight: 700; cursor: pointer; }
.btn:hover { background: #f7faf7; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }
.stat-row { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.65rem; margin-bottom: 0.75rem; }
.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }
.provider-stat { border-color: #f0e1bd; background: #fffdf7; }
.provider-stat strong { color: #9b6618; }
.remote-layout { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(320px, 0.8fr); gap: 0.8rem; align-items: start; }
.side-stack { display: flex; flex-direction: column; gap: 0.8rem; }
.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.map-count { color: #8a978c; font-size: 0.61rem; }
.map-panel { overflow: hidden; }
.selected-strip { display: flex; align-items: center; gap: 0.5rem; padding: 0.6rem 0.8rem; border-top: 1px solid #edf2ed; background: #fbfcfb; }
.strip-icon { display: grid; place-items: center; width: 29px; height: 29px; border-radius: 8px; background: #eaf1f8; color: #426f93; }
.selected-strip > span:nth-child(2) { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.selected-strip strong { color: #294931; font-size: 0.68rem; }
.selected-strip small { color: #8b978d; font-size: 0.58rem; }
.strip-area { color: #3e6049; font-size: 0.64rem; font-weight: 800; }
.status-badge { border-radius: 999px; background: #fff6df; color: #956315; padding: 0.16rem 0.42rem; font-size: 0.54rem; font-weight: 800; }
.pipeline { padding: 0.8rem 1rem; }
.pipeline-step { display: flex; gap: 0.55rem; padding: 0.5rem 0; border-top: 1px solid #eef2ee; }
.pipeline-step:first-child { border-top: 0; }
.step-dot { width: 8px; height: 8px; flex: 0 0 auto; margin-top: 0.3rem; border-radius: 50%; background: #4a9d70; }
.pipeline-step.pending .step-dot { background: #c7a75b; }
.pipeline-step strong { color: #2b4a34; font-size: 0.67rem; }
.pipeline-step p { margin: 0.1rem 0 0; color: #879589; font-size: 0.6rem; line-height: 1.4; }
.pipeline-note { display: flex; gap: 0.4rem; margin: 0 1rem 1rem; padding: 0.55rem 0.6rem; border-radius: 8px; background: #fbf8ed; color: #8a7548; }
.pipeline-note p { margin: 0; font-size: 0.6rem; line-height: 1.45; }
.parcel-list { list-style: none; margin: 0; padding: 0.3rem 0.7rem 0.7rem; }
.parcel-list li { display: flex; align-items: center; gap: 0.45rem; padding: 0.5rem 0.2rem; border-top: 1px solid #eef2ee; cursor: pointer; }
.parcel-list li:first-child { border-top: 0; }
.parcel-icon { display: grid; place-items: center; width: 26px; height: 26px; flex: 0 0 auto; border-radius: 7px; background: #eef6f0; color: #4a7c5b; }
.parcel-copy { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.parcel-copy strong { overflow: hidden; color: #294931; font-size: 0.66rem; text-overflow: ellipsis; white-space: nowrap; }
.parcel-copy small { overflow: hidden; color: #939f94; font-size: 0.56rem; text-overflow: ellipsis; white-space: nowrap; }
.parcel-area { color: #3e6049; font-size: 0.6rem; font-weight: 800; }
.compact-empty { padding: 1rem; color: #879589; font-size: 0.66rem; }
.compact-empty p { margin: 0; }
@media (max-width: 1000px) { .remote-layout { grid-template-columns: 1fr; } .side-stack { display: grid; grid-template-columns: 1fr 1fr; } }
@media (max-width: 760px) { .page-intro { flex-direction: column; align-items: flex-start; } .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } .side-stack { grid-template-columns: 1fr; } }
@media (max-width: 520px) { .stat-row { grid-template-columns: 1fr; } }
</style>
