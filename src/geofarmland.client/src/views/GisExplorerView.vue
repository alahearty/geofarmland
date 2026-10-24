<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { enterpriseApi, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import LandParcelMap from '@/components/LandParcelMap.vue'
import AppIcon from '@/components/AppIcon.vue'
import { formatDate, formatHectares, statusClass, titleCase } from '@/utils/enterprise'

const route = useRoute()
const router = useRouter()
const workspace = useWorkspaceStore()

const parcels = ref<LandParcel[]>([])
const selected = ref<LandParcel | null>(null)
const search = ref('')
const statusFilter = ref('')
const loading = ref(true)
const error = ref('')
const mapRef = ref<InstanceType<typeof LandParcelMap> | null>(null)

const filteredParcels = computed(() => {
  const term = search.value.trim().toLowerCase()
  return parcels.value.filter((parcel) => {
    if (statusFilter.value && parcel.status !== statusFilter.value) return false
    if (!term) return true
    return parcel.name.toLowerCase().includes(term)
      || (parcel.code ?? '').toLowerCase().includes(term)
      || parcel.farmName.toLowerCase().includes(term)
  })
})

const totalHectares = computed(() =>
  filteredParcels.value.reduce((total, parcel) => total + Number(parcel.areaHectares), 0)
)
const activeCount = computed(() => filteredParcels.value.filter((parcel) => parcel.status === 'Active').length)
const statuses = computed(() => [...new Set(parcels.value.map((parcel) => parcel.status))].sort())

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string } }; message?: string }
  return candidate.response?.data?.message || candidate.message || 'The land parcel register could not be loaded.'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const { data } = await enterpriseApi.parcels({ pageSize: 250 }, workspace.organizationId)
    parcels.value = data.items
    if (selected.value) {
      selected.value = parcels.value.find((parcel) => parcel.id === selected.value?.id) ?? null
    }
    if (!selected.value) {
      selected.value = filteredParcels.value[0] ?? null
    }
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function selectParcel(parcel: LandParcel, focus = true) {
  selected.value = parcel
  if (focus) mapRef.value?.focusParcel(parcel.id)
}

function openRegister() {
  router.push({ name: 'Farms', query: { new: 'parcel' } })
}

function clearFilters() {
  search.value = ''
  statusFilter.value = ''
}

function syncSearchFromRoute() {
  if (typeof route.query.search === 'string') search.value = route.query.search
}

watch(() => route.query.search, syncSearchFromRoute)
watch(filteredParcels, (next) => {
  if (selected.value && !next.some((parcel) => parcel.id === selected.value?.id)) {
    selected.value = null
  }
})

onMounted(() => {
  syncSearchFromRoute()
  load()
})
</script>

