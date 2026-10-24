// @vitest-environment jsdom
import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import AdminView from './AdminView.vue'
import { adminApi } from '@/api/admin'

vi.mock('@/api/admin', () => ({
  adminApi: {
    overview: vi.fn(async () => ({
      data: {
        users: 12,
        admins: 1,
        organizations: 2,
        farms: 3,
        landParcels: 7,
        plots: 7,
        devices: 4,
        readings: 22,
        recentUsers: [
          { id: '1', email: 'farmer@geofarmland.com', fullName: 'Demo Farmer', roles: ['User'], createdAt: '2026-10-24T00:00:00Z' }
        ]
      }
    }))
  }
}))

describe('AdminView', () => {
  it('renders platform counts and the user directory', async () => {
    const wrapper = mount(AdminView)
    await flushPromises()

    expect(adminApi.overview).toHaveBeenCalled()
    expect(wrapper.text()).toContain('Admin overview')
    expect(wrapper.text()).toContain('12')
    expect(wrapper.text()).toContain('Demo Farmer')
    expect(wrapper.text()).toContain('farmer@geofarmland.com')
  })
})
