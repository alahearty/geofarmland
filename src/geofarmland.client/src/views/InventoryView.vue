<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  inventoryApi,
  type InventoryItem,
  type InventoryItemRequest,
  type InventoryOverview
} from '@/api/inventory'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()
const overview = ref<InventoryOverview | null>(null)
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const typeFilter = ref('')
const showModal = ref(false)
const editing = ref<InventoryItem | null>(null)
const saving = ref(false)
const formError = ref('')

const form = reactive({
  name: '',
  itemType: 'Input',
  unit: '',
  quantityOnHand: 0,
  reorderLevel: 0,
  unitCost: null as number | null,
  supplier: '',
  notes: ''
})

const itemTypes = ['Input', 'Equipment', 'Tool', 'Fuel', 'Other']

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const items = computed(() => overview.value?.items ?? [])
const types = computed(() => [...new Set(items.value.map((item) => item.itemType))].sort())
const filteredItems = computed(() => {
  const term = search.value.trim().toLowerCase()
  return items.value.filter((item) => {
    if (typeFilter.value && item.itemType !== typeFilter.value) return false
    if (!term) return true
    return item.name.toLowerCase().includes(term)
      || item.unit.toLowerCase().includes(term)
      || (item.supplier ?? '').toLowerCase().includes(term)
  })
})
const visibleValue = computed(() =>
  filteredItems.value.reduce((total, item) => total + Number(item.stockValue ?? 0), 0)
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
    const { data } = await inventoryApi.overview(workspace.organizationId)
    overview.value = data
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
    name: '',
    itemType: 'Input',
    unit: '',
    quantityOnHand: 0,
    reorderLevel: 0,
    unitCost: null,
    supplier: '',
    notes: ''
  })
  showModal.value = true
}

function editItem(item: InventoryItem) {
  formError.value = ''
  editing.value = item
  Object.assign(form, {
    name: item.name,
    itemType: item.itemType,
    unit: item.unit,
    quantityOnHand: Number(item.quantityOnHand),
    reorderLevel: Number(item.reorderLevel),
    unitCost: item.unitCost == null ? null : Number(item.unitCost),
    supplier: item.supplier ?? '',
    notes: item.notes ?? ''
  })
  showModal.value = true
}