<template>
  <div class="gis-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>

    <section class="gis-head">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Spatial intelligence</p>
        <h2>Land parcel explorer</h2>
        <p class="subtitle">Inspect parcel boundaries, land use and farm context in a PostGIS-backed map.</p>
      </div>
      <div class="head-actions">
        <router-link to="/field-map" class="btn btn-secondary">
          <AppIcon name="pin" :size="17" /> Boundary editor
        </router-link>
        <button v-if="workspace.canManageParcels" type="button" class="btn btn-primary" @click="openRegister">
          <AppIcon name="plus" :size="17" /> New land parcel
        </button>
      </div>
    </section>

    <section class="gis-stats">
      <div class="stat"><span>Visible parcels</span><strong>{{ filteredParcels.length }}</strong></div>
      <div class="stat"><span>Active</span><strong>{{ activeCount }}</strong></div>
      <div class="stat"><span>Mapped area</span><strong>{{ formatHectares(totalHectares) }}</strong></div>
      <div class="stat legend-stat">
        <span>Status legend</span>
        <div class="legend">
          <i class="dot active"></i> Active
          <i class="dot fallow"></i> Fallow
          <i class="dot development"></i> Development
          <i class="dot archived"></i> Archived
        </div>
      </div>
    </section>

    <section class="gis-layout">
      <aside class="parcel-sidebar">
        <div class="sidebar-tools">
          <label class="search-field">
            <AppIcon name="search" :size="16" />
            <input v-model="search" type="search" placeholder="Search parcels or farms" />
          </label>
          <select v-model="statusFilter" aria-label="Filter by status">
            <option value="">All statuses</option>
            <option v-for="status in statuses" :key="status" :value="status">{{ titleCase(status) }}</option>
          </select>
        </div>

        <div v-if="loading" class="side-state">Loading parcel register...</div>
        <div v-else-if="filteredParcels.length === 0" class="side-state">
          <AppIcon name="map" :size="28" />
          <p>No parcels match the current search and status filter.</p>
          <button v-if="search || statusFilter" type="button" class="btn btn-secondary small" @click="clearFilters">Clear filters</button>
        </div>
        <ul v-else class="parcel-list">
          <li v-for="parcel in filteredParcels" :key="parcel.id">
            <button
              type="button"
              class="parcel-card"
              :class="{ active: selected?.id === parcel.id }"
              @click="selectParcel(parcel)"
            >
              <span class="parcel-topline">
                <span class="parcel-name">{{ parcel.name }}</span>
                <span class="status-pill" :class="statusClass(parcel.status)">{{ titleCase(parcel.status) }}</span>
              </span>
              <span class="parcel-farm">{{ parcel.farmName }}<template v-if="parcel.code"> · {{ parcel.code }}</template></span>
              <span class="parcel-meta">
                <span>{{ formatHectares(parcel.areaHectares) }}</span>
                <span>{{ titleCase(parcel.landUse) }}</span>
                <span>{{ titleCase(parcel.tenureType) }}</span>
              </span>
            </button>
          </li>
        </ul>

        <div v-if="selected" class="parcel-detail">
          <div class="detail-head">
            <div>
              <p class="detail-eyebrow">Selected parcel</p>
              <h3>{{ selected.name }}</h3>
            </div>
            <span class="status-pill" :class="statusClass(selected.status)">{{ titleCase(selected.status) }}</span>
          </div>
          <dl class="detail-grid">
            <div><dt>Farm</dt><dd>{{ selected.farmName }}</dd></div>
            <div><dt>Area</dt><dd>{{ formatHectares(selected.areaHectares) }}</dd></div>
            <div><dt>Land use</dt><dd>{{ titleCase(selected.landUse) }}</dd></div>
            <div><dt>Tenure</dt><dd>{{ titleCase(selected.tenureType) }}</dd></div>
            <div><dt>Registered</dt><dd>{{ formatDate(selected.createdAt) }}</dd></div>
            <div><dt>Vertices</dt><dd>{{ selected.coordinates.length }}</dd></div>
          </dl>
          <p v-if="selected.notes" class="detail-notes">{{ selected.notes }}</p>
          <router-link :to="{ name: 'Farms', query: { parcel: selected.id } }" class="detail-link">
            Open in farm register <AppIcon name="arrow" :size="13" />
          </router-link>
        </div>
      </aside>

      <div class="map-panel">
        <div class="map-toolbar">
          <span class="layer-chip"><AppIcon name="layers" :size="14" /> Land parcels</span>
          <span class="map-count">{{ filteredParcels.length }} features</span>
          <button type="button" class="map-action" @click="mapRef?.fitAll()">
            <AppIcon name="refresh" :size="14" /> Fit map
          </button>
        </div>
        <div class="map-stage">
          <LandParcelMap
            ref="mapRef"
            :parcels="filteredParcels"
            :selected-id="selected?.id ?? null"
            height="100%"
            empty-message="No land parcels match the current filter."
            @select="selectParcel"
          >
            <template #empty>
              <button v-if="workspace.canManageParcels" type="button" class="btn btn-primary small" @click="openRegister">
                Register a land parcel
              </button>
            </template>
          </LandParcelMap>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.gis-page {
  color: #1d3525;
}

