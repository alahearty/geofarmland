import api from './client'

export interface Plot {
  id: number
  name: string
  coordinates: number[][]
}

export interface SensorReading {
  id: number
  deviceId: string
  latitude: number
  longitude: number
  rainfall: number
  soilMoisture: number
  timestamp: string
}

export interface LiveWeather {
  temperature: string
  feelsLike: string
  humidity: string
  pressure: string
  windSpeed: string
  condition: string
  description: string
  icon: string
  location: string
  fetchedAtUtc: string
}

export interface WeatherHistoryItem {
  id: number
  plotId: number | null
  temperatureC: number
  feelsLikeC: number
  humidity: number
  pressure: number
  windSpeed: number
  condition: string
  description: string
  location: string
  fetchedAtUtc: string
}

export interface ForecastItem {
  timeUtc: string
  temperatureC: number
  feelsLikeC: number
  condition: string
  description: string
}

export const plotsApi = {
  list: () => api.get<Plot[]>('/plots'),

  get: (id: number) => api.get<Plot>(`/plots/${id}`),

  create: (name: string, coordinates: number[][]) =>
    api.post('/plots', { name, coordinates }),

  update: (id: number, name: string, coordinates: number[][]) =>
    api.put(`/plots/${id}`, { name, coordinates }),

  remove: (id: number) => api.delete(`/plots/${id}`)
}

export const monitoringApi = {
  sensors: () => api.get<SensorReading[]>('/iot/sensor-data'),

  ingest: (payload: {
    deviceId: string
    latitude: number
    longitude: number
    rainfall: number
    soilMoisture: number
  }) => api.post('/iot/sensor-data', payload),

  weather: (latitude: number, longitude: number, plotId?: number) =>
    api.get<LiveWeather>('/weather/live', { params: { latitude, longitude, plotId } }),

  weatherHistory: (plotId?: number) =>
    api.get<WeatherHistoryItem[]>('/weather/history', { params: { plotId } }),

  forecast: (latitude: number, longitude: number) =>
    api.get<ForecastItem[]>('/weather/forecast', { params: { latitude, longitude } })
}

