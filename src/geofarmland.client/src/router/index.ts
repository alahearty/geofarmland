import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/store/auth'
import AppShell from '@/components/AppShell.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/LoginView.vue'),
      meta: { guest: true }
    },
    {
      path: '/register',
      name: 'Register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { guest: true }
    },
    {
      path: '/',
      component: AppShell,
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          name: 'Home',
          component: () => import('@/views/EnterpriseDashboardView.vue'),
          meta: { title: 'Operations dashboard', section: 'Enterprise overview' }
        },
        {
          path: 'gis',
          name: 'GisExplorer',
          component: () => import('@/views/GisExplorerView.vue'),
          meta: { title: 'GIS explorer', section: 'Spatial intelligence' }
        },
        {
          path: 'farms',
          name: 'Farms',
          component: () => import('@/views/FarmsView.vue'),
          meta: { title: 'Farms & land parcels', section: 'Land register' }
        },
        {
          path: 'field-map',
          name: 'FieldMap',
          component: () => import('@/views/DashboardView.vue'),
          meta: { title: 'Field map editor', section: 'Field operations' }
        },
        {
          path: 'modules/crops',
          name: 'Crops',
          component: () => import('@/views/CropsView.vue'),
          meta: { title: 'Crops & planting', section: 'Production intelligence' }
        },
        {
          path: 'modules/livestock',
          name: 'Livestock',
          component: () => import('@/views/LivestockView.vue'),
          meta: { title: 'Livestock & pasture', section: 'Animal production' }
        },
        {
          path: 'modules/aquaculture',
          name: 'Aquaculture',
          component: () => import('@/views/AquacultureView.vue'),
          meta: { title: 'Aquaculture', section: 'Aquatic production' }
        },
        {
          path: 'modules/greenhouses',
          name: 'Greenhouses',
          component: () => import('@/views/GreenhousesView.vue'),
          meta: { title: 'Greenhouses & plantations', section: 'Protected production' }
        },
        {
          path: 'modules/iot',
          name: 'IotDevices',
          component: () => import('@/views/IotDevicesView.vue'),
          meta: { title: 'IoT devices', section: 'Field telemetry' }
        },
        {
          path: 'modules/remote-sensing',
          name: 'RemoteSensing',
          component: () => import('@/views/RemoteSensingView.vue'),
          meta: { title: 'Remote sensing', section: 'Earth observation' }
        },
        {
          path: 'modules/analytics',
          name: 'Analytics',
          component: () => import('@/views/AnalyticsView.vue'),
          meta: { title: 'Analytics & forecasting', section: 'Decision support' }
        },
        {
          path: 'modules/operations',
          name: 'Operations',
          component: () => import('@/views/OperationsView.vue'),
          meta: { title: 'Field operations', section: 'Operational execution' }
        },
        {
          path: 'modules/weather',
          name: 'Weather',
          component: () => import('@/views/WeatherView.vue'),
          meta: { title: 'Weather monitoring', section: 'Environmental monitoring' }
        },
        {
          path: 'modules/soil',
          name: 'Soil',
          component: () => import('@/views/SoilView.vue'),
          meta: { title: 'Soil & irrigation', section: 'Soil and water' }
        },
        {
          path: 'modules/inventory',
          name: 'Inventory',
          component: () => import('@/views/InventoryView.vue'),
          meta: { title: 'Inventory & equipment', section: 'Supply control' }
        },
        {
          path: 'modules/finance',
          name: 'Finance',
          component: () => import('@/views/FinanceView.vue'),
          meta: { title: 'Finance & reports', section: 'Enterprise performance' }
        },
        {
          path: 'modules/:module',
          name: 'Module',
          component: () => import('@/views/ModuleView.vue'),
          meta: { title: 'Module delivery plan', section: 'Enterprise roadmap' }
        },
        {
          path: 'account',
          name: 'Account',
          component: () => import('@/views/AccountView.vue'),
          meta: { title: 'Account settings', section: 'Your workspace' }
        },
        {
          path: 'admin',
          name: 'Admin',
          component: () => import('@/views/AdminView.vue'),
          meta: { admin: true, title: 'Platform administration', section: 'Administration' }
        }
      ]
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/'
    }
  ]
})

router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore()
  const token = localStorage.getItem('accessToken')

  if (to.meta.requiresAuth && !token) {
    next({ name: 'Login', query: { redirect: to.fullPath } })
    return
  }

  if (to.meta.guest && token) {
    next({ name: 'Home' })
    return
  }

  if (token && !authStore.user && to.meta.requiresAuth) {
    try {
      await authStore.fetchUser()
    } catch {
      authStore.clearAuth()
      next({ name: 'Login' })
      return
    }
  }

  if (to.meta.admin && !authStore.isAdmin) {
    next({ name: 'Home' })
    return
  }

  next()
})

export default router
