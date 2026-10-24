import api from './client'

export interface Device {
  id: number
  deviceId: string
  name: string
  plotId: number | null
  plotName: string | null
  createdAt: string
}

export interface DeviceRequest {
  deviceId: string
  name: string
  plotId?: number | null
}

export const devicesApi = {
  list: () => api.get<Device[]>('/devices'),

  create: (payload: DeviceRequest) => api.post<Device>('/devices', payload),

  update: (id: number, payload: DeviceRequest) => api.put<Device>(`/devices/${id}`, payload),

  remove: (id: number) => api.delete(`/devices/${id}`)
}
