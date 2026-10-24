import api from './client'

export interface LivestockGroup {
  id: number
  farmId: number
  farmName: string
  grazingLandParcelId?: number | null
  grazingParcelName?: string
  name: string
  species: string
  breed?: string
  purpose: string
  headCount: number
  averageWeightKg?: number | null
  status: string
  joinedDate?: string | null
  notes?: string
  eventCount: number
  lastEventDate?: string | null
  createdAt: string
  updatedAt: string
}

export interface LivestockEvent {
  id: number
  livestockGroupId: number
  groupName: string
  farmName: string
  eventType: string
  eventDate: string
  headCount?: number | null
  weightKg?: number | null
  notes?: string
  createdAt: string
}

export interface SpeciesBreakdown {
  species: string
  groups: number
  headCount: number
}

export interface LivestockMetrics {
  totalHead: number
  activeGroups: number
  quarantineGroups: number
  eventsLast30Days: number
  averageWeightKg?: number | null
}

export interface LivestockOverview {
  canManage: boolean
  metrics: LivestockMetrics
  groups: LivestockGroup[]
  recentEvents: LivestockEvent[]
  speciesBreakdown: SpeciesBreakdown[]
}

export interface LivestockGroupRequest {
  farmId: number
  grazingLandParcelId?: number | null
  name: string
  species?: string
  breed?: string
  purpose?: string
  headCount?: number
  averageWeightKg?: number | null
  status?: string
  joinedDate?: string | null
  notes?: string
}

export interface LivestockEventRequest {
  eventType: string
  eventDate: string
  headCount?: number | null
  weightKg?: number | null
  notes?: string
}

function withOrganization(organizationId?: number) {
  return organizationId ? { organizationId } : {}
}

export const livestockApi = {
  overview: (organizationId?: number) =>
    api.get<LivestockOverview>('/api/livestock/overview', { params: withOrganization(organizationId) }),

  createGroup: (payload: LivestockGroupRequest, organizationId?: number) =>
    api.post<LivestockGroup>('/api/livestock/groups', payload, { params: withOrganization(organizationId) }),

  updateGroup: (id: number, payload: LivestockGroupRequest) =>
    api.put<LivestockGroup>(`/api/livestock/groups/${id}`, payload),

  deleteGroup: (id: number) => api.delete(`/api/livestock/groups/${id}`),

  createEvent: (groupId: number, payload: LivestockEventRequest) =>
    api.post<LivestockEvent>(`/api/livestock/groups/${groupId}/events`, payload)
}
