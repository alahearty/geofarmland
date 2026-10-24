import api from './client'

export interface IotDevice {
  id: number
  deviceId: string
  name: string
  plotId?: number | null
  plotName?: string
  landParcelId?: number | null
  landParcelName?: string
  farmName?: string
  readingCount: number
  lastReadingAt?: string | null
  lastSoilMoisture?: number | null
  lastRainfall?: number | null
  reportingLast24h: boolean
  createdAt: string
}

export interface IotReading {
  id: number
  deviceId: string
  deviceName?: string
  landParcelName?: string
  farmName?: string
  latitude: number
  longitude: number
  rainfall: number
  soilMoisture: number
  timestamp: string
}

export interface IotMetrics {
  devices: number
  reportingLast24Hours: number
  readingsLast24Hours: number
  averageSoilMoisture?: number | null
  rainfallLast24Hours: number
}

export interface IotOverview {
  canManage: boolean
  metrics: IotMetrics
  devices: IotDevice[]
  recentReadings: IotReading[]
}

export const iotApi = {
  overview: (organizationId?: number) =>
    api.get<IotOverview>('/api/iot/overview', {
      params: organizationId ? { organizationId } : {}
    })
}
