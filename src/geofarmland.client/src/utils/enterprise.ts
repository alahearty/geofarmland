export function formatHectares(value: number | string | null | undefined): string {
  const numeric = Number(value ?? 0)
  if (!Number.isFinite(numeric)) return '0 ha'
  const options: Intl.NumberFormatOptions = numeric >= 1000
    ? { maximumFractionDigits: 0 }
    : numeric > 0 && numeric < 10
      ? { minimumFractionDigits: 2, maximumFractionDigits: 2 }
      : { minimumFractionDigits: 1, maximumFractionDigits: 2 }
  const formatted = new Intl.NumberFormat(undefined, options).format(numeric)
  return `${formatted} ha`
}

export function formatNumber(value: number | null | undefined): string {
  return new Intl.NumberFormat(undefined).format(Number(value ?? 0))
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return 'No data'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return 'No data'
  return parsed.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return 'No data'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return 'No data'
  return parsed.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

export function formatDateOnly(value: string | null | undefined): string {
  if (!value) return 'Not set'
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value)
  if (!match) return formatDate(value)
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
  if (Number.isNaN(date.getTime())) return 'Not set'
  return date.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

export function titleCase(value: string | null | undefined): string {
  if (!value) return ''
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .replace(/\b\w/g, (letter) => letter.toUpperCase())
}

export function roleLabel(value: string | null | undefined): string {
  return titleCase(value) || 'Member'
}

export function statusClass(status: string | null | undefined): string {
  switch ((status ?? '').toLowerCase()) {
    case 'active':
      return 'status-active'
    case 'fallow':
      return 'status-fallow'
    case 'underdevelopment':
      return 'status-development'
    case 'archived':
      return 'status-archived'
    default:
      return 'status-neutral'
  }
}

export function cropStatusClass(status: string | null | undefined): string {
  switch ((status ?? '').toLowerCase()) {
    case 'planned':
      return 'crop-planned'
    case 'planted':
      return 'crop-planted'
    case 'growing':
      return 'crop-growing'
    case 'harvested':
      return 'crop-harvested'
    case 'failed':
      return 'crop-failed'
    default:
      return 'crop-neutral'
  }
}

export function initials(value: string | null | undefined): string {
  const source = (value ?? '').trim()
  if (!source) return 'GF'
  const parts = source.split(/\s+/).filter(Boolean)
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase()
}

export function greeting(date = new Date()): string {
  const hour = date.getHours()
  if (hour < 12) return 'Good morning'
  if (hour < 17) return 'Good afternoon'
  return 'Good evening'
}

export function percentage(part: number, total: number): number {
  if (!total) return 0
  return Math.max(0, Math.min(100, Math.round((part / total) * 100)))
}
