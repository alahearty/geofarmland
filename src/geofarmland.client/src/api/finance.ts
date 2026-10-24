import api from './client'

export interface FinancialRecord {
  id: number
  farmId?: number | null
  farmName?: string
  recordType: string
  category: string
  amount: number
  recordDate: string
  description?: string
  createdAt: string
}

export interface FinanceMetrics {
  revenue: number
  expenses: number
  netPosition: number
  records: number
}

export interface FinanceOverview {
  canManage: boolean
  metrics: FinanceMetrics
  records: FinancialRecord[]
}

export interface FinancialRecordRequest {
  farmId?: number | null
  recordType?: string
  category: string
  amount: number
  recordDate: string
  description?: string
}

export const financeApi = {
  overview: (organizationId?: number) =>
    api.get<FinanceOverview>('/api/finance/overview', {
      params: organizationId ? { organizationId } : {}
    }),

  create: (payload: FinancialRecordRequest, organizationId?: number) =>
    api.post<FinancialRecord>('/api/finance/records', payload, {
      params: organizationId ? { organizationId } : {}
    }),

  update: (id: number, payload: FinancialRecordRequest) =>
    api.put<FinancialRecord>(`/api/finance/records/${id}`, payload),

  remove: (id: number) => api.delete(`/api/finance/records/${id}`)
}
