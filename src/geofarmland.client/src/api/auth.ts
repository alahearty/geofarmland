import api from './client'

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  fullName?: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  email: string
  fullName?: string
  roles: string[]
}

export interface UserResponse {
  id: string
  email: string
  fullName?: string
  roles: string[]
}

export const authApi = {
  login: (data: LoginRequest) =>
    api.post<AuthResponse>('/api/auth/login', data),

  register: (data: RegisterRequest) =>
    api.post<{ message: string; email: string }>('/api/auth/register', data),

  refresh: (refreshToken: string) =>
    api.post<AuthResponse>('/api/auth/refresh', { refreshToken } as { refreshToken: string }),

  me: () => api.get<UserResponse>('/api/auth/me')
}
