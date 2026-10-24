import api from './client'

export interface AquaculturePond {
  id: number
  farmId: number
  farmName: string
  landParcelId?: number | null
  landParcelName?: string
  name: string
  waterType: string
  systemType: string
  areaM2: number
  species?: string
  stockCount: number
  averageWeightG?: number | null
  lastStockedDate?: string | null
  status: string
  notes?: string
  createdAt: string
  updatedAt: string
}

export interface AquacultureMetrics {
  ponds: number
  activePonds: number
  stockedPonds: number
  waterAreaM2: number
  totalStock: number
}

export interface AquacultureOverview {
  canManage: boolean
  metrics: AquacultureMetrics
  ponds: AquaculturePond[]
}

export interface AquaculturePondRequest {
  farmId: number
  landParcelId?: number | null
  name: string
  waterType?: string
  systemType?: string
  areaM2: number
  species?: string
  stockCount?: number
  averageWeightG?: number | null
  lastStockedDate?: string | null
  status?: string
  notes?: string
}

export const aquacultureApi = {
  overview: (organizationId?: number) =>
    api.get<AquacultureOverview>('/api/aquaculture/overview', {
      params: organizationId ? { organizationId } : {}
    }),

  create: (payload: AquaculturePondRequest, organizationId?: number) =>
    api.post<AquaculturePond>('/api/aquaculture/ponds', payload, {
      params: organizationId ? { organizationId } : {}
    }),

  update: (id: number, payload: AquaculturePondRequest) =>
    api.put<AquaculturePond>(`/api/aquaculture/ponds/${id}`, payload),

  remove: (id: number) => api.delete(`/api/aquaculture/ponds/${id}`)
}
