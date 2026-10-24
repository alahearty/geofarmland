<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/store/auth'

const authStore = useAuthStore()
const router = useRouter()
const isAdmin = computed(() => authStore.isAdmin)

async function logout() {
  await authStore.logout()
  router.push('/login')
}
</script>

<template>
  <div class="app-shell">
    <header class="app-header">
      <router-link to="/" class="brand">
        <span class="brand-mark">G</span>
        <span>
          <strong>GeoFarmland</strong>
          <small>Plot mapping and field monitoring</small>
        </span>
      </router-link>

      <nav class="app-nav" aria-label="Main navigation">
        <router-link to="/" class="nav-link">Dashboard</router-link>
        <router-link to="/account" class="nav-link">Account</router-link>
        <router-link v-if="isAdmin" to="/admin" class="nav-link">Admin</router-link>
      </nav>

      <div class="app-user">
        <span class="user-name">{{ authStore.user?.fullName || authStore.user?.email }}</span>
        <span v-if="isAdmin" class="admin-badge">Admin</span>
        <button type="button" class="signout-button" @click="logout">Sign out</button>
      </div>
    </header>

    <main class="app-content">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
  background: #f4f6f4;
  color: #183024;
}

.app-header {
  background: #1a472a;
  color: white;
  padding: 0.9rem 2rem;
  display: flex;
  align-items: center;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  color: white;
  text-decoration: none;
  margin-right: auto;
}

.brand-mark {
  width: 2.1rem;
  height: 2.1rem;
  display: grid;
  place-items: center;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.14);
  border: 1px solid rgba(255, 255, 255, 0.25);
  font-weight: 700;
}

.brand strong {
  display: block;
  font-size: 1.2rem;
  line-height: 1.2;
}

.brand small {
  display: block;
  margin-top: 0.15rem;
  font-size: 0.72rem;
  opacity: 0.72;
}

.app-nav {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.nav-link {
  padding: 0.45rem 0.75rem;
  border-radius: 7px;
  color: rgba(255, 255, 255, 0.76);
  font-size: 0.86rem;
  font-weight: 600;
  text-decoration: none;
}

.nav-link:hover {
  color: white;
  background: rgba(255, 255, 255, 0.1);
}

.nav-link.router-link-exact-active {
  color: #183024;
  background: #eaf4ed;
}

.app-user {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.user-name {
  font-size: 0.86rem;
  opacity: 0.9;
}

.admin-badge {
  background: rgba(255, 255, 255, 0.18);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 999px;
  padding: 0.15rem 0.5rem;
  font-size: 0.7rem;
  font-weight: 600;
}

.signout-button {
  background: rgba(255, 255, 255, 0.13);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.35);
  border-radius: 7px;
  padding: 0.45rem 0.8rem;
  cursor: pointer;
  font-weight: 600;
}

.signout-button:hover {
  background: rgba(255, 255, 255, 0.22);
}

.app-content {
  max-width: 1600px;
  margin: 0 auto;
  padding: 1rem;
}

@media (max-width: 760px) {
  .app-header {
    padding: 0.8rem 1rem;
    gap: 0.8rem;
  }

  .app-nav {
    order: 3;
    width: 100%;
    border-top: 1px solid rgba(255, 255, 255, 0.12);
    padding-top: 0.55rem;
  }

  .app-user {
    margin-left: auto;
  }

  .user-name {
    display: none;
  }
}
</style>
