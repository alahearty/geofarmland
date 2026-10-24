import axios, { type AxiosRequestConfig } from 'axios'

const api = axios.create({
  baseURL: '',
  headers: { 'Content-Type': 'application/json' }
})

interface RefreshResponse {
  accessToken: string
  refreshToken: string
}

let refreshPromise: Promise<RefreshResponse> | null = null

function refreshSession(refreshToken: string): Promise<RefreshResponse> {
  if (!refreshPromise) {
    refreshPromise = axios
      .post<RefreshResponse>('/api/auth/refresh', { refreshToken })
      .then(({ data }) => {
        localStorage.setItem('accessToken', data.accessToken)
        localStorage.setItem('refreshToken', data.refreshToken)
        return data
      })
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined
    if (!originalRequest || error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error)
    }

    // Never attempt a token refresh for authentication endpoints themselves.
    if (String(originalRequest.url || '').includes('/api/auth/')) {
      return Promise.reject(error)
    }

    originalRequest._retry = true
    const refreshToken = localStorage.getItem('refreshToken')
    if (!refreshToken) {
      window.location.href = '/login'
      return Promise.reject(error)
    }

    try {
      const session = await refreshSession(refreshToken)
      originalRequest.headers = {
        ...(originalRequest.headers || {}),
        Authorization: `Bearer ${session.accessToken}`
      }
      return api(originalRequest)
    } catch {
      localStorage.removeItem('accessToken')
      localStorage.removeItem('refreshToken')
      window.location.href = '/login'
      return Promise.reject(error)
    }
  }
)

export default api
