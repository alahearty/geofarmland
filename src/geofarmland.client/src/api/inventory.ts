import api from './client'

export interface InventoryItem {
  id: number
  name: string
  itemType: string
  unit: string
  quantityOnHand: number
  reorderLevel: number
  unitCost?: number | null
  stockValue?: number | null
  isLowStock: boolean
  supplier?: string
  notes?: string
  createdAt: string
  updatedAt: string
}

export interface InventoryMetrics {
  items: number
  lowStock: number
  equipment: number
  stockValue: number
}

export interface InventoryOverview {
  canManage: boolean
  metrics: InventoryMetrics
  items: InventoryItem[]
}

export interface InventoryItemRequest {
  name: string
  itemType?: string
  unit: string
  quantityOnHand?: number
  reorderLevel?: number
  unitCost?: number | null
  supplier?: string
  notes?: string
}

export const inventoryApi = {
  overview: (organizationId?: number) =>
    api.get<InventoryOverview>('/api/inventory/overview', {
      params: organizationId ? { organizationId } : {}
    }),

  create: (payload: InventoryItemRequest, organizationId?: number) =>
    api.post<InventoryItem>('/api/inventory/items', payload, {
      params: organizationId ? { organizationId } : {}
    }),

  update: (id: number, payload: InventoryItemRequest) =>
    api.put<InventoryItem>(`/api/inventory/items/${id}`, payload),

  remove: (id: number) => api.delete(`/api/inventory/items/${id}`)
}
