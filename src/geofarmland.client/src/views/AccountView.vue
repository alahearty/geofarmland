<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/store/auth'

const authStore = useAuthStore()
const loading = ref(true)
const savingProfile = ref(false)
const savingPassword = ref(false)
const profileMessage = ref('')
const profileError = ref('')
const passwordMessage = ref('')
const passwordError = ref('')
const fullName = ref('')
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')

const roles = computed(() => authStore.user?.roles?.length ? authStore.user.roles : ['User'])

async function loadProfile() {
  loading.value = true
  profileError.value = ''
  try {
    const user = authStore.user ?? await authStore.fetchUser()
    fullName.value = user.fullName ?? ''
  } catch (error) {
    profileError.value = errorMessage(error)
  } finally {
    loading.value = false
  }
}

async function saveProfile() {
  if (fullName.value.trim().length < 2) {
    profileError.value = 'Enter a display name of at least two characters.'
    return
  }

  savingProfile.value = true
  profileError.value = ''
  profileMessage.value = ''
  try {
    await authApi.updateProfile(fullName.value.trim())
    await authStore.fetchUser()
    profileMessage.value = 'Profile updated.'
  } catch (error) {
    profileError.value = errorMessage(error)
  } finally {
    savingProfile.value = false
  }
}

async function changePassword() {
  passwordError.value = ''
  passwordMessage.value = ''
  if (!currentPassword.value || !newPassword.value) {
    passwordError.value = 'Enter your current and new password.'
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    passwordError.value = 'The new passwords do not match.'
    return
  }

  savingPassword.value = true
  try {
    await authApi.changePassword(currentPassword.value, newPassword.value)
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
    passwordMessage.value = 'Password changed. Other refresh sessions have been revoked.'
  } catch (error) {
    passwordError.value = errorMessage(error)
  } finally {
    savingPassword.value = false
  }
}

function errorMessage(error: unknown): string {
  const candidate = error as { response?: { data?: { message?: string; detail?: string; title?: string } }; message?: string }
  return candidate.response?.data?.message
    || candidate.response?.data?.detail
    || candidate.response?.data?.title
    || candidate.message
    || 'The request could not be completed.'
}

onMounted(loadProfile)
</script>

<template>
  <div class="page">
    <div class="page-heading">
      <p class="eyebrow">Your workspace</p>
      <h1>Account settings</h1>
      <p>Manage your profile identity and sign-in security.</p>
    </div>

    <div v-if="loading" class="state">Loading account...</div>
    <div v-else class="grid">
      <section class="profile-card">
        <div class="avatar">{{ (fullName || authStore.user?.email || 'U').slice(0, 1).toUpperCase() }}</div>
        <h2>{{ authStore.user?.fullName || authStore.user?.email }}</h2>
        <p>{{ authStore.user?.email }}</p>
        <div class="roles">
          <span v-for="role in roles" :key="role" class="role">{{ role }}</span>
        </div>
        <p class="note">
          Your account is used for plot ownership, devices and sensor readings. Data is isolated to your user unless you are an administrator.
        </p>
      </section>

      <div class="cards">
        <section class="card">
          <h2>Profile</h2>
          <p class="card-subtitle">Update the display name shown in the application.</p>
          <p v-if="profileMessage" class="success">{{ profileMessage }}</p>
          <p v-if="profileError" class="error">{{ profileError }}</p>
          <form @submit.prevent="saveProfile">
            <label class="field">
              <span>Display name</span>
              <input v-model="fullName" maxlength="120" autocomplete="name" />
            </label>
            <label class="field">
              <span>Email</span>
              <input :value="authStore.user?.email" readonly />
            </label>
            <button type="submit" class="btn-primary" :disabled="savingProfile">
              {{ savingProfile ? 'Saving...' : 'Save profile' }}
            </button>
          </form>
        </section>

        <section class="card">
          <h2>Change password</h2>
          <p class="card-subtitle">Changing your password revokes refresh sessions on other devices.</p>
          <p v-if="passwordMessage" class="success">{{ passwordMessage }}</p>
          <p v-if="passwordError" class="error">{{ passwordError }}</p>
          <form @submit.prevent="changePassword">
            <label class="field">
              <span>Current password</span>
              <input v-model="currentPassword" type="password" autocomplete="current-password" />
            </label>
            <label class="field">
              <span>New password</span>
              <input v-model="newPassword" type="password" autocomplete="new-password" />
            </label>
            <label class="field">
              <span>Confirm new password</span>
              <input v-model="confirmPassword" type="password" autocomplete="new-password" />
            </label>
            <button type="submit" class="btn-primary" :disabled="savingPassword">
              {{ savingPassword ? 'Saving...' : 'Update password' }}
            </button>
          </form>
        </section>
      </div>
    </div>
  </div>
</template>

<style scoped>
.page { max-width: 980px; margin: 0 auto; }
.page-heading { margin-bottom: 1.25rem; }
.eyebrow { margin: 0 0 0.3rem; color: #6b7c70; font-size: 0.72rem; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; }
h1 { margin: 0; font-size: 1.6rem; }
.page-heading > p:last-child { margin: 0.35rem 0 0; color: #6b7c70; font-size: 0.9rem; }
.grid { display: grid; grid-template-columns: 300px minmax(0, 1fr); gap: 1rem; align-items: start; }
.profile-card, .card { background: white; border: 1px solid #e2e8e2; border-radius: 10px; padding: 1.2rem; box-shadow: 0 2px 8px rgba(26, 71, 42, 0.06); }
.avatar { display: grid; place-items: center; width: 64px; height: 64px; border-radius: 18px; background: #1a472a; color: white; font-size: 1.6rem; font-weight: 700; }
.profile-card h2 { margin: 0.9rem 0 0.2rem; font-size: 1.1rem; }
.profile-card p { margin: 0; color: #6b7c70; font-size: 0.85rem; overflow-wrap: anywhere; }
.roles { display: flex; flex-wrap: wrap; gap: 0.35rem; margin-top: 0.7rem; }
.role { border-radius: 999px; background: #edf6ef; color: #1a6b3c; padding: 0.2rem 0.55rem; font-size: 0.7rem; font-weight: 700; }
.note { margin-top: 0.9rem !important; padding-top: 0.8rem; border-top: 1px solid #eef2ee; line-height: 1.5; }
.cards { display: flex; flex-direction: column; gap: 1rem; }
.card h2 { margin: 0; font-size: 1.05rem; }
.card-subtitle { margin: 0.25rem 0 1rem; color: #6b7c70; font-size: 0.82rem; }
.field { display: flex; flex-direction: column; gap: 0.3rem; margin-bottom: 0.7rem; font-size: 0.82rem; font-weight: 600; }
.field input { border: 1px solid #cdd8cf; border-radius: 7px; padding: 0.55rem 0.65rem; font-size: 0.9rem; font-weight: 400; }
.field input[readonly] { background: #f6f8f6; color: #6b7c70; }
.btn-primary { border: 0; border-radius: 7px; background: #1a472a; color: white; padding: 0.55rem 0.9rem; font-weight: 700; cursor: pointer; }
.btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
.success, .error { border-radius: 7px; padding: 0.55rem 0.7rem; font-size: 0.82rem; }
.success { background: #e7f6ec; color: #1a6b3c; }
.error { background: #fdecec; color: #9b1c1c; }
.state { color: #6b7c70; }
@media (max-width: 820px) { .grid { grid-template-columns: 1fr; } }
</style>
