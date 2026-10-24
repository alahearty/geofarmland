import api from './client'

export interface SoilSample {
  id: number
  farmId: number
  farmName: string
  landParcelId: number
  landParcelName: string
  sampleDate: string
  ph: number
  organicMatterPct: number
  nitrogenPpm?: number | null
  phosphorusPpm?: number | null
  potassiumPpm?: number | null
  laboratory?: string
  notes?: string
  createdAt: string
}

export interface IrrigationEvent {
  id: number
  farmId: number
  farmName: string
  landParcelId: number
  landParcelName: string
  eventDate: string
  volumeM3: number
  durationMinutes?: number | null
  source: string
  notes?: string
  createdAt: string
}

export interface SoilMetrics {
  samples: number
  averagePH?: number | null
  averageOrganicMatterPct?: number | null
  irrigationEventsLast30Days: number
  waterVolumeLast30DaysM3: number
}

export interface SoilOverview {
  canManage: boolean
  metrics: SoilMetrics
  samples: SoilSample[]
  irrigationEvents: IrrigationEvent[]
}

export interface SoilSampleRequest {
  farmId: number
  landParcelId: number
  sampleDate: string
  ph: number
  organicMatterPct: number
  nitrogenPpm?: number | null
  phosphorusPpm?: number | null
  potassiumPpm?: number | null
  laboratory?: string
  notes?: string
}

export interface IrrigationEventRequest {
  farmId: number
  landParcelId: number
  eventDate: string
  volumeM3: number
  durationMinutes?: number | null
  source?: string
  notes?: string
}

function withOrganization(organizationId?: number) {
  return organizationId ? { organizationId } : {}
}

export const soilApi = {
  overview: (organizationId?: number) =>
    api.get<SoilOverview>('/api/soil/overview', { params: withOrganization(organizationId) }),

  createSample: (payload: SoilSampleRequest, organizationId?: number) =>
    api.post<SoilSample>('/api/soil/samples', payload, { params: withOrganization(organizationId) }),

  updateSample: (id: number, payload: SoilSampleRequest) =>
    api.put<SoilSample>(`/api/soil/samples/${id}`, payload),

  deleteSample: (id: number) => api.delete(`/api/soil/samples/${id}`),

  createIrrigation: (payload: IrrigationEventRequest, organizationId?: number) =>
    api.post<IrrigationEvent>('/api/soil/irrigation', payload, { params: withOrganization(organizationId) }),

  updateIrrigation: (id: number, payload: IrrigationEventRequest) =>
    api.put<IrrigationEvent>(`/api/soil/irrigation/${id}`, payload),

  deleteIrrigation: (id: number) => api.delete(`/api/soil/irrigation/${id}`)
}
