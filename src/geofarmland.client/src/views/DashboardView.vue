<script setup lang="ts">
import { useAuthStore } from '@/store/auth'
import { useRouter } from 'vue-router'

const authStore = useAuthStore()
const router = useRouter()

function logout() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <div class="dashboard">
    <header class="header">
      <h1>GeoFarmland</h1>
      <div class="user-menu">
        <span class="user-info">
          {{ authStore.user?.fullName || authStore.user?.email }}
          <span v-if="authStore.isAdmin" class="badge">Admin</span>
        </span>
        <button @click="logout" class="btn-logout">Sign out</button>
      </div>
    </header>

    <main class="main">
      <section class="welcome">
        <h2>Welcome to GeoFarmland</h2>
        <p>You're signed in. This is your dashboard.</p>
        <p class="hint">Admin login: admin@geofarmland.com / Admin@123456</p>
      </section>
    </main>
  </div>
</template>

<style scoped>
.dashboard {
  min-height: 100vh;
  background: #f5f5f5;
}

.header {
  background: #1a472a;
  color: white;
  padding: 1rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.header h1 {
  margin: 0;
  font-size: 1.5rem;
}

.user-menu {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.user-info {
  font-size: 0.95rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.badge {
  background: rgba(255, 255, 255, 0.3);
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
}

.btn-logout {
  padding: 0.5rem 1rem;
  background: rgba(255, 255, 255, 0.2);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.4);
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
}

.btn-logout:hover {
  background: rgba(255, 255, 255, 0.3);
}

.main {
  padding: 2rem;
  max-width: 1200px;
  margin: 0 auto;
}

.welcome {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.welcome h2 {
  margin: 0 0 0.5rem;
  color: #1a472a;
}

.welcome p {
  margin: 0.25rem 0;
  color: #555;
}

.hint {
  margin-top: 1rem !important;
  font-size: 0.9rem;
  color: #888;
}
</style>
