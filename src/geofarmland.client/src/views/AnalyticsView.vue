<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { analyticsApi, type AnalyticsOverview } from '@/api/analytics'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import { formatHectares, formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()
const overview = ref<AnalyticsOverview | null>(null)
const loading = ref(true)
const error = ref('')

const metrics = computed(() => overview.value?.metrics)
const maxMonthlyHarvest = computed(() =>
  Math.max(1, ...(overview.value?.monthlyHarvests.map((item) => Number(item.quantityTons)) ?? [0]))
)
const hasHarvestHistory = computed(() =>
  (overview.value?.monthlyHarvests ?? []).some((item) => Number(item.quantityTons) > 0)
)
const landUseTotal = computed(() =>
  (overview.value?.landUse ?? []).reduce((total, item) => total + Number(item.hectares), 0)
)
const landUseColors = ['#2d7a55', '#d49a3a', '#517fa4', '#8d6fa1', '#81a25a', '#c27352', '#708d81']

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.message
    || 'The analytics view could not be loaded.'
}

function monthLabel(value: string) {
  const [year, month] = value.split('-').map(Number)
  const date = new Date(year, (month ?? 1) - 1, 1)
  return date.toLocaleDateString(undefined, { month: 'short' })
}

function monthTitle(value: string) {
  const [year, month] = value.split('-').map(Number)
  const date = new Date(year, (month ?? 1) - 1, 1)
  return date.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
}

function barHeight(value: number) {
  if (value <= 0) return 0
  return Math.max(5, Math.round((value / maxMonthlyHarvest.value) * 100))
}

