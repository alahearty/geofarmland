import api from './client'

export interface GreenhouseUnit {
  id: number
  farmId: number
  farmName: string
  landParcelId?: number | null
  landParcelName?: string
  name: string
  structureType: string
  areaM2: number
  currentCrop?: string
  status: string
  notes?: string
  createdAt: string
  updatedAt: string
}

export interface GreenhouseMetrics {
  units: number
  activeUnits: number
  coveredAreaM2: number
  croppedUnits: number
}

export interface GreenhouseOverview {
  canManage: boolean
  metrics: GreenhouseMetrics
  units: GreenhouseUnit[]
}

export interface GreenhouseUnitRequest {
  farmId: number
  landParcelId?: number | null
  name: string
  structureType?: string
  areaM2: number
  currentCrop?: string
  status?: string
  notes?: string
}

export const greenhousesApi = {
  overview: (organizationId?: number) =>
    api.get<GreenhouseOverview>('/api/greenhouses/overview', {
      params: organizationId ? { organizationId } : {}
    }),

  create: (payload: GreenhouseUnitRequest, organizationId?: number) =>
    api.post<GreenhouseUnit>('/api/greenhouses/units', payload, {
      params: organizationId ? { organizationId } : {}
    }),

  update: (id: number, payload: GreenhouseUnitRequest) =>
    api.put<GreenhouseUnit>(`/api/greenhouses/units/${id}`, payload),

  remove: (id: number) => api.delete(`/api/greenhouses/units/${id}`)
}
