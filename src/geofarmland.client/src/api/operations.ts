import api from './client'

export interface FarmTask {
  id: number
  farmId: number
  farmName: string
  landParcelId?: number | null
  landParcelName?: string
  title: string
  taskType: string
  priority: string
  status: string
  dueDate?: string | null
  completedAt?: string | null
  notes?: string
  createdAt: string
  updatedAt: string
  isOverdue: boolean
}

export interface OperationsMetrics {
  openTasks: number
  inProgress: number
  overdue: number
  dueThisWeek: number
  highPriority: number
  completedLast30Days: number
}

export interface OperationsOverview {
  canManage: boolean
  metrics: OperationsMetrics
  tasks: FarmTask[]
}

export interface FarmTaskRequest {
  farmId: number
  landParcelId?: number | null
  title: string
  taskType?: string
  priority?: string
  status?: string
  dueDate?: string | null
  notes?: string
}

function withOrganization(organizationId?: number) {
  return organizationId ? { organizationId } : {}
}

export const operationsApi = {
  overview: (organizationId?: number) =>
    api.get<OperationsOverview>('/api/operations/overview', { params: withOrganization(organizationId) }),

  createTask: (payload: FarmTaskRequest, organizationId?: number) =>
    api.post<FarmTask>('/api/operations/tasks', payload, { params: withOrganization(organizationId) }),

  updateTask: (id: number, payload: FarmTaskRequest) =>
    api.put<FarmTask>(`/api/operations/tasks/${id}`, payload),

  completeTask: (id: number) =>
    api.post<FarmTask>(`/api/operations/tasks/${id}/complete`),

  deleteTask: (id: number) => api.delete(`/api/operations/tasks/${id}`)
}
