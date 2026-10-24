import { describe, expect, it } from 'vitest'
import {
  openRing,
  plotCenterFromCoordinates,
  polygonAreaHectares,
  toGeoJsonCoordinates,
  toLeafletCoordinates
} from './plots'

const closedRing = [
  [18.44, -33.93],
  [18.46, -33.93],
  [18.46, -33.94],
  [18.44, -33.94],
  [18.44, -33.93]
]

describe('plot utility functions', () => {
  it('removes a closing ring point without mutating the input', () => {
    const result = openRing(closedRing)
    expect(result).toHaveLength(4)
    expect(closedRing).toHaveLength(5)
  })

  it('calculates a positive hectare area for a closed polygon', () => {
    expect(polygonAreaHectares(closedRing)).toBeGreaterThan(0)
    expect(polygonAreaHectares([])).toBe(0)
  })

  it('calculates the centre from the open ring', () => {
    const [lat, lng] = plotCenterFromCoordinates(closedRing)
    expect(lat).toBeCloseTo(-33.935, 6)
    expect(lng).toBeCloseTo(18.45, 6)
  })

  it('converts GeoJSON lng/lat to Leaflet lat/lng and back', () => {
    const leaflet = toLeafletCoordinates(closedRing)
    expect(leaflet[0]).toEqual([-33.93, 18.44])
    expect(toGeoJsonCoordinates(leaflet)[0]).toEqual([18.44, -33.93])
  })
})
