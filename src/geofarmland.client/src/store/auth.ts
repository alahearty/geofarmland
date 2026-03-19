import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi, type AuthResponse } from '@/api/auth'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<{ id: string; email: string; fullName?: string; roles: string[] } | null>(null)
  const accessToken = ref<string | null>(localStorage.getItem('accessToken'))
  const refreshToken = ref<string | null>(localStorage.getItem('refreshToken'))

  const isAuthenticated = computed(() => !!accessToken.value)
  const isAdmin = computed(() => user.value?.roles?.includes('Admin') ?? false)

  function setAuth(data: AuthResponse) {
    accessToken.value = data.accessToken
    refreshToken.value = data.refreshToken
    user.value = {
      id: '',
      email: data.email,
      fullName: data.fullName ?? undefined,
      roles: data.roles ?? []
    }
    localStorage.setItem('accessToken', data.accessToken)
    localStorage.setItem('refreshToken', data.refreshToken)
  }

  function clearAuth() {
    user.value = null
    accessToken.value = null
    refreshToken.value = null
    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
  }

  async function login(email: string, password: string) {
    const { data } = await authApi.login({ email, password })
    setAuth(data)
    return data
  }

  async function register(email: string, password: string, fullName?: string) {
    const { data } = await authApi.register({ email, password, fullName })
    return data
  }

  async function fetchUser() {
    const { data } = await authApi.me()
    user.value = data
    return data
  }

  function logout() {
    clearAuth()
  }

  return {
    user,
    accessToken,
    refreshToken,
    isAuthenticated,
    isAdmin,
    login,
    register,
    fetchUser,
    logout,
    setAuth,
    clearAuth
  }
})