function attainmentTone(value: number) {
  if (value >= 90) return 'good'
  if (value >= 50) return 'medium'
  return 'low'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const { data } = await analyticsApi.overview(workspace.organizationId)
    overview.value = data
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="analytics-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Decision support</p>
        <h2>Analytics &amp; forecasting</h2>
        <p class="subtitle">Production performance, yield attainment and operational signals sourced from the live workspace.</p>
        <div class="scope-line">
          <span><AppIcon name="analytics" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span>Real records only · no synthetic trends</span>
        </div>
      </div>
      <div class="head-actions">
        <button type="button" class="btn btn-secondary" @click="load">
          <AppIcon name="refresh" :size="16" /> Refresh analytics
        </button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Calculating production analytics...</div>

    <template v-else-if="overview && metrics">
      <section class="stat-row">
        <div class="stat"><span>Land under management</span><strong>{{ formatHectares(metrics.landHectares) }}</strong><small>{{ formatNumber(metrics.landParcels) }} land parcels</small></div>
        <div class="stat"><span>Cultivated area</span><strong>{{ formatHectares(metrics.cultivatedHectares) }}</strong><small>{{ formatNumber(metrics.activeCropSeasons) }} active crop seasons</small></div>
        <div class="stat"><span>Yield attainment</span><strong>{{ formatNumber(metrics.yieldAttainmentPercent) }}%</strong><small>{{ formatNumber(metrics.harvestedTons) }} t harvested / {{ formatNumber(metrics.expectedYieldTons) }} t expected</small></div>
        <div class="stat"><span>Livestock head</span><strong>{{ formatNumber(metrics.livestockHead) }}</strong><small>{{ formatNumber(metrics.reportingDevices) }} devices reporting · {{ formatNumber(metrics.readingsLast24Hours) }} readings</small></div>
      </section>

      <div class="analytics-grid top-grid">
        <section class="panel harvest-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Production trend</p>
              <h3>Monthly harvested volume</h3>
              <p class="panel-subtitle">Recorded harvests across the last six calendar months.</p>
            </div>
            <span class="unit-chip">tonnes</span>
          </header>

          <div v-if="hasHarvestHistory" class="bar-chart">
            <div v-for="item in overview.monthlyHarvests" :key="item.month" class="chart-column" :title="monthTitle(item.month)">
              <span class="chart-value">{{ item.quantityTons > 0 ? formatNumber(item.quantityTons) : '' }}</span>
              <div class="chart-track">
                <span class="chart-bar" :style="{ height: `${barHeight(Number(item.quantityTons))}%` }"></span>
              </div>
              <span class="chart-label">{{ monthLabel(item.month) }}</span>
            </div>
          </div>
          <div v-else class="panel-empty">
            <span class="empty-icon"><AppIcon name="analytics" :size="24" /></span>
            <p>No harvest records exist in the last six months. Trend analytics will populate as harvests are logged.</p>
            <router-link to="/modules/crops" class="text-link">Open crop production <AppIcon name="arrow" :size="13" /></router-link>
          </div>
        </section>

        <section class="panel forecast-panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Forward view</p>
              <h3>Harvest forecast</h3>
              <p class="panel-subtitle">Projection from recorded history.</p>
            </div>
          </header>
          <div v-if="overview.forecast" class="forecast-body">
            <p class="forecast-label">Next month projection</p>
            <strong>{{ formatNumber(overview.forecast.projectedNextMonthTons) }} t</strong>
            <p class="forecast-method">{{ overview.forecast.method }}</p>
            <span class="forecast-meta">{{ overview.forecast.monthsWithHarvests }} months with harvest records</span>
          </div>
          <div v-else class="panel-empty">
            <span class="empty-icon"><AppIcon name="analytics" :size="24" /></span>
            <p>At least three months with harvest records are required before a trend projection is calculated.</p>
            <router-link to="/modules/crops" class="text-link">Review crop production <AppIcon name="arrow" :size="13" /></router-link>
          </div>
        </section>
      </div>

      <div class="analytics-grid middle-grid">
        <section class="panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Crop performance</p>
              <h3>Yield attainment by crop</h3>
            </div>
            <router-link to="/modules/crops" class="text-link">Crop register <AppIcon name="arrow" :size="13" /></router-link>
          </header>
          <div v-if="overview.cropPerformance.length" class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Crop</th>
                  <th class="numeric">Cycles</th>
                  <th class="numeric">Area</th>
                  <th class="numeric">Expected</th>
                  <th class="numeric">Harvested</th>
                  <th>Attainment</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in overview.cropPerformance" :key="item.cropName">
                  <td><strong>{{ item.cropName }}</strong></td>
                  <td class="numeric">{{ item.seasons }}</td>
                  <td class="numeric">{{ formatHectares(item.areaHectares) }}</td>
                  <td class="numeric">{{ formatNumber(item.expectedYieldTons) }} t</td>
                  <td class="numeric strong-cell">{{ formatNumber(item.harvestedTons) }} t</td>
                  <td>
                    <div class="attainment">
                      <div class="bar-track"><span :class="attainmentTone(item.attainmentPercent)" :style="{ width: `${Math.min(100, item.attainmentPercent)}%` }"></span></div>
                      <span>{{ formatNumber(item.attainmentPercent) }}%</span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div v-else class="panel-empty">
            <span class="empty-icon"><AppIcon name="crop" :size="24" /></span>
            <p>No crop performance records are available yet.</p>
          </div>
        </section>

        <section class="panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Spatial mix</p>
              <h3>Land use</h3>
            </div>
          </header>
          <div v-if="overview.landUse.length" class="breakdown-list">
            <div v-for="(item, index) in overview.landUse" :key="item.landUse" class="breakdown-row">
              <div class="breakdown-top">
                <span class="breakdown-name"><i :style="{ background: landUseColors[index % landUseColors.length] }"></i>{{ titleCase(item.landUse) }}</span>
                <strong>{{ formatHectares(item.hectares) }}</strong>
              </div>
              <div class="bar-track"><span :style="{ width: `${landUseTotal ? Math.max(3, (Number(item.hectares) / Number(landUseTotal)) * 100) : 0}%`, background: landUseColors[index % landUseColors.length] }"></span></div>
              <small>{{ item.parcels }} parcels</small>
            </div>
          </div>
          <div v-else class="panel-empty"><p>Land use data appears once parcels are registered.</p></div>
        </section>
      </div>

      <div class="analytics-grid bottom-grid">
        <section class="panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Livestock mix</p>
              <h3>Head by species</h3>
            </div>
            <router-link to="/modules/livestock" class="text-link">Livestock register <AppIcon name="arrow" :size="13" /></router-link>
          </header>
          <div v-if="overview.species.length" class="species-grid">
            <div v-for="item in overview.species" :key="item.species" class="species-card">
              <span class="species-name">{{ titleCase(item.species) }}</span>
              <strong>{{ formatNumber(item.headCount) }}</strong>
              <small>{{ item.groups }} {{ item.groups === 1 ? 'group' : 'groups' }}</small>
            </div>
          </div>
          <div v-else class="panel-empty"><p>No active livestock groups have been registered.</p></div>
        </section>

        <section class="panel">
          <header class="panel-head">
            <div>
              <p class="panel-eyebrow">Telemetry · 24 hours</p>
              <h3>Field signal</h3>
            </div>
            <router-link to="/modules/iot" class="text-link">IoT fleet <AppIcon name="arrow" :size="13" /></router-link>
          </header>
          <div class="telemetry-grid">
            <div class="telemetry-stat"><span>Reporting devices</span><strong>{{ formatNumber(metrics.reportingDevices) }}</strong></div>
            <div class="telemetry-stat"><span>Readings captured</span><strong>{{ formatNumber(metrics.readingsLast24Hours) }}</strong></div>
            <div class="telemetry-stat"><span>Rainfall captured</span><strong>{{ formatNumber(metrics.rainfallLast24Hours) }} mm</strong></div>
          </div>
          <p class="telemetry-note">Reporting devices are distinct sensors that submitted at least one reading in the last 24 hours.</p>
        </section>
      </div>

      <footer class="analytics-foot">
        <span><span class="live-dot"></span> Sourced from PostGIS land records, crop harvests, livestock and authenticated telemetry.</span>
        <button type="button" class="text-link" @click="load"><AppIcon name="refresh" :size="13" /> Recalculate</button>
      </footer>
    </template>
  </div>
</template>

<style scoped>
.analytics-page { color: #1d3525; }

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
.head-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }

.btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; min-height: 36px; border: 1px solid transparent; border-radius: 9px; padding: 0.48rem 0.75rem; font-size: 0.75rem; font-weight: 700; text-decoration: none; cursor: pointer; }
.btn-secondary { background: #fff; border-color: #d9e4da; color: #2f513c; }
.btn-secondary:hover { background: #f7faf7; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }

.stat-row { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.65rem; margin-bottom: 0.75rem; }
.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }

.analytics-grid { display: grid; gap: 0.8rem; margin-bottom: 0.8rem; }
.top-grid { grid-template-columns: minmax(0, 1.7fr) minmax(280px, 0.7fr); }
.middle-grid { grid-template-columns: minmax(0, 1.55fr) minmax(290px, 0.8fr); }
.bottom-grid { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); }

