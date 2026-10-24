<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  operationsApi,
  type FarmTask,
  type FarmTaskRequest,
  type OperationsOverview
} from '@/api/operations'
import { enterpriseApi, type Farm, type LandParcel } from '@/api/enterprise'
import { useWorkspaceStore } from '@/store/workspace'
import AppIcon from '@/components/AppIcon.vue'
import AppModal from '@/components/AppModal.vue'
import { formatDateOnly, formatDateTime, formatNumber, titleCase } from '@/utils/enterprise'

const workspace = useWorkspaceStore()

const overview = ref<OperationsOverview | null>(null)
const farms = ref<Farm[]>([])
const parcels = ref<LandParcel[]>([])
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')

const showTaskModal = ref(false)
const editingTask = ref<FarmTask | null>(null)
const saving = ref(false)
const formError = ref('')

const taskForm = reactive({
  farmId: null as number | null,
  landParcelId: null as number | null,
  title: '',
  taskType: 'Inspection',
  priority: 'Medium',
  status: 'Pending',
  dueDate: '',
  notes: ''
})

const taskTypeOptions = ['Inspection', 'Irrigation', 'Maintenance', 'Harvest', 'Planting', 'Other']
const priorityOptions = ['Low', 'Medium', 'High', 'Urgent']
const statusOptions = ['Pending', 'InProgress', 'Completed', 'Cancelled']

const canManage = computed(() => overview.value?.canManage ?? workspace.canManageParcels)
const tasks = computed(() => overview.value?.tasks ?? [])
const parcelsForFarm = computed(() =>
  parcels.value.filter((parcel) => !taskForm.farmId || parcel.farmId === taskForm.farmId)
)
const filteredTasks = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return tasks.value
  return tasks.value.filter((task) =>
    task.title.toLowerCase().includes(term)
    || task.farmName.toLowerCase().includes(term)
    || (task.landParcelName ?? '').toLowerCase().includes(term)
  )
})
const pendingTasks = computed(() => filteredTasks.value.filter((task) => task.status === 'Pending'))
const inProgressTasks = computed(() => filteredTasks.value.filter((task) => task.status === 'InProgress'))
const completedTasks = computed(() => filteredTasks.value.filter((task) => task.status === 'Completed'))

function errorMessage(err: unknown): string {
  const candidate = err as { response?: { data?: { message?: string; detail?: string } }; message?: string }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.message
    || 'The request could not be completed.'
}

function priorityClass(priority: string) {
  switch (priority.toLowerCase()) {
    case 'urgent': return 'priority-urgent'
    case 'high': return 'priority-high'
    case 'medium': return 'priority-medium'
    default: return 'priority-low'
  }
}

function statusClass(status: string) {
  switch (status.toLowerCase()) {
    case 'pending': return 'task-pending'
    case 'inprogress': return 'task-progress'
    case 'completed': return 'task-completed'
    default: return 'task-cancelled'
  }
}

function taskIcon(type: string) {
  switch (type.toLowerCase()) {
    case 'irrigation': return 'soil'
    case 'harvest': return 'inventory'
    case 'planting': return 'crop'
    case 'maintenance': return 'tasks'
    default: return 'tasks'
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await workspace.load()
    const organizationId = workspace.organizationId
    const [operationResponse, farmResponse, parcelResponse] = await Promise.all([
      operationsApi.overview(organizationId),
      enterpriseApi.farms(organizationId),
      enterpriseApi.parcels({ pageSize: 250 }, organizationId)
    ])
    overview.value = operationResponse.data
    farms.value = farmResponse.data
    parcels.value = parcelResponse.data.items
  } catch (err) {
    error.value = errorMessage(err)
  } finally {
    loading.value = false
  }
}

function resetTaskForm() {
  formError.value = ''
  Object.assign(taskForm, {
    farmId: farms.value[0]?.id ?? null,
    landParcelId: null,
    title: '',
    taskType: 'Inspection',
    priority: 'Medium',
    status: 'Pending',
    dueDate: '',
    notes: ''
  })
  editingTask.value = null
}

