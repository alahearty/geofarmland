import api from './client'

export interface AdminUser {
  id: string
  email: string
  fullName?: string
  roles: string[]
  createdAt: string
}

export interface AdminOverview {
  users: number
  admins: number
  plots: number
  devices: number
  readings: number
  recentUsers: AdminUser[]
}

export const adminApi = {
  overview: () => api.get<AdminOverview>('/api/admin/overview')
}