.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.panel-subtitle { margin: 0.18rem 0 0; color: #8a978c; font-size: 0.64rem; }
.unit-chip { border-radius: 999px; background: #f2f7f2; color: #617765; padding: 0.18rem 0.45rem; font-size: 0.57rem; font-weight: 800; }
.text-link { display: inline-flex; align-items: center; gap: 0.25rem; border: 0; background: transparent; color: #2f704a; font-size: 0.64rem; font-weight: 700; text-decoration: none; cursor: pointer; white-space: nowrap; }
.text-link:hover { color: #1c4d32; }

.bar-chart { display: flex; align-items: flex-end; justify-content: space-around; gap: 0.5rem; height: 230px; padding: 1.3rem 1.2rem 0.8rem; }
.chart-column { display: flex; flex-direction: column; align-items: center; justify-content: flex-end; height: 100%; flex: 1; min-width: 0; }
.chart-value { height: 16px; color: #526b58; font-size: 0.58rem; font-weight: 700; }
.chart-track { display: flex; align-items: flex-end; justify-content: center; width: 100%; max-width: 56px; flex: 1; border-radius: 6px 6px 2px 2px; background: linear-gradient(180deg, #f8fbf8, #f0f5f0); overflow: hidden; }
.chart-bar { display: block; width: 58%; border-radius: 5px 5px 0 0; background: linear-gradient(180deg, #5fab7c, #2e7b52); box-shadow: 0 2px 6px rgba(46, 123, 82, 0.16); }
.chart-label { margin-top: 0.4rem; color: #8b978d; font-size: 0.58rem; font-weight: 700; text-transform: uppercase; }

.forecast-body { padding: 1.4rem 1rem; }
.forecast-label { margin: 0 0 0.2rem; color: #8a978c; font-size: 0.6rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
.forecast-body > strong { display: block; color: #1d5337; font-size: 2.2rem; letter-spacing: -0.05em; line-height: 1.05; }
.forecast-method { margin: 0.55rem 0 0; color: #6f8074; font-size: 0.66rem; line-height: 1.5; }
.forecast-meta { display: block; margin-top: 0.7rem; padding-top: 0.6rem; border-top: 1px solid #edf2ed; color: #8a978c; font-size: 0.6rem; }

.panel-empty { display: grid; place-items: center; gap: 0.45rem; min-height: 190px; padding: 1.3rem; text-align: center; }
.panel-empty p { max-width: 310px; margin: 0; color: #849287; font-size: 0.7rem; line-height: 1.5; }
.empty-icon { display: grid; place-items: center; width: 46px; height: 46px; border-radius: 13px; background: #f0f6f1; color: #558366; }

.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.8rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.8rem; border-top: 1px solid #eef2ee; color: #4c6353; }
td strong { color: #294931; font-size: 0.7rem; }
.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.attainment { display: flex; align-items: center; gap: 0.4rem; min-width: 110px; }
.bar-track { height: 5px; flex: 1; border-radius: 999px; background: #edf2ed; overflow: hidden; }
.bar-track span { display: block; height: 100%; border-radius: inherit; background: #4a9d70; }
.bar-track span.medium { background: #d49a3a; }
.bar-track span.low { background: #bd6b5d; }
.attainment > span { color: #637a69; font-size: 0.58rem; font-weight: 700; white-space: nowrap; }

.breakdown-list { padding: 0.35rem 1rem 0.9rem; }
.breakdown-row { padding: 0.5rem 0; border-top: 1px solid #eef2ee; }
.breakdown-row:first-child { border-top: 0; }
.breakdown-top { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; color: #415d48; font-size: 0.68rem; }
.breakdown-name { display: inline-flex; align-items: center; gap: 0.4rem; font-weight: 700; }
.breakdown-name i { width: 7px; height: 7px; border-radius: 2px; }
.breakdown-top strong { font-size: 0.64rem; }
.breakdown-row small { display: block; margin-top: 0.2rem; color: #919d93; font-size: 0.58rem; }

.species-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.5rem; padding: 1rem; }
.species-card { padding: 0.7rem; border: 1px solid #edf2ed; border-radius: 9px; background: #fbfcfb; }
.species-name { display: block; color: #7b8c80; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.06em; text-transform: uppercase; }
.species-card strong { display: block; margin-top: 0.2rem; color: #28563b; font-size: 1.05rem; letter-spacing: -0.03em; }
.species-card small { color: #8d998f; font-size: 0.58rem; }

.telemetry-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.5rem; padding: 1rem 1rem 0.5rem; }
.telemetry-stat { padding: 0.65rem; border-radius: 9px; background: #f7faf7; }
.telemetry-stat span { display: block; color: #8a978c; font-size: 0.57rem; font-weight: 700; }
.telemetry-stat strong { display: block; margin-top: 0.22rem; color: #325d41; font-size: 0.9rem; }
.telemetry-note { margin: 0; padding: 0 1rem 1rem; color: #8a978c; font-size: 0.6rem; line-height: 1.45; }

.analytics-foot { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.2rem 0.15rem; color: #849187; font-size: 0.63rem; }
.analytics-foot > span { display: inline-flex; align-items: center; gap: 0.4rem; }
.live-dot { width: 6px; height: 6px; border-radius: 50%; background: #39a267; box-shadow: 0 0 0 3px rgba(57, 162, 103, 0.13); }

@media (max-width: 1120px) {
  .top-grid, .middle-grid, .bottom-grid { grid-template-columns: 1fr; }
}

@media (max-width: 900px) {
  .page-intro { flex-direction: column; align-items: flex-start; }
  .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .species-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}

@media (max-width: 620px) {
  .stat-row { grid-template-columns: 1fr; }
  .species-grid, .telemetry-grid { grid-template-columns: 1fr; }
  .analytics-foot { flex-direction: column; align-items: flex-start; }
}
</style>