.notice {
  display: flex;
  align-items: center;
  gap: 0.5rem;
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

.gis-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1.2rem;
  margin-bottom: 0.9rem;
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

.gis-head h2 {
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

.head-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
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
  font-size: 0.7rem;
}

.gis-stats {
  display: grid;
  grid-template-columns: 150px 110px 170px minmax(260px, 1fr);
  gap: 0.6rem;
  margin-bottom: 0.75rem;
}

.stat {
  padding: 0.6rem 0.75rem;
  border: 1px solid #e1e9e1;
  border-radius: 10px;
  background: #fff;
}

.stat > span {
  display: block;
  color: #829286;
  font-size: 0.59rem;
  font-weight: 800;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.stat > strong {
  display: block;
  margin-top: 0.2rem;
  color: #1b3d27;
  font-size: 1.05rem;
  letter-spacing: -0.03em;
}

.legend-stat {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.6rem;
}

.legend {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  color: #6f8074;
  font-size: 0.61rem;
  font-weight: 600;
}

.legend .dot {
  width: 7px;
  height: 7px;
  border-radius: 2px;
  margin-right: -0.28rem;
}

.dot.active { background: #4a9d70; }
.dot.fallow { background: #dca33a; }
.dot.development { background: #4c91b7; }
.dot.archived { background: #98a49b; }

.gis-layout {
  display: grid;
  grid-template-columns: 370px minmax(0, 1fr);
  gap: 0.75rem;
  min-height: calc(100vh - 245px);
}

.parcel-sidebar,
.map-panel {
  min-height: 0;
  border: 1px solid #e1e9e1;
  border-radius: 13px;
  background: #fff;
  box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04);
  overflow: hidden;
}

.parcel-sidebar {
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 245px);
}

.sidebar-tools {
  display: flex;
  gap: 0.45rem;
  padding: 0.7rem;
  border-bottom: 1px solid #edf2ed;
}

.search-field {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex: 1;
  min-width: 0;
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
  font-size: 0.71rem;
}

.sidebar-tools select {
  max-width: 118px;
  border: 1px solid #dfe7df;
  border-radius: 8px;
  background: #f7faf7;
  padding: 0.42rem 0.35rem;
  color: #3f5b49;
  font-size: 0.68rem;
  font-weight: 600;
}

.parcel-list {
  list-style: none;
  flex: 1;
  min-height: 120px;
  margin: 0;
  padding: 0.35rem;
  overflow-y: auto;
}

.parcel-list li + li {
  margin-top: 0.18rem;
}

.parcel-card {
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
  width: 100%;
  padding: 0.55rem 0.6rem;
  border: 1px solid transparent;
  border-radius: 9px;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.parcel-card:hover {
  background: #f7faf7;
}

.parcel-card.active {
  border-color: #b7d4bf;
  background: #eef7f0;
}

.parcel-topline {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.4rem;
}

.parcel-name {
  overflow: hidden;
  color: #24412d;
  font-size: 0.75rem;
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.parcel-farm {
  overflow: hidden;
  color: #859287;
  font-size: 0.62rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.parcel-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  color: #607765;
  font-size: 0.59rem;
  font-weight: 600;
}

.parcel-meta span {
  padding: 0.12rem 0.32rem;
  border-radius: 4px;
  background: #f1f5f1;
}

.status-pill {
  display: inline-flex;
  flex: 0 0 auto;
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

.side-state {
  display: grid;
  flex: 1;
  place-content: center;
  justify-items: center;
  gap: 0.5rem;
  padding: 1.5rem;
  color: #849287;
  text-align: center;
}

.side-state p {
  max-width: 240px;
  margin: 0;
  font-size: 0.72rem;
  line-height: 1.5;
}

.parcel-detail {
  flex: 0 0 auto;
  padding: 0.8rem;
  border-top: 1px solid #e8efe8;
  background: #fbfcfb;
}

.detail-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.55rem;
}

.detail-eyebrow {
  margin: 0 0 0.15rem;
  color: #8a978c;
  font-size: 0.55rem;
  font-weight: 800;
  letter-spacing: 0.09em;
  text-transform: uppercase;
}

.detail-head h3 {
  margin: 0;
  color: #23462f;
  font-size: 0.82rem;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.35rem;
  margin: 0;
}

.detail-grid div {
  min-width: 0;
  padding: 0.35rem 0.4rem;
  border-radius: 6px;
  background: #f2f6f2;
}

.detail-grid dt {
  color: #8c998e;
  font-size: 0.52rem;
  font-weight: 700;
  text-transform: uppercase;
}

.detail-grid dd {
  overflow: hidden;
  margin: 0.12rem 0 0;
  color: #375540;
  font-size: 0.62rem;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.detail-notes {
  margin: 0.5rem 0 0;
  color: #718275;
  font-size: 0.62rem;
  line-height: 1.45;
}

.detail-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  margin-top: 0.5rem;
  color: #2d734b;
  font-size: 0.62rem;
  font-weight: 700;
  text-decoration: none;
}

.map-panel {
  display: flex;
  flex-direction: column;
}

.map-toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.55rem 0.7rem;
  border-bottom: 1px solid #edf2ed;
  background: #fbfcfb;
}

.layer-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.2rem 0.45rem;
  border: 1px solid #d9e7dc;
  border-radius: 6px;
  background: #eef7f0;
  color: #2f704a;
  font-size: 0.6rem;
  font-weight: 800;
}

.map-count {
  color: #8a978c;
  font-size: 0.61rem;
}

.map-action {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  margin-left: auto;
  border: 0;
  background: transparent;
  color: #3a6f4c;
  font-size: 0.62rem;
  font-weight: 700;
  cursor: pointer;
}

.map-stage {
  flex: 1;
  min-height: 480px;
}

@media (max-width: 1100px) {
  .gis-stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .legend-stat {
    grid-column: 1 / -1;
  }

  .gis-layout {
    grid-template-columns: 320px minmax(0, 1fr);
  }
}

@media (max-width: 840px) {
  .gis-head {
    flex-direction: column;
    align-items: flex-start;
  }

  .gis-layout {
    grid-template-columns: 1fr;
  }

  .parcel-sidebar {
    max-height: 480px;
  }

  .map-stage {
    min-height: 460px;
  }
}

@media (max-width: 560px) {
  .gis-stats {
    grid-template-columns: 1fr;
  }

  .legend-stat {
    grid-column: auto;
    align-items: flex-start;
    flex-direction: column;
  }

  .sidebar-tools {
    flex-direction: column;
  }

  .sidebar-tools select {
    max-width: none;
  }
}
</style>
