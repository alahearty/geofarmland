import { describe, expect, it } from 'vitest'
import {
  cropStatusClass,
  formatDateOnly,
  formatHectares,
  initials,
  percentage,
  roleLabel,
  statusClass,
  titleCase
} from './enterprise'

describe('enterprise presentation helpers', () => {
  it('formats land area consistently', () => {
    expect(formatHectares(0)).toBe('0.0 ha')
    expect(formatHectares(4.2367)).toBe('4.24 ha')
    expect(formatHectares(1260.4)).toBe('1,260 ha')
  })

  it('converts API enum labels to readable labels', () => {
    expect(titleCase('UnderDevelopment')).toBe('Under Development')
    expect(titleCase('state_lease')).toBe('State Lease')
    expect(roleLabel('OrganizationAdministrator')).toBe('Organization Administrator')
  })

  it('maps parcel statuses to visual classes', () => {
    expect(statusClass('Active')).toBe('status-active')
    expect(statusClass('UnderDevelopment')).toBe('status-development')
    expect(statusClass('Archived')).toBe('status-archived')
    expect(statusClass(undefined)).toBe('status-neutral')
  })

  it('maps crop statuses and date-only values', () => {
    expect(cropStatusClass('Growing')).toBe('crop-growing')
    expect(cropStatusClass('Harvested')).toBe('crop-harvested')
    expect(cropStatusClass(undefined)).toBe('crop-neutral')
    expect(formatDateOnly('2026-01-15')).toContain('2026')
    expect(formatDateOnly(null)).toBe('Not set')
  })

  it('creates initials and bounded percentages', () => {
    expect(initials('Nomsa Dlamini')).toBe('ND')
    expect(initials('GeoFarmland')).toBe('GE')
    expect(percentage(25, 100)).toBe(25)
    expect(percentage(5, 0)).toBe(0)
    expect(percentage(200, 100)).toBe(100)
  })
})