async function save() {
  formError.value = ''
  if (form.name.trim().length < 2) {
    formError.value = 'Enter an item name of at least two characters.'
    return
  }
  if (!form.unit.trim()) {
    formError.value = 'Enter the stock unit.'
    return
  }
  saving.value = true
  const payload: InventoryItemRequest = {
    name: form.name.trim(),
    itemType: form.itemType,
    unit: form.unit.trim(),
    quantityOnHand: form.quantityOnHand,
    reorderLevel: form.reorderLevel,
    unitCost: form.unitCost,
    supplier: form.supplier.trim() || undefined,
    notes: form.notes.trim() || undefined
  }
  try {
    if (editing.value) {
      const { data } = await inventoryApi.update(editing.value.id, payload)
      message.value = `"${data.name}" updated.`
    } else {
      const { data } = await inventoryApi.create(payload, workspace.organizationId)
      message.value = `"${data.name}" added to inventory.`
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

async function remove(item: InventoryItem) {
  if (!window.confirm(`Remove "${item.name}" from inventory?`)) return
  try {
    await inventoryApi.remove(item.id)
    message.value = `"${item.name}" removed.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

onMounted(load)
</script>

<template>
  <div class="inventory-page">
    <div v-if="error" class="notice notice-error"><AppIcon name="warning" :size="18" /> {{ error }}<button type="button" @click="load">Retry</button></div>
    <div v-if="message" class="notice notice-success"><AppIcon name="inventory" :size="18" /> {{ message }}<button type="button" class="dismiss" @click="message = ''">Dismiss</button></div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Supply control</p>
        <h2>Inventory &amp; equipment</h2>
        <p class="subtitle">Track inputs, tools, machinery and consumables with reorder thresholds and stock value.</p>
        <div class="scope-line">
          <span><AppIcon name="inventory" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!canManage" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button v-if="canManage" type="button" class="btn btn-primary" @click="openModal"><AppIcon name="plus" :size="16" /> Add inventory item</button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading inventory...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Inventory items</span><strong>{{ formatNumber(overview.metrics.items) }}</strong><small>{{ types.length }} categories in use</small></div>
        <div class="stat" :class="{ alert: overview.metrics.lowStock > 0 }"><span>Low stock</span><strong>{{ formatNumber(overview.metrics.lowStock) }}</strong><small>At or below reorder level</small></div>
        <div class="stat"><span>Equipment items</span><strong>{{ formatNumber(overview.metrics.equipment) }}</strong><small>Machinery, tools and assets</small></div>
        <div class="stat"><span>Stock value</span><strong>{{ formatNumber(overview.metrics.stockValue) }}</strong><small>Based on recorded unit cost</small></div>
      </section>

      <section class="panel">
        <header class="panel-head">
          <div>
            <p class="panel-eyebrow">Stock register</p>
            <h3>Inventory items</h3>
            <p class="panel-subtitle">{{ filteredItems.length }} items · value {{ formatNumber(visibleValue) }}</p>
          </div>
          <div class="panel-tools">
            <label class="search-field"><AppIcon name="search" :size="16" /><input v-model="search" type="search" placeholder="Search inventory..." /></label>
            <select v-model="typeFilter" aria-label="Filter inventory type">
              <option value="">All types</option>
              <option v-for="type in types" :key="type" :value="type">{{ titleCase(type) }}</option>
            </select>
          </div>
        </header>

        <div v-if="filteredItems.length === 0" class="empty-panel">
          <span class="empty-icon"><AppIcon name="inventory" :size="26" /></span>
          <p>{{ items.length === 0 ? 'No inventory items yet. Add inputs, equipment or consumables to begin stock control.' : 'No items match the current view.' }}</p>
          <button v-if="canManage && items.length === 0" type="button" class="btn btn-primary" @click="openModal">Add first item</button>
        </div>
        <div v-else class="table-wrap">
          <table>
            <thead><tr><th>Item</th><th>Type</th><th>Unit</th><th class="numeric">On hand</th><th class="numeric">Reorder</th><th class="numeric">Unit cost</th><th class="numeric">Stock value</th><th>Status</th><th class="actions-col"></th></tr></thead>
            <tbody>
              <tr v-for="item in filteredItems" :key="item.id">
                <td><div class="item-cell"><strong>{{ item.name }}</strong><small>{{ item.supplier || 'No supplier' }}</small></div></td>
                <td>{{ titleCase(item.itemType) }}</td>
                <td>{{ item.unit }}</td>
                <td class="numeric strong-cell">{{ formatNumber(item.quantityOnHand) }}</td>
                <td class="numeric">{{ formatNumber(item.reorderLevel) }}</td>
                <td class="numeric">{{ item.unitCost == null ? '—' : formatNumber(item.unitCost) }}</td>
                <td class="numeric">{{ item.stockValue == null ? '—' : formatNumber(item.stockValue) }}</td>
                <td><span class="stock-pill" :class="item.isLowStock ? 'low' : 'ok'">{{ item.isLowStock ? 'Low stock' : 'In stock' }}</span></td>
                <td class="actions-cell"><template v-if="canManage"><button type="button" class="row-action" @click="editItem(item)">Edit</button><button type="button" class="row-action danger" @click="remove(item)">Delete</button></template></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>

    <AppModal v-if="showModal" :title="editing ? 'Edit inventory item' : 'Add inventory item'" subtitle="Record stock, reorder and unit cost information." @close="showModal = false">
      <form id="inventory-form" class="form-grid" @submit.prevent="save">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2"><span>Item name</span><input v-model="form.name" maxlength="160" placeholder="e.g. NPK fertiliser" autofocus /></label>
        <label class="field"><span>Type</span><select v-model="form.itemType"><option v-for="type in itemTypes" :key="type" :value="type">{{ titleCase(type) }}</option></select></label>
        <label class="field"><span>Unit</span><input v-model="form.unit" maxlength="40" placeholder="e.g. bag, litre, unit" /></label>
        <label class="field"><span>Quantity on hand</span><input v-model.number="form.quantityOnHand" type="number" min="0" step="0.001" /></label>
        <label class="field"><span>Reorder level</span><input v-model.number="form.reorderLevel" type="number" min="0" step="0.001" /></label>
        <label class="field"><span>Unit cost</span><input v-model.number="form.unitCost" type="number" min="0" step="0.01" /></label>
        <label class="field"><span>Supplier</span><input v-model="form.supplier" maxlength="160" placeholder="Optional supplier" /></label>
        <label class="field span-2"><span>Notes</span><input v-model="form.notes" maxlength="1000" placeholder="Optional stock note" /></label>
      </form>
      <template #footer><button type="button" class="btn btn-secondary" @click="showModal = false">Cancel</button><button type="submit" form="inventory-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editing ? 'Save item' : 'Add item' }}</button></template>
    </AppModal>
  </div>
</template>

<style scoped>
.inventory-page { color: #1d3525; }
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
.btn-secondary:hover { background: #f7faf7; }
.btn:disabled { opacity: 0.62; cursor: not-allowed; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }
.stat-row { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.65rem; margin-bottom: 0.75rem; }
.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat.alert { border-color: #f0d4c8; background: #fff9f5; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat.alert > span { color: #a36b57; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat.alert strong { color: #a85a3b; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }
.panel { border: 1px solid #e1e9e1; border-radius: 13px; background: #fff; box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04); overflow: hidden; }
.panel-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; border-bottom: 1px solid #edf2ed; }
.panel-eyebrow { margin: 0 0 0.15rem; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.panel-head h3 { margin: 0; color: #1c3a26; font-size: 0.95rem; }
.panel-subtitle { margin: 0.18rem 0 0; color: #8a978c; font-size: 0.64rem; }
.panel-tools { display: flex; align-items: center; gap: 0.45rem; }
.search-field { display: flex; align-items: center; gap: 0.4rem; width: 240px; padding: 0.42rem 0.55rem; border: 1px solid #dfe7df; border-radius: 8px; background: #f7faf7; color: #7c8e80; }
.search-field input { min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; color: #24412d; font-size: 0.7rem; }
.panel-tools select { max-width: 130px; border: 1px solid #dfe7df; border-radius: 8px; background: #f7faf7; padding: 0.42rem 0.35rem; color: #3f5b49; font-size: 0.68rem; font-weight: 600; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 0.7rem; }
th { padding: 0.55rem 0.8rem; color: #8b978d; background: #fbfcfb; font-size: 0.54rem; font-weight: 800; letter-spacing: 0.07em; text-align: left; text-transform: uppercase; white-space: nowrap; }
td { padding: 0.58rem 0.8rem; border-top: 1px solid #eef2ee; color: #4c6353; white-space: nowrap; }
.item-cell { display: flex; flex-direction: column; }
.item-cell strong { color: #294931; font-size: 0.7rem; }
.item-cell small { color: #939f94; font-size: 0.58rem; }
.numeric { text-align: right; }
.strong-cell { color: #24432e; font-weight: 700; }
.stock-pill { display: inline-flex; border-radius: 999px; padding: 0.14rem 0.4rem; font-size: 0.55rem; font-weight: 800; }
.stock-pill.ok { background: #e8f5ec; color: #28734a; }
.stock-pill.low { background: #fdeee8; color: #a15a3d; }
.actions-col { width: 100px; }
.actions-cell { text-align: right; }
.row-action { border: 0; background: transparent; color: #2f704a; font-size: 0.6rem; font-weight: 700; cursor: pointer; }
.row-action + .row-action { margin-left: 0.4rem; }
.row-action.danger { color: #9b4141; }
.empty-panel { display: grid; place-items: center; gap: 0.45rem; padding: 2.5rem 1.5rem; text-align: center; }
.empty-panel p { max-width: 420px; margin: 0; color: #849287; font-size: 0.7rem; line-height: 1.5; }
.empty-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; background: #f2f6ef; color: #6f8448; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
@media (max-width: 900px) { .page-intro { flex-direction: column; align-items: flex-start; } .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } .panel-head { flex-direction: column; } .panel-tools { width: 100%; } .search-field { flex: 1; width: auto; } }
@media (max-width: 620px) { .stat-row { grid-template-columns: 1fr; } .form-grid { grid-template-columns: 1fr; } .field.span-2 { grid-column: auto; } .panel-tools { flex-direction: column; align-items: stretch; } .panel-tools select { max-width: none; } }
</style>
