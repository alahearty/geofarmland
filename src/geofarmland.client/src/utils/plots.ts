export type LatLng = [number, number]
export type LngLat = [number, number]

export function toRadians(value: number) {
  return (value * Math.PI) / 180
}

export function polygonAreaHectares(coordinates: number[][]): number {
  const points = coordinates.map(([lng, lat]) => ({ lat, lng }))
  if (points.length < 3) return 0
  const radius = 6378137
  let area = 0
  for (let i = 0; i < points.length; i++) {
    const current = points[i]
    const next = points[(i + 1) % points.length]
    area += toRadians(next.lng - current.lng) *
      (2 + Math.sin(toRadians(current.lat)) + Math.sin(toRadians(next.lat)))
  }
  return Math.abs((area * radius * radius) / 2) / 10000
}

export function openRing(coordinates: number[][]): number[][] {
  const points = coordinates.slice()
  if (points.length > 1) {
    const first = points[0]
    const last = points[points.length - 1]
    if (first[0] === last[0] && first[1] === last[1]) points.pop()
  }
  return points
}

export function plotCenterFromCoordinates(coordinates: number[][]): LatLng {
  const points = openRing(coordinates)
  const total = points.reduce((acc, [lng, lat]) => {
    acc.lng += lng
    acc.lat += lat
    return acc
  }, { lng: 0, lat: 0 })
  const count = Math.max(points.length, 1)
  return [total.lat / count, total.lng / count]
}

export function toLeafletCoordinates(coordinates: number[][]): LatLng[] {
  return coordinates.map(([lng, lat]) => [lat, lng] as LatLng)
}

export function toGeoJsonCoordinates(points: LatLng[]): LngLat[] {
  return points.map(([lat, lng]) => [lng, lat] as LngLat)
}

export function safeArea(coordinates: number[][]): string {
  return polygonAreaHectares(coordinates).toFixed(2)
}
