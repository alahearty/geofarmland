<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  financeApi,
  type FinancialRecord,
  type FinancialRecordRequest,
  type FinanceOverview
} from '@/api/finance'
import { enterpriseApi, type Farm } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatDateOnly, formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()
const overview = ref<FinanceOverview | null>(null)
const farms = ref<Farm[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const typeFilter = ref('')
const showModal = ref(false)
const editing = ref<FinancialRecord | null>(null)
const saving = ref(false)
const formError = ref('')

const form = reactive({
  farmId: null as number | null,
  recordType: 'Expense',
  category: '',
  amount: null as number | null,
  recordDate: '',
  description: ''
})

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const records = computed(() => overview.value?.records ?? [])
const filteredRecords = computed(() =>
  typeFilter.value ? records.value.filter((record) => record.recordType === typeFilter.value) : records.value
)

const todayIso = () => {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}

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
    const [financeResponse, farmResponse] = await Promise.all([
      financeApi.overview(organizationId),
      enterpriseApi.farms(organizationId)
    ])
    overview.value = financeResponse.data
    farms.value = farmResponse.data
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function openModal() {
  formError.value = ''
  editing.value = null
  Object.assign(form, {
    farmId: null,
    recordType: 'Expense',
    category: '',
    amount: null,
    recordDate: todayIso(),
    description: ''
  })
  showModal.value = true
}

function editRecord(record: FinancialRecord) {
  formError.value = ''
  editing.value = record
  Object.assign(form, {
    farmId: record.farmId ?? null,
    recordType: record.recordType,
    category: record.category,
    amount: Number(record.amount),
    recordDate: record.recordDate,
    description: record.description ?? ''
  })
  showModal.value = true
}

async function save() {
  formError.value = ''
  if (form.category.trim().length < 2) {
    formError.value = 'Enter a category of at least two characters.'
    return
  }
  if (!form.amount || form.amount <= 0) {
    formError.value = 'Enter an amount greater than zero.'
    return
  }
  if (!form.recordDate) {
    formError.value = 'Enter the record date.'
    return
  }
  saving.value = true
  const payload: FinancialRecordRequest = {
    farmId: form.farmId,
    recordType: form.recordType,
    category: form.category.trim(),
    amount: form.amount,
    recordDate: form.recordDate,
    description: form.description.trim() || undefined
  }
  try {
    if (editing.value) {
      await financeApi.update(editing.value.id, payload)
      message.value = 'Financial record updated.'
    } else {
      await financeApi.create(payload, workspace.organizationId)
      message.value = 'Financial record added.'
    }
    showModal.value = false
    editing.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function remove(record: FinancialRecord) {
  if (!window.confirm(`Delete the ${record.recordType.toLowerCase()} record "${record.category}"?`)) return
  try {
    await financeApi.remove(record.id)
    message.value = 'Financial record deleted.'
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

onMounted(load)
</script>

<template>
  <div class="finance-page">
    <div v-if="error" class="notice notice-error"><AppIcon name="warning" :size="18" /> {{ error }}<button type="button" @click="load">Retry</button></div>
    <div v-if="message" class="notice notice-success"><AppIcon name="finance" :size="18" /> {{ message }}<button type="button" class="dismiss" @click="message = ''">Dismiss</button></div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Enterprise performance</p>
        <h2>Finance &amp; reports</h2>
        <p class="subtitle">Record farm revenue and expenses, monitor the net position and review the financial ledger.</p>
        <div class="scope-line">
          <span><AppIcon name="finance" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!canManage" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button v-if="canManage" type="button" class="btn btn-primary" @click="openModal"><AppIcon name="plus" :size="16" /> Add record</button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading financial ledger...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat revenue"><span>Revenue</span><strong>{{ formatNumber(overview.metrics.revenue) }}</strong><small>Recorded income</small></div>
        <div class="stat expense"><span>Expenses</span><strong>{{ formatNumber(overview.metrics.expenses) }}</strong><small>Recorded costs</small></div>
        <div class="stat" :class="overview.metrics.netPosition >= 0 ? 'net-positive' : 'net-negative'"><span>Net position</span><strong>{{ formatNumber(overview.metrics.netPosition) }}</strong><small>Revenue less expenses</small></div>
        <div class="stat"><span>Ledger records</span><strong>{{ formatNumber(overview.metrics.records) }}</strong><small>Revenue and expense entries</small></div>
      </section>

      <section class="panel">
        <header class="panel-head">
          <div><p class="panel-eyebrow">Ledger</p><h3>Financial records</h3><p class="panel-subtitle">{{ filteredRecords.length }} entries in view</p></div>
          <select v-model="typeFilter" aria-label="Filter financial record type">
            <option value="">All records</option>
            <option value="Revenue">Revenue</option>
            <option value="Expense">Expense</option>
          </select>
        </header>

        <div v-if="filteredRecords.length === 0" class="empty-panel">
          <span class="empty-icon"><AppIcon name="finance" :size="26" /></span>
          <p>No financial records yet. Add revenue and expense entries to build the enterprise ledger.</p>
          <button v-if="canManage" type="button" class="btn btn-primary" @click="openModal">Add first record</button>
        </div>
        <div v-else class="table-wrap">
          <table>
            <thead><tr><th>Date</th><th>Type</th><th>Category</th><th>Farm</th><th class="numeric">Amount</th><th>Description</th><th class="actions-col"></th></tr></thead>
            <tbody>
              <tr v-for="record in filteredRecords" :key="record.id">
                <td>{{ formatDateOnly(record.recordDate) }}</td>
                <td><span class="record-pill" :class="record.recordType === 'Revenue' ? 'revenue' : 'expense'">{{ titleCase(record.recordType) }}</span></td>
                <td><strong>{{ record.category }}</strong></td>
                <td>{{ record.farmName || 'Organisation-wide' }}</td>
                <td class="numeric" :class="record.recordType === 'Revenue' ? 'amount-revenue' : 'amount-expense'">
                  {{ record.recordType === 'Revenue' ? '+' : '−' }} {{ formatNumber(record.amount) }}
                </td>
                <td class="muted-cell description-cell">{{ record.description || '—' }}</td>
                <td class="actions-cell"><template v-if="canManage"><button type="button" class="row-action" @click="editRecord(record)">Edit</button><button type="button" class="row-action danger" @click="remove(record)">Delete</button></template></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="ledger-note">
        <AppIcon name="finance" :size="18" />
        <p>Values are recorded in the organisation currency configured in the workspace context. No accounting periods, tax or bank reconciliation are applied yet.</p>
      </section>
    </template>

    <AppModal v-if="showModal" :title="editing ? 'Edit financial record' : 'Add financial record'" subtitle="Capture a revenue or expense entry for the ledger." @close="showModal = false">
      <form id="finance-form" class="form-grid" @submit.prevent="save">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field"><span>Type</span><select v-model="form.recordType"><option value="Revenue">Revenue</option><option value="Expense">Expense</option></select></label>
        <label class="field"><span>Record date</span><input v-model="form.recordDate" type="date" :max="todayIso()" /></label>
        <label class="field span-2"><span>Category</span><input v-model="form.category" maxlength="80" placeholder="e.g. Produce sales, fertiliser, labour" autofocus /></label>
        <label class="field"><span>Amount</span><input v-model.number="form.amount" type="number" min="0.01" step="0.01" /></label>
        <label class="field"><span>Farm (optional)</span><select v-model.number="form.farmId"><option :value="null">Organisation-wide</option><option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option></select></label>
        <label class="field span-2"><span>Description</span><input v-model="form.description" maxlength="500" placeholder="Optional record description" /></label>
      </form>
      <template #footer><button type="button" class="btn btn-secondary" @click="showModal = false">Cancel</button><button type="submit" form="finance-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editing ? 'Save record' : 'Add record' }}</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.finance-page { color: #1d3525; }
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
.btn-primary { background: #1d5a39; border-color: #1d5a39; color: #fff; }
.btn-primary:hover { background: #16482d; }
.btn-secondary { background: #fff; border-color: #d9e4da; color: #2f513c; }
.btn:disabled { opacity: 0.62; cursor: not-allowed; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }
.stat-row { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.65rem; margin-bottom: 0.75rem; }
.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }
.stat.revenue { border-color: #cfe6d6; background: #fbfefc; }
.stat.revenue strong { color: #277049; }
.stat.expense { border-color: #f0d7d2; background: #fffdfc; }
.stat.expense strong { color: #a05247; }
.stat.net-positive { border-color: #cfe6d6; }
.stat.net-positive strong { color: #277049; }
.stat.net-negative { border-color: #f0d7d2; }
.stat.net-negative strong { color: #a05247; }
.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.panel-subtitle { margin: 0.18rem 0 0; color: #8a978c; font-size: 0.64rem; }
.panel-tools { display: flex; align-items: center; gap: 0.45rem; }
.panel-tools select, .panel-head > select { border: 1px solid #dfe7df; border-radius: 8px; background: #f7faf7; padding: 0.42rem 0.45rem; color: #3f5b49; font-size: 0.68rem; font-weight: 600; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.8rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.8rem; border-top: 1px solid #eef2ee; color: #4c6353; white-space: nowrap; }
td strong { color: #294931; font-size: 0.7rem; }
.numeric { text-align: right; }
.amount-revenue { color: #28734a; font-weight: 700; }
.amount-expense { color: #a05247; font-weight: 700; }
.muted-cell { color: #89968c; }
.description-cell { max-width: 260px; overflow: hidden; text-overflow: ellipsis; }
.record-pill { display: inline-flex; border-radius: 999px; padding: 0.14rem 0.4rem; font-size: 0.55rem; font-weight: 800; }
.record-pill.revenue { background: #e8f5ec; color: #28734a; }
.record-pill.expense { background: #fdeee8; color: #a15a3d; }
.actions-col { width: 100px; }
.actions-cell { text-align: right; }
.row-action { border: 0; background: transparent; color: #2f704a; font-size: 0.6rem; font-weight: 700; cursor: pointer; }
.row-action + .row-action { margin-left: 0.4rem; }
.row-action.danger { color: #9b4141; }
.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 2.5rem 1.5rem; text-align: center; }
.empty-panel p { max-width: 420px; margin: 0; color: #849287; font-size: 0.7rem; line-height: 1.5; }
.empty-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; background: #f2f6ef; color: #6f8448; }
.ledger-note { display: flex; align-items: flex-start; gap: 0.5rem; margin-top: 0.8rem; padding: 0.7rem 0.85rem; border: 1px solid #e4ebe4; border-radius: 9px; background: #f8fbf8; color: #7e8c81; }
.ledger-note p { margin: 0; font-size: 0.63rem; line-height: 1.45; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
@media (max-width: 900px) { .page-intro { flex-direction: column; align-items: flex-start; } .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } .panel-head { flex-direction: column; } }
@media (max-width: 620px) { .stat-row { grid-template-columns: 1fr; } .form-grid { grid-template-columns: 1fr; } .field.span-2 { grid-column: auto; } }
</style>
