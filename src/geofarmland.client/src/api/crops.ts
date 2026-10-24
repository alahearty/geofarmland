import api from './client'

export interface Crop {
  id: number
  name: string
  category: string
  variety?: string
  description?: string
  isActive: boolean
  seasonCount: number
  createdAt: string
}

export interface CropSeason {
  id: number
  cropId: number
  cropName: string
  variety?: string
  farmId: number
  farmName: string
  landParcelId: number
  landParcelName: string
  seasonName: string
  status: string
  plantingDate: string
  expectedHarvestDate?: string | null
  actualHarvestDate?: string | null
  areaHectares: number
  expectedYieldTons?: number | null
  harvestedTons: number
  notes?: string
  createdAt: string
  updatedAt: string
}

export interface Harvest {
  id: number
  cropSeasonId: number
  cropName: string
  seasonName: string
  farmName: string
  harvestDate: string
  quantityTons: number
  grade?: string
  notes?: string
  createdAt: string
}

export interface CropOverviewMetrics {
  crops: number
  activeSeasons: number
  plannedSeasons: number
  plantedHectares: number
  expectedYieldTons: number
  harvestedTons: number
  upcomingHarvests: number
}

export interface CropOverview {
  canManage: boolean
  metrics: CropOverviewMetrics
  crops: Crop[]
  seasons: CropSeason[]
  recentHarvests: Harvest[]
}

export interface CropRequest {
  name: string
  category?: string
  variety?: string
  description?: string
  isActive?: boolean
}

export interface CropSeasonRequest {
  cropId: number
  farmId: number
  landParcelId: number
  seasonName: string
  status?: string
  plantingDate: string
  expectedHarvestDate?: string | null
  areaHectares?: number
  expectedYieldTons?: number | null
  notes?: string
}

export interface HarvestRequest {
  harvestDate: string
  quantityTons: number
  grade?: string
  notes?: string
}

function withOrganization(organizationId?: number) {
  return organizationId ? { organizationId } : {}
}

export const cropsApi = {
  overview: (organizationId?: number) =>
    api.get<CropOverview>('/api/crops/overview', { params: withOrganization(organizationId) }),

  createCrop: (payload: CropRequest, organizationId?: number) =>
    api.post<Crop>('/api/crops', payload, { params: withOrganization(organizationId) }),

  updateCrop: (id: number, payload: CropRequest) =>
    api.put<Crop>(`/api/crops/${id}`, payload),

  deleteCrop: (id: number) => api.delete(`/api/crops/${id}`),

  createSeason: (payload: CropSeasonRequest, organizationId?: number) =>
    api.post<CropSeason>('/api/crop-seasons', payload, { params: withOrganization(organizationId) }),

  updateSeason: (id: number, payload: CropSeasonRequest) =>
    api.put<CropSeason>(`/api/crop-seasons/${id}`, payload),

  deleteSeason: (id: number) => api.delete(`/api/crop-seasons/${id}`),

  createHarvest: (seasonId: number, payload: HarvestRequest) =>
    api.post<Harvest>(`/api/crop-seasons/${seasonId}/harvests`, payload)
}