function openTaskModal() {
  resetTaskForm()
  showTaskModal.value = true
}

function editTask(task: FarmTask) {
  formError.value = ''
  editingTask.value = task
  Object.assign(taskForm, {
    farmId: task.farmId,
    landParcelId: task.landParcelId ?? null,
    title: task.title,
    taskType: task.taskType,
    priority: task.priority,
    status: task.status,
    dueDate: task.dueDate ?? '',
    notes: task.notes ?? ''
  })
  showTaskModal.value = true
}

function onFarmChange() {
  taskForm.landParcelId = null
}

function toRequest(task: FarmTask, status = task.status): FarmTaskRequest {
  return {
    farmId: task.farmId,
    landParcelId: task.landParcelId ?? null,
    title: task.title,
    taskType: task.taskType,
    priority: task.priority,
    status,
    dueDate: task.dueDate ?? null,
    notes: task.notes
  }
}

async function saveTask() {
  formError.value = ''
  if (!taskForm.farmId) {
    formError.value = 'Select the farm for this task.'
    return
  }
  if (taskForm.title.trim().length < 2) {
    formError.value = 'Enter a task title of at least two characters.'
    return
  }
  saving.value = true
  const payload: FarmTaskRequest = {
    farmId: taskForm.farmId,
    landParcelId: taskForm.landParcelId,
    title: taskForm.title.trim(),
    taskType: taskForm.taskType,
    priority: taskForm.priority,
    status: taskForm.status,
    dueDate: taskForm.dueDate || null,
    notes: taskForm.notes.trim() || undefined
  }
  try {
    if (editingTask.value) {
      const { data } = await operationsApi.updateTask(editingTask.value.id, payload)
      message.value = `Task "${data.title}" updated.`
    } else {
      const { data } = await operationsApi.createTask(payload, workspace.organizationId)
      message.value = `Task "${data.title}" created.`
    }
    showTaskModal.value = false
    editingTask.value = null
    await load()
  } catch (err) {
    formError.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function startTask(task: FarmTask) {
  try {
    const { data } = await operationsApi.updateTask(task.id, toRequest(task, 'InProgress'))
    message.value = `Task "${data.title}" moved to in progress.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

async function completeTask(task: FarmTask) {
  try {
    await operationsApi.completeTask(task.id)
    message.value = `Task "${task.title}" completed.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

async function reopenTask(task: FarmTask) {
  try {
    const { data } = await operationsApi.updateTask(task.id, toRequest(task, 'Pending'))
    message.value = `Task "${data.title}" reopened.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

async function deleteTask(task: FarmTask) {
  if (!window.confirm(`Delete the task "${task.title}"?`)) return
  try {
    await operationsApi.deleteTask(task.id)
    message.value = `Task "${task.title}" deleted.`
    await load()
  } catch (err) {
    error.value = errorMessage(err)
  }
}

onMounted(load)
</script>

<template>
  <div class="operations-page">
    <div v-if="error" class="notice notice-error">
      <AppIcon name="warning" :size="18" /> {{ error }}
      <button type="button" @click="load">Retry</button>
    </div>
    <div v-if="message" class="notice notice-success">
      <AppIcon name="tasks" :size="18" /> {{ message }}
      <button type="button" class="dismiss" @click="message = ''">Dismiss</button>
    </div>

    <section class="page-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow-dot"></span> Operational execution</p>
        <h2>Field operations</h2>
        <p class="subtitle">Plan inspections, irrigation, maintenance and harvest work across farms and land parcels.</p>
        <div class="scope-line">
          <span><AppIcon name="tasks" :size="14" /> {{ workspace.activeOrganization?.name }}</span>
          <span v-if="!canManage" class="read-only"><AppIcon name="user" :size="13" /> Read-only role</span>
        </div>
      </div>
      <div class="head-actions">
        <button v-if="canManage" type="button" class="btn btn-primary" @click="openTaskModal">
          <AppIcon name="plus" :size="16" /> New field task
        </button>
      </div>
    </section>

    <div v-if="loading" class="loading-state">Loading field operations...</div>

    <template v-else-if="overview">
      <section class="stat-row">
        <div class="stat"><span>Open tasks</span><strong>{{ formatNumber(overview.metrics.openTasks) }}</strong><small>{{ formatNumber(overview.metrics.inProgress) }} in progress</small></div>
        <div class="stat" :class="{ alert: overview.metrics.overdue > 0 }"><span>Overdue</span><strong>{{ formatNumber(overview.metrics.overdue) }}</strong><small>Past their due date</small></div>
        <div class="stat"><span>Due this week</span><strong>{{ formatNumber(overview.metrics.dueThisWeek) }}</strong><small>{{ formatNumber(overview.metrics.highPriority) }} high priority open</small></div>
        <div class="stat"><span>Completed · 30 days</span><strong>{{ formatNumber(overview.metrics.completedLast30Days) }}</strong><small>Closed field tasks</small></div>
      </section>

      <div v-if="overview.metrics.overdue > 0" class="overdue-banner">
        <span class="overdue-icon"><AppIcon name="warning" :size="18" /></span>
        <div>
          <strong>{{ overview.metrics.overdue }} {{ overview.metrics.overdue === 1 ? 'task is' : 'tasks are' }} overdue</strong>
          <p>Review the pending and in-progress columns to close overdue field work.</p>
        </div>
      </div>

      <section class="board-toolbar">
        <label class="search-field">
          <AppIcon name="search" :size="16" />
          <input v-model="search" type="search" placeholder="Search tasks, farms or parcels..." />
        </label>
        <span class="board-summary">{{ filteredTasks.length }} tasks in view</span>
      </section>

      <section class="task-board">
        <article class="board-column">
          <header class="column-head">
            <span class="column-title"><i class="status-dot pending"></i> Pending</span>
            <span class="column-count">{{ pendingTasks.length }}</span>
          </header>
          <div v-if="pendingTasks.length === 0" class="column-empty">No pending tasks.</div>
          <div v-else class="task-stack">
            <div v-for="task in pendingTasks" :key="task.id" class="task-card">
              <div class="task-top">
                <span class="task-type"><AppIcon :name="taskIcon(task.taskType)" :size="14" /> {{ titleCase(task.taskType) }}</span>
                <span class="priority-pill" :class="priorityClass(task.priority)">{{ task.priority }}</span>
              </div>
              <h4>{{ task.title }}</h4>
              <p class="task-location"><AppIcon name="pin" :size="12" /> {{ task.landParcelName || task.farmName }}</p>
              <div class="task-meta">
                <span :class="{ overdue: task.isOverdue }"><AppIcon name="calendar" :size="12" /> {{ task.dueDate ? formatDateOnly(task.dueDate) : 'No due date' }}</span>
              </div>
              <div v-if="canManage" class="task-actions">
                <button type="button" class="task-action" @click="startTask(task)">Start</button>
                <button type="button" class="task-action" @click="completeTask(task)">Complete</button>
                <button type="button" class="task-action subtle" @click="editTask(task)">Edit</button>
                <button type="button" class="task-action danger" @click="deleteTask(task)">Delete</button>
              </div>
            </div>
          </div>
        </article>

        <article class="board-column">
          <header class="column-head">
            <span class="column-title"><i class="status-dot progress"></i> In progress</span>
            <span class="column-count">{{ inProgressTasks.length }}</span>
          </header>
          <div v-if="inProgressTasks.length === 0" class="column-empty">No work in progress.</div>
          <div v-else class="task-stack">
            <div v-for="task in inProgressTasks" :key="task.id" class="task-card">
              <div class="task-top">
                <span class="task-type"><AppIcon :name="taskIcon(task.taskType)" :size="14" /> {{ titleCase(task.taskType) }}</span>
                <span class="priority-pill" :class="priorityClass(task.priority)">{{ task.priority }}</span>
              </div>
              <h4>{{ task.title }}</h4>
              <p class="task-location"><AppIcon name="pin" :size="12" /> {{ task.landParcelName || task.farmName }}</p>
              <div class="task-meta">
                <span :class="{ overdue: task.isOverdue }"><AppIcon name="calendar" :size="12" /> {{ task.dueDate ? formatDateOnly(task.dueDate) : 'No due date' }}</span>
              </div>
              <div v-if="canManage" class="task-actions">
                <button type="button" class="task-action" @click="completeTask(task)">Complete</button>
                <button type="button" class="task-action subtle" @click="editTask(task)">Edit</button>
                <button type="button" class="task-action danger" @click="deleteTask(task)">Delete</button>
              </div>
            </div>
          </div>
        </article>

        <article class="board-column">
          <header class="column-head">
            <span class="column-title"><i class="status-dot complete"></i> Completed</span>
            <span class="column-count">{{ completedTasks.length }}</span>
          </header>
          <div v-if="completedTasks.length === 0" class="column-empty">Completed tasks will appear here.</div>
          <div v-else class="task-stack">
            <div v-for="task in completedTasks.slice(0, 8)" :key="task.id" class="task-card completed">
              <div class="task-top">
                <span class="task-type"><AppIcon :name="taskIcon(task.taskType)" :size="14" /> {{ titleCase(task.taskType) }}</span>
                <span class="task-status" :class="statusClass(task.status)">{{ titleCase(task.status) }}</span>
              </div>
              <h4>{{ task.title }}</h4>
              <p class="task-location"><AppIcon name="pin" :size="12" /> {{ task.landParcelName || task.farmName }}</p>
              <div class="task-meta">
                <span><AppIcon name="calendar" :size="12" /> Completed {{ task.completedAt ? formatDateTime(task.completedAt) : 'recently' }}</span>
              </div>
              <div v-if="canManage" class="task-actions">
                <button type="button" class="task-action" @click="reopenTask(task)">Reopen</button>
                <button type="button" class="task-action danger" @click="deleteTask(task)">Delete</button>
              </div>
            </div>
          </div>
        </article>
      </section>
    </template>

    <AppModal
      v-if="showTaskModal"
      :title="editingTask ? 'Edit field task' : 'Create a field task'"
      subtitle="Assign operational work to a farm and optional land parcel."
      @close="showTaskModal = false"
    >
      <form id="task-form" class="form-grid" @submit.prevent="saveTask">
        <p v-if="formError" class="form-error">{{ formError }}</p>
        <label class="field span-2">
          <span>Task title</span>
          <input v-model="taskForm.title" maxlength="160" placeholder="e.g. Inspect north orchard irrigation" autofocus />
        </label>
        <label class="field">
          <span>Farm</span>
          <select v-model.number="taskForm.farmId" @change="onFarmChange">
            <option :value="null" disabled>Select a farm</option>
            <option v-for="farm in farms" :key="farm.id" :value="farm.id">{{ farm.name }}</option>
          </select>
        </label>
        <label class="field">
          <span>Land parcel (optional)</span>
          <select v-model.number="taskForm.landParcelId">
            <option :value="null">Farm-wide task</option>
            <option v-for="parcel in parcelsForFarm" :key="parcel.id" :value="parcel.id">{{ parcel.name }}</option>
          </select>
        </label>
        <label class="field">
          <span>Task type</span>
          <select v-model="taskForm.taskType">
            <option v-for="type in taskTypeOptions" :key="type" :value="type">{{ titleCase(type) }}</option>
          </select>
        </label>
        <label class="field">
          <span>Priority</span>
          <select v-model="taskForm.priority">
            <option v-for="priority in priorityOptions" :key="priority" :value="priority">{{ priority }}</option>
          </select>
        </label>
        <label class="field">
          <span>Status</span>
          <select v-model="taskForm.status">
            <option v-for="status in statusOptions" :key="status" :value="status">{{ titleCase(status) }}</option>
          </select>
        </label>
        <label class="field">
          <span>Due date</span>
          <input v-model="taskForm.dueDate" type="date" />
        </label>
        <label class="field span-2">
          <span>Notes</span>
          <textarea v-model="taskForm.notes" maxlength="2000" rows="3" placeholder="Optional task instructions or observations"></textarea>
        </label>
      </form>
      <template #footer>
        <button type="button" class="btn btn-secondary" @click="showTaskModal = false">Cancel</button>
        <button type="submit" form="task-form" class="btn btn-primary" :disabled="saving">{{ saving ? 'Saving...' : editingTask ? 'Save task' : 'Create task' }}</button>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.operations-page { color: #1d3525; }

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

.btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; min-height: 36px; border: 1px solid transparent; border-radius: 9px; padding: 0.48rem 0.75rem; font-size: 0.75rem; font-weight: 700; text-decoration: none; cursor: pointer; }
.btn-primary { background: #1d5a39; border-color: #1d5a39; color: #fff; box-shadow: 0 5px 13px rgba(29, 90, 57, 0.18); }
.btn-primary:hover { background: #16482d; }
.btn-secondary { background: #fff; border-color: #d9e4da; color: #2f513c; }
.btn-secondary:hover { background: #f7faf7; }
.btn:disabled { opacity: 0.62; cursor: not-allowed; }
.loading-state { padding: 2rem; color: #7b8c7f; font-size: 0.8rem; }

.stat-row { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.65rem; margin-bottom: 0.75rem; }
.stat { padding: 0.75rem 0.85rem; border: 1px solid #e1e9e1; border-radius: 11px; background: #fff; }
.stat.alert { border-color: #edcdcd; background: #fffafa; }
.stat > span { display: block; color: #829286; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.stat.alert > span { color: #9b5b5b; }
.stat strong { display: block; margin-top: 0.2rem; color: #1b3f28; font-size: 1.3rem; letter-spacing: -0.03em; }
.stat.alert strong { color: #a74343; }
.stat small { display: block; margin-top: 0.15rem; color: #8d998f; font-size: 0.6rem; }

.overdue-banner { display: flex; align-items: flex-start; gap: 0.65rem; margin-bottom: 0.75rem; padding: 0.7rem 0.85rem; border: 1px solid #f0d4c8; border-radius: 10px; background: #fff9f5; }
.overdue-icon { display: grid; place-items: center; width: 31px; height: 31px; flex: 0 0 auto; border-radius: 8px; background: #fcebe4; color: #b25b39; }
.overdue-banner strong { color: #824232; font-size: 0.74rem; }
.overdue-banner p { margin: 0.18rem 0 0; color: #9a7569; font-size: 0.64rem; }

.board-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 0.8rem; margin-bottom: 0.7rem; }
.search-field { display: flex; align-items: center; gap: 0.4rem; width: 300px; padding: 0.42rem 0.55rem; border: 1px solid #dfe7df; border-radius: 8px; background: #fff; color: #7c8e80; }
.search-field input { min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; color: #24412d; font-size: 0.7rem; }
.board-summary { color: #8a978c; font-size: 0.63rem; }

.task-board { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.75rem; align-items: start; }
.board-column { min-width: 0; border: 1px solid #e1e9e1; border-radius: 13px; background: #fafcfa; overflow: hidden; }
.column-head { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; padding: 0.7rem 0.8rem; border-bottom: 1px solid #e9efe9; background: #fff; }
.column-title { display: inline-flex; align-items: center; gap: 0.35rem; color: #3d5a47; font-size: 0.68rem; font-weight: 800; }
.status-dot { width: 7px; height: 7px; border-radius: 50%; }
.status-dot.pending { background: #b3a078; }
.status-dot.progress { background: #5180a4; }
.status-dot.complete { background: #4a9d70; }
.column-count { border-radius: 999px; background: #f2f5f2; color: #667a6b; padding: 0.12rem 0.4rem; font-size: 0.58rem; font-weight: 800; }
.column-empty { padding: 1.2rem 0.8rem; color: #9aa59b; font-size: 0.65rem; text-align: center; }
.task-stack { display: flex; flex-direction: column; gap: 0.45rem; padding: 0.5rem; }
.task-card { padding: 0.7rem; border: 1px solid #e4ece4; border-radius: 10px; background: #fff; box-shadow: 0 2px 7px rgba(30, 60, 38, 0.035); }
.task-card.completed { opacity: 0.82; }
.task-top { display: flex; align-items: center; justify-content: space-between; gap: 0.4rem; }
.task-type { display: inline-flex; align-items: center; gap: 0.25rem; color: #6d8073; font-size: 0.57rem; font-weight: 700; }
.priority-pill { border-radius: 999px; padding: 0.13rem 0.36rem; font-size: 0.52rem; font-weight: 800; }
.priority-urgent { background: #fdecec; color: #9b1c1c; }
.priority-high { background: #fff3e3; color: #9b6413; }
.priority-medium { background: #f2f6ef; color: #647d48; }
.priority-low { background: #f0f4f0; color: #718074; }
.task-card h4 { margin: 0.35rem 0 0.2rem; color: #26452f; font-size: 0.72rem; line-height: 1.35; }
.task-location { display: flex; align-items: center; gap: 0.25rem; margin: 0; overflow: hidden; color: #8b978d; font-size: 0.6rem; text-overflow: ellipsis; white-space: nowrap; }
.task-meta { display: flex; flex-wrap: wrap; gap: 0.45rem; margin-top: 0.4rem; color: #7c8d80; font-size: 0.58rem; }
.task-meta span { display: inline-flex; align-items: center; gap: 0.22rem; }
.task-meta .overdue { color: #ae4f4f; font-weight: 700; }
.task-status { border-radius: 999px; padding: 0.12rem 0.36rem; font-size: 0.52rem; font-weight: 800; }
.task-pending { background: #f7f0e2; color: #8c702d; }
.task-progress { background: #eaf3fa; color: #346b93; }
.task-completed { background: #e8f5ec; color: #28734a; }
.task-cancelled { background: #f1f4f1; color: #6d7a70; }
.task-actions { display: flex; flex-wrap: wrap; gap: 0.25rem; margin-top: 0.55rem; padding-top: 0.45rem; border-top: 1px solid #f0f4f0; }
.task-action { border: 0; border-radius: 5px; background: #f1f8f3; color: #2f704a; padding: 0.22rem 0.4rem; font-size: 0.56rem; font-weight: 700; cursor: pointer; }
.task-action:hover { background: #e5f3e9; }
.task-action.subtle { background: #f5f7f5; color: #607765; }
.task-action.danger { background: transparent; color: #a15353; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-error { grid-column: 1 / -1; margin: 0; padding: 0.55rem 0.65rem; border: 1px solid #f0c9c9; border-radius: 8px; background: #fdf0f0; color: #8d2f2f; font-size: 0.72rem; }
.field { display: flex; flex-direction: column; gap: 0.28rem; color: #48604f; font-size: 0.7rem; font-weight: 700; }
.field.span-2 { grid-column: span 2; }
.field input, .field select, .field textarea { width: 100%; border: 1px solid #d8e3d9; border-radius: 8px; padding: 0.5rem 0.6rem; background: #fff; color: #263f2e; font-size: 0.76rem; font-weight: 400; outline: none; }
.field input:focus, .field select:focus, .field textarea:focus { border-color: #8fbd9d; box-shadow: 0 0 0 3px rgba(45, 122, 82, 0.08); }
.field textarea { resize: vertical; }

@media (max-width: 1050px) {
  .task-board { grid-template-columns: 1fr; }
}

@media (max-width: 900px) {
  .page-intro { flex-direction: column; align-items: flex-start; }
  .stat-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .board-toolbar { flex-direction: column; align-items: flex-start; }
  .search-field { width: 100%; }
}

@media (max-width: 620px) {
  .stat-row { grid-template-columns: 1fr; }
  .form-grid { grid-template-columns: 1fr; }
  .field.span-2 { grid-column: auto; }
}
</style>
