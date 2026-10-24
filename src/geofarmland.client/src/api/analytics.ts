import api from './client'

export interface AnalyticsMetrics {
  landParcels: number
  landHectares: number
  activeCropSeasons: number
  cultivatedHectares: number
  expectedYieldTons: number
  harvestedTons: number
  yieldAttainmentPercent: number
  livestockHead: number
  reportingDevices: number
  readingsLast24Hours: number
  rainfallLast24Hours: number
}

export interface CropPerformance {
  cropName: string
  seasons: number
  areaHectares: number
  expectedYieldTons: number
  harvestedTons: number
  attainmentPercent: number
}

export interface MonthlyHarvest {
  month: string
  quantityTons: number
}

export interface AnalyticsLandUse {
  landUse: string
  parcels: number
  hectares: number
}

export interface AnalyticsSpecies {
  species: string
  groups: number
  headCount: number
}

export interface YieldForecast {
  projectedNextMonthTons: number
  monthsWithHarvests: number
  method: string
}

export interface AnalyticsOverview {
  metrics: AnalyticsMetrics
  cropPerformance: CropPerformance[]
  monthlyHarvests: MonthlyHarvest[]
  landUse: AnalyticsLandUse[]
  species: AnalyticsSpecies[]
  forecast?: YieldForecast | null
}

export const analyticsApi = {
  overview: (organizationId?: number) =>
    api.get<AnalyticsOverview>('/api/analytics/overview', {
      params: organizationId ? { organizationId } : {}
    })
}
