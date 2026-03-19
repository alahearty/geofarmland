<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/store/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

async function onSubmit() {
  error.value = ''
  loading.value = true
  try {
    await authStore.login(email.value, password.value)
    const redirect = (route.query.redirect as string) || '/'
    router.push(redirect)
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string; Message?: string } } }
    const data = err?.response?.data
    error.value = (typeof data === 'string' ? data : data?.message ?? data?.Message) ?? 'Login failed'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <div class="auth-card">
      <h1>GeoFarmland</h1>
      <p class="subtitle">Sign in to your account</p>

      <form @submit.prevent="onSubmit" class="auth-form">
        <div v-if="error" class="error">{{ error }}</div>

        <div class="field">
          <label for="email">Email</label>
          <input
            id="email"
            v-model="email"
            type="email"
            placeholder="admin@geofarmland.com"
            required
            autocomplete="email"
          />
        </div>

        <div class="field">
          <label for="password">Password</label>
          <input
            id="password"
            v-model="password"
            type="password"
            placeholder="••••••••"
            required
            autocomplete="current-password"
          />
        </div>

        <button type="submit" :disabled="loading" class="btn-primary">
          {{ loading ? 'Signing in...' : 'Sign in' }}
        </button>
      </form>

      <p class="footer">
        Don't have an account?
        <router-link to="/register">Register</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1a472a 0%, #2d5a3d 50%, #1e3d2b 100%);
}

.auth-card {
  background: rgba(255, 255, 255, 0.98);
  padding: 2.5rem;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  width: 100%;
  max-width: 400px;
}

h1 {
  margin: 0 0 0.25rem;
  font-size: 1.75rem;
  color: #1a472a;
}

.subtitle {
  margin: 0 0 1.5rem;
  color: #666;
  font-size: 0.95rem;
}

.auth-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.error {
  background: #fee;
  color: #c00;
  padding: 0.75rem;
  border-radius: 6px;
  font-size: 0.9rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.field label {
  font-weight: 500;
  font-size: 0.9rem;
  color: #333;
}

.field input {
  padding: 0.75rem 1rem;
  border: 1px solid #ccc;
  border-radius: 6px;
  font-size: 1rem;
}

.field input:focus {
  outline: none;
  border-color: #1a472a;
  box-shadow: 0 0 0 2px rgba(26, 71, 42, 0.2);
}

.btn-primary {
  padding: 0.85rem 1.5rem;
  background: #1a472a;
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-primary:hover:not(:disabled) {
  background: #2d5a3d;
}

.btn-primary:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.footer {
  margin-top: 1.5rem;
  text-align: center;
  font-size: 0.9rem;
  color: #666;
}

.footer a {
  color: #1a472a;
  font-weight: 500;
}
</style>
