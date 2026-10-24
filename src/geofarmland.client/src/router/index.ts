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
          component: () => import('@/views/DashboardView.vue')
        },
        {
          path: 'account',
          name: 'Account',
          component: () => import('@/views/AccountView.vue')
        },
        {
          path: 'admin',
          name: 'Admin',
          component: () => import('@/views/AdminView.vue'),
          meta: { admin: true }
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
