import api from './client'

export interface EnterpriseOrganization {
  id: number
  name: string
  slug: string
  country: string
  currencyCode: string
  timeZone: string
  role: string
}

export interface EnterpriseContext {
  userId: string
  email: string
  fullName?: string
  isPlatformAdmin: boolean
  activeOrganization?: EnterpriseOrganization
  organizations: EnterpriseOrganization[]
  canManageFarms: boolean
  canManageParcels: boolean
}

export interface EnterpriseMetrics {
  farms: number
  parcels: number
  hectares: number
  devices: number
  readingsToday: number
  weatherObservations: number
}

export interface FarmOverview {
  id: number
  name: string
  code?: string
  region?: string
  parcelCount: number
  hectares: number
}

export interface LandUseBreakdown {
  landUse: string
  parcels: number
  hectares: number
}

export interface RecentParcel {
  id: number
  name: string
  code?: string
  farmName: string
  landUse: string
  status: string
  hectares: number
  coordinates: number[][]
}

export interface RecentReading {
  id: number
  deviceId: string
  soilMoisture: number
  rainfall: number
  timestamp: string
}

export interface WeatherOverview {
  temperatureC: number
  feelsLikeC: number
  humidity: number
  windSpeed: number
  condition: string
  description: string
  location: string
  fetchedAtUtc: string
}

export interface EnterpriseOverview {
  organization: EnterpriseOrganization
  canManageFarms: boolean
  canManageParcels: boolean
  metrics: EnterpriseMetrics
  farms: FarmOverview[]
  landUse: LandUseBreakdown[]
  recentParcels: RecentParcel[]
  recentReadings: RecentReading[]
  latestWeather?: WeatherOverview
}

export interface Farm {
  id: number
  organizationId: number
  name: string
  code?: string
  region?: string
  country?: string
  description?: string
  parcelCount: number
  hectares: number
  createdAt: string
  updatedAt: string
}

export interface LandParcel {
  id: number
  organizationId: number
  farmId: number
  farmName: string
  name: string
  code?: string
  landUse: string
  tenureType: string
  status: string
  areaHectares: number
  notes?: string
  coordinates: number[][]
  createdAt: string
  updatedAt: string
}

export interface LandParcelList {
  items: LandParcel[]
  total: number
  page: number
  pageSize: number
}

export interface FarmRequest {
  name: string
  code?: string
  region?: string
  country?: string
  description?: string
}

export interface LandParcelRequest {
  farmId: number
  name: string
  code?: string
  landUse?: string
  tenureType?: string
  status?: string
  notes?: string
  coordinates?: number[][]
}

export interface ParcelFilters {
  farmId?: number
  search?: string
  status?: string
  landUse?: string
  page?: number
  pageSize?: number
}

function withOrganization(params: Record<string, unknown>, organizationId?: number) {
  return organizationId ? { ...params, organizationId } : params
}

export const enterpriseApi = {
  context: (organizationId?: number) =>
    api.get<EnterpriseContext>('/api/enterprise/context', {
      params: withOrganization({}, organizationId)
    }),

  overview: (organizationId?: number) =>
    api.get<EnterpriseOverview>('/api/enterprise/overview', {
      params: withOrganization({}, organizationId)
    }),

  farms: (organizationId?: number) =>
    api.get<Farm[]>('/api/farms', {
      params: withOrganization({}, organizationId)
    }),

  createFarm: (payload: FarmRequest, organizationId?: number) =>
    api.post<Farm>('/api/farms', payload, {
      params: withOrganization({}, organizationId)
    }),

  updateFarm: (id: number, payload: FarmRequest) =>
    api.put<Farm>(`/api/farms/${id}`, payload),

  deleteFarm: (id: number) => api.delete(`/api/farms/${id}`),

  parcels: (filters: ParcelFilters = {}, organizationId?: number) =>
    api.get<LandParcelList>('/api/land-parcels', {
      params: withOrganization({
        farmId: filters.farmId,
        search: filters.search || undefined,
        status: filters.status || undefined,
        landUse: filters.landUse || undefined,
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 100
      }, organizationId)
    }),

  createParcel: (payload: LandParcelRequest, organizationId?: number) =>
    api.post<LandParcel>('/api/land-parcels', payload, {
      params: withOrganization({}, organizationId)
    }),

  updateParcel: (id: number, payload: LandParcelRequest) =>
    api.put<LandParcel>(`/api/land-parcels/${id}`, payload),

  deleteParcel: (id: number) => api.delete(`/api/land-parcels/${id}`)
}
