export interface NavigationItem {
  label: string
  path: string
  icon: string
  adminOnly?: boolean
}

export interface NavigationGroup {
  label: string
  items: NavigationItem[]
}

export interface ModuleDefinition {
  slug: string
  title: string
  icon: string
  phase: string
  description: string
  capabilities: string[]
}

export const navigationGroups: NavigationGroup[] = [
  {
    label: 'Overview',
    items: [
      { label: 'Dashboard', path: '/', icon: 'dashboard' },
      { label: 'GIS Explorer', path: '/gis', icon: 'map' }
    ]
  },
  {
    label: 'Land & production',
    items: [
      { label: 'Farms & Land Parcels', path: '/farms', icon: 'farm' },
      { label: 'Crops & Planting', path: '/modules/crops', icon: 'crop' },
      { label: 'Livestock & Pasture', path: '/modules/livestock', icon: 'livestock' },
      { label: 'Aquaculture', path: '/modules/aquaculture', icon: 'aquaculture' },
      { label: 'Greenhouses & Plantations', path: '/modules/greenhouses', icon: 'greenhouse' },
      { label: 'Soil & Irrigation', path: '/modules/soil', icon: 'soil' }
    ]
  },
  {
    label: 'Monitoring',
    items: [
      { label: 'Weather Monitoring', path: '/modules/weather', icon: 'weather' },
      { label: 'IoT Devices', path: '/modules/iot', icon: 'device' },
      { label: 'Remote Sensing', path: '/modules/remote-sensing', icon: 'satellite' }
    ]
  },
  {
    label: 'Intelligence',
    items: [
      { label: 'Analytics & Forecasting', path: '/modules/analytics', icon: 'analytics' }
    ]
  },
  {
    label: 'Operations',
    items: [
      { label: 'Field Operations', path: '/modules/operations', icon: 'tasks' },
      { label: 'Inventory & Equipment', path: '/modules/inventory', icon: 'inventory' },
      { label: 'Finance & Reports', path: '/modules/finance', icon: 'finance' }
    ]
  },
  {
    label: 'Administration',
    items: [
      { label: 'Administration', path: '/admin', icon: 'admin', adminOnly: true }
    ]
  }
]

export const moduleCatalog: Record<string, ModuleDefinition> = {
  crops: {
    slug: 'crops',
    title: 'Crops & Planting',
    icon: 'crop',
    phase: 'Phase 2',
    description: 'Plan crop cycles against the land parcel register, then track planting, inputs and harvests.',
    capabilities: [
      'Crop catalogue, varieties and growing cycles',
      'Season and planting records linked to land parcels',
      'Harvest, input application and yield records',
      'Crop status and expected harvest timelines'
    ]
  },
  livestock: {
    slug: 'livestock',
    title: 'Livestock & Pasture',
    icon: 'livestock',
    phase: 'Phase 2',
    description: 'Manage herds, grazing rotations, animal health events and pasture carrying capacity.',
    capabilities: [
      'Livestock groups, tags and breeding records',
      'Grazing paddocks and rotation planning',
      'Health, vaccination and movement events',
      'Stocking-rate and pasture utilisation metrics'
    ]
  },
  aquaculture: {
    slug: 'aquaculture',
    title: 'Aquaculture',
    icon: 'aquaculture',
    phase: 'Phase 2',
    description: 'Track fishponds, stocking events, water quality and harvest readiness.',
    capabilities: [
      'Pond and cage registration with spatial context',
      'Stocking, feed and mortality records',
      'Water-quality readings and thresholds',
      'Harvest and batch traceability'
    ]
  },
  greenhouses: {
    slug: 'greenhouses',
    title: 'Greenhouses & Plantations',
    icon: 'greenhouse',
    phase: 'Phase 2',
    description: 'Manage protected growing environments and long-cycle plantation blocks.',
    capabilities: [
      'Greenhouse units and internal bay layout',
      'Climate and irrigation setpoints',
      'Plantation block cycles and tree inventories',
      'Production and environmental observations'
    ]
  },
  soil: {
    slug: 'soil',
    title: 'Soil & Irrigation',
    icon: 'soil',
    phase: 'Phase 3',
    description: 'Store soil profiles, sample results and irrigation events against each land parcel.',
    capabilities: [
      'Soil sample results and laboratory metadata',
      'Irrigation source, schedule and application events',
      'Moisture and salinity thresholds',
      'Soil health and irrigation history'
    ]
  },
  weather: {
    slug: 'weather',
    title: 'Weather Monitoring',
    icon: 'weather',
    phase: 'Phase 3',
    description: 'Bring provider observations, forecasts and station data into one operational timeline.',
    capabilities: [
      'Provider-backed current conditions and forecasts',
      'Weather observation history by farm',
      'Rainfall and temperature trend summaries',
      'Weather-triggered advisory rules'
    ]
  },
  iot: {
    slug: 'iot',
    title: 'IoT Devices',
    icon: 'device',
    phase: 'Phase 3',
    description: 'Operate field devices, monitor readings and route authenticated telemetry to the right farm.',
    capabilities: [
      'Device registry with farm and parcel assignment',
      'Authenticated telemetry ingestion',
      'Latest reading, heartbeat and battery state',
      'Reading history and threshold alerts'
    ]
  },
  'remote-sensing': {
    slug: 'remote-sensing',
    title: 'Remote Sensing',
    icon: 'satellite',
    phase: 'Phase 4',
    description: 'Connect satellite imagery and vegetation indices to parcel boundaries for change detection.',
    capabilities: [
      'Imagery scenes clipped to parcel geometry',
      'Vegetation and moisture index timelines',
      'Change detection and anomaly flags',
      'Field inspection follow-up from imagery'
    ]
  },
  analytics: {
    slug: 'analytics',
    title: 'Analytics & Forecasting',
    icon: 'analytics',
    phase: 'Phase 4',
    description: 'Turn operational records into yield, cost, water and production intelligence.',
    capabilities: [
      'Yield and production trend analysis',
      'Input, water and cost benchmarking',
      'Forecast scenarios by farm and crop',
      'Exportable management reports'
    ]
  },
  operations: {
    slug: 'operations',
    title: 'Field Operations',
    icon: 'tasks',
    phase: 'Phase 3',
    description: 'Plan inspections, assign work, capture evidence and close operational tasks.',
    capabilities: [
      'Field inspections tied to parcels and crops',
      'Work orders, ownership and due dates',
      'Evidence attachments and field notes',
      'Operational completion and exception tracking'
    ]
  },
  inventory: {
    slug: 'inventory',
    title: 'Inventory & Equipment',
    icon: 'inventory',
    phase: 'Phase 4',
    description: 'Control stock, inputs, machinery, maintenance and usage across the organisation.',
    capabilities: [
      'Stock items, units and reorder thresholds',
      'Input movement and issue records',
      'Equipment register and maintenance schedule',
      'Usage and cost allocation to farms'
    ]
  },
  finance: {
    slug: 'finance',
    title: 'Finance & Reports',
    icon: 'finance',
    phase: 'Phase 4',
    description: 'Capture farm expenses and revenue, then report enterprise performance by farm or parcel.',
    capabilities: [
      'Expense and revenue records with categories',
      'Farm and parcel cost allocation',
      'Budget versus actual reporting',
      'Management report exports'
    ]
  }
}

export function findModule(slug: string | string[] | undefined): ModuleDefinition | undefined {
  const key = Array.isArray(slug) ? slug[0] : slug
  return key ? moduleCatalog[key] : undefined
}
