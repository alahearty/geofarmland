// @vitest-environment jsdom
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AccountView from './AccountView.vue'
import { authApi } from '@/api/auth'

const mocks = vi.hoisted(() => {
  const fetchUser = vi.fn(async () => ({
    id: 'user-1',
    email: 'farmer@geofarmland.com',
    fullName: 'Demo Farmer',
    roles: ['User']
  }))
  return {
    fetchUser,
    store: {
      user: { id: 'user-1', email: 'farmer@geofarmland.com', fullName: 'Demo Farmer', roles: ['User'] },
      fetchUser
    }
  }
})

vi.mock('@/store/auth', () => ({
  useAuthStore: () => mocks.store
}))

vi.mock('@/api/auth', () => ({
  authApi: {
    updateProfile: vi.fn(async () => ({ data: { id: 'user-1', email: 'farmer@geofarmland.com', fullName: 'Updated Farmer', roles: ['User'] } })),
    changePassword: vi.fn(async () => ({ data: { message: 'ok' } }))
  }
}))

describe('AccountView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.fetchUser.mockImplementation(async () => ({
      id: 'user-1',
      email: 'farmer@geofarmland.com',
      fullName: 'Demo Farmer',
      roles: ['User']
    }))
  })

  it('renders the signed-in identity and saves the display name', async () => {
    const wrapper = mount(AccountView)
    await flushPromises()

    expect(wrapper.text()).toContain('Demo Farmer')
    expect(wrapper.text()).toContain('farmer@geofarmland.com')

    const input = wrapper.get('input[autocomplete="name"]')
    await input.setValue('Updated Farmer')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(authApi.updateProfile).toHaveBeenCalledWith('Updated Farmer')
  })
})

