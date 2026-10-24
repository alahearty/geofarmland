import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { enterpriseApi, type EnterpriseContext } from '@/api/enterprise'

export const useWorkspaceStore = defineStore('workspace', () => {
  const context = ref<EnterpriseContext | null>(null)
  const loading = ref(false)
  const error = ref('')

  const activeOrganization = computed(() => context.value?.activeOrganization)
  const organizationId = computed(() => activeOrganization.value?.id)
  const canManageFarms = computed(() => context.value?.canManageFarms ?? false)
  const canManageParcels = computed(() => context.value?.canManageParcels ?? false)
  const isPlatformAdmin = computed(() => context.value?.isPlatformAdmin ?? false)

  async function load(force = false) {
    if (context.value && !force) return context.value
    loading.value = true
    error.value = ''
    try {
      const { data } = await enterpriseApi.context()
      context.value = data
      return data
    } catch (err) {
      const candidate = err as { response?: { data?: { message?: string } }; message?: string }
      error.value = candidate.response?.data?.message || candidate.message || 'Could not load the workspace.'
      throw err
    } finally {
      loading.value = false
    }
  }

  function clear() {
    context.value = null
    error.value = ''
  }

  return {
    context,
    loading,
    error,
    activeOrganization,
    organizationId,
    canManageFarms,
    canManageParcels,
    isPlatformAdmin,
    load,
    clear
  }
})
