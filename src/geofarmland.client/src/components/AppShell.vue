<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/store/auth'
import { useWorkspaceStore } from '@/store/workspace'
import { navigationGroups } from '@/utils/navigation'
import { initials, roleLabel } from '@/utils/enterprise'
import AppIcon from './AppIcon.vue'

const authStore = useAuthStore()
const workspace = useWorkspaceStore()
const route = useRoute()
const router = useRouter()

const collapsed = ref(localStorage.getItem('geofarmland.sidebar') === 'collapsed')
const mobileOpen = ref(false)
const userMenuOpen = ref(false)
const search = ref('')
const userMenuEl = ref<HTMLElement | null>(null)

const isAdmin = computed(() => authStore.isAdmin)
const user = computed(() => authStore.user)
const displayName = computed(() => user.value?.fullName || user.value?.email || 'Workspace member')
const userInitials = computed(() => initials(displayName.value))
const organizationName = computed(() => workspace.activeOrganization?.name || 'GeoFarmland workspace')
const organizationRole = computed(() => roleLabel(workspace.activeOrganization?.role))
const pageTitle = computed(() => String(route.meta.title || 'Dashboard'))
const pageSection = computed(() => String(route.meta.section || 'Workspace'))
const today = new Intl.DateTimeFormat(undefined, {
  weekday: 'short',
  day: 'numeric',
  month: 'short'
}).format(new Date())

const visibleGroups = computed(() =>
  navigationGroups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => !item.adminOnly || isAdmin.value)
    }))
    .filter((group) => group.items.length > 0)
)

function isActive(path: string) {
  if (path === '/') return route.path === '/'
  if (path.startsWith('/modules/')) return route.path === path
  return route.path === path || route.path.startsWith(`${path}/`)
}

function toggleSidebar() {
  collapsed.value = !collapsed.value
  localStorage.setItem('geofarmland.sidebar', collapsed.value ? 'collapsed' : 'expanded')
}

function toggleMobile() {
  mobileOpen.value = !mobileOpen.value
}

function submitSearch() {
  const term = search.value.trim()
  if (!term) return
  router.push({ name: 'GisExplorer', query: { search: term } })
  search.value = ''
  mobileOpen.value = false
}

async function logout() {
  await authStore.logout()
  workspace.clear()
  router.push('/login')
}

function onDocumentClick(event: MouseEvent) {
  if (userMenuEl.value && !userMenuEl.value.contains(event.target as Node)) {
    userMenuOpen.value = false
  }
}

watch(() => route.fullPath, () => {
  mobileOpen.value = false
  userMenuOpen.value = false
})

onMounted(async () => {
  document.addEventListener('click', onDocumentClick)
  try {
    await workspace.load()
  } catch {
    // The page-level empty/error states retain the failed request context.
  }
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocumentClick)
})
</script>

<template>
  <div class="app-shell" :class="{ 'sidebar-collapsed': collapsed }">
    <div v-if="mobileOpen" class="mobile-scrim" @click="mobileOpen = false"></div>

    <aside class="app-sidebar" :class="{ 'mobile-open': mobileOpen }">
      <div class="brand-row">
        <router-link to="/" class="brand" aria-label="GeoFarmland dashboard">
          <span class="brand-mark">
            <AppIcon name="leaf" :size="23" />
          </span>
          <span class="brand-copy">
            <strong>GeoFarmland</strong>
            <small>Agricultural intelligence</small>
          </span>
        </router-link>
        <button type="button" class="desktop-collapse" :aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'" @click="toggleSidebar">
          <AppIcon name="arrow" :size="16" />
        </button>
        <button type="button" class="mobile-close" aria-label="Close navigation" @click="mobileOpen = false">
          <AppIcon name="close" :size="19" />
        </button>
      </div>

      <nav class="primary-nav" aria-label="Primary navigation">
        <section v-for="group in visibleGroups" :key="group.label" class="nav-group">
          <p class="nav-group-label">{{ group.label }}</p>
          <router-link
            v-for="item in group.items"
            :key="item.path"
            :to="item.path"
            class="nav-item"
            :class="{ active: isActive(item.path) }"
            :title="collapsed ? item.label : undefined"
          >
            <span class="nav-icon"><AppIcon :name="item.icon" :size="19" /></span>
            <span class="nav-label">{{ item.label }}</span>
            <span v-if="isActive(item.path)" class="nav-active-dot"></span>
          </router-link>
        </section>
      </nav>

      <div class="sidebar-footer">
        <router-link to="/field-map" class="utility-link" :title="collapsed ? 'Field map editor' : undefined">
          <span class="nav-icon"><AppIcon name="pin" :size="18" /></span>
          <span class="nav-label">Field map editor</span>
        </router-link>
        <router-link to="/account" class="utility-link" :title="collapsed ? 'Account settings' : undefined">
          <span class="nav-icon"><AppIcon name="user" :size="18" /></span>
          <span class="nav-label">Account settings</span>
        </router-link>

        <div class="workspace-card">
          <span class="workspace-avatar">{{ initials(organizationName) }}</span>
          <span class="workspace-copy">
            <strong>{{ organizationName }}</strong>
            <small>{{ organizationRole }}</small>
          </span>
          <span v-if="workspace.context" class="live-dot" title="Workspace connected"></span>
        </div>
      </div>
    </aside>

    <div class="app-column">
      <header class="topbar">
        <div class="topbar-leading">
          <button type="button" class="icon-button mobile-menu" aria-label="Open navigation" @click="toggleMobile">
            <AppIcon name="menu" :size="20" />
          </button>
          <div class="page-heading">
            <p>{{ pageSection }}</p>
            <h1>{{ pageTitle }}</h1>
          </div>
        </div>

        <form class="topbar-search" role="search" @submit.prevent="submitSearch">
          <AppIcon name="search" :size="17" />
          <input v-model="search" type="search" placeholder="Search land parcels..." aria-label="Search land parcels" />
          <kbd>Enter</kbd>
        </form>

        <div class="topbar-actions">
          <span class="date-pill">
            <AppIcon name="calendar" :size="15" />
            {{ today }}
          </span>
          <span v-if="workspace.context" class="live-status">
            <span class="live-dot"></span>
            Live data
          </span>

          <div ref="userMenuEl" class="user-menu">
            <button type="button" class="user-trigger" :aria-expanded="userMenuOpen" @click.stop="userMenuOpen = !userMenuOpen">
              <span class="user-avatar">{{ userInitials }}</span>
              <span class="user-copy">
                <strong>{{ displayName }}</strong>
                <small>{{ organizationRole }}</small>
              </span>
              <span class="chevron" :class="{ open: userMenuOpen }"><AppIcon name="chevron" :size="14" /></span>
            </button>

            <div v-if="userMenuOpen" class="user-dropdown">
              <div class="dropdown-identity">
                <span class="user-avatar">{{ userInitials }}</span>
                <div>
                  <strong>{{ displayName }}</strong>
                  <small>{{ user?.email }}</small>
                </div>
              </div>
              <router-link to="/account" class="dropdown-item">
                <AppIcon name="user" :size="16" /> Account settings
              </router-link>
              <router-link v-if="isAdmin" to="/admin" class="dropdown-item">
                <AppIcon name="admin" :size="16" /> Platform overview
              </router-link>
              <button type="button" class="dropdown-item danger" @click="logout">
                <AppIcon name="logout" :size="16" /> Sign out
              </button>
            </div>
          </div>
        </div>
      </header>

      <main class="app-main">
        <div class="page-frame">
          <router-view />
        </div>
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-shell {
  --sidebar-width: 264px;
  min-height: 100vh;
  display: flex;
  background: #f2f5f0;
  color: #1b3324;
}

.app-shell.sidebar-collapsed {
  --sidebar-width: 82px;
}

.app-sidebar {
  position: sticky;
  top: 0;
  z-index: 20;
  display: flex;
  flex-direction: column;
  width: var(--sidebar-width);
  height: 100vh;
  flex: 0 0 var(--sidebar-width);
  padding: 1.1rem 0.8rem 0.9rem;
  background:
    radial-gradient(circle at 15% 0%, rgba(103, 169, 119, 0.18), transparent 32%),
    linear-gradient(180deg, #123a27 0%, #0e2d1e 58%, #0a2418 100%);
  color: #e9f4ec;
  overflow: hidden;
  transition: width 0.2s ease, flex-basis 0.2s ease, transform 0.24s ease;
}

.brand-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-height: 46px;
  padding: 0 0.35rem 0.95rem;
  border-bottom: 1px solid rgba(221, 244, 226, 0.12);
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  min-width: 0;
  color: #fff;
  text-decoration: none;
}

.brand-mark {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border: 1px solid rgba(220, 244, 224, 0.2);
  border-radius: 13px;
  background: linear-gradient(145deg, #4c9168, #2c704a);
  color: #f4fff5;
  box-shadow: 0 7px 18px rgba(4, 24, 13, 0.28);
}

.brand-copy {
  display: flex;
  flex-direction: column;
  min-width: 0;
  white-space: nowrap;
}

.brand-copy strong {
  font-size: 1.08rem;
  letter-spacing: -0.02em;
}

.brand-copy small {
  margin-top: 0.08rem;
  color: rgba(226, 244, 230, 0.56);
  font-size: 0.64rem;
  letter-spacing: 0.04em;
}

.desktop-collapse,
.mobile-close {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  margin-left: auto;
  border: 1px solid rgba(220, 244, 224, 0.13);
  border-radius: 8px;
  background: transparent;
  color: rgba(235, 250, 239, 0.68);
  cursor: pointer;
}

.desktop-collapse {
  transform: rotate(180deg);
}

.desktop-collapse:hover,
.mobile-close:hover {
  background: rgba(255, 255, 255, 0.08);
  color: #fff;
}

.mobile-close {
  display: none;
}

.sidebar-collapsed .desktop-collapse {
  transform: rotate(180deg);
}

.primary-nav {
  flex: 1;
  overflow-y: auto;
  padding: 0.5rem 0 0.35rem;
  scrollbar-width: thin;
  scrollbar-color: rgba(214, 241, 221, 0.2) transparent;
}

.nav-group {
  margin-bottom: 0.42rem;
}

.nav-group-label {
  margin: 0 0 0.12rem;
  padding: 0 0.62rem;
  color: rgba(222, 243, 227, 0.42);
  font-size: 0.55rem;
  font-weight: 800;
  letter-spacing: 0.11em;
  text-transform: uppercase;
  white-space: nowrap;
}

.nav-item {
  position: relative;
  display: flex;
  align-items: center;
  gap: 0.6rem;
  min-height: 31px;
  margin: 0.04rem 0;
  padding: 0.32rem 0.6rem;
  border: 1px solid transparent;
  border-radius: 8px;
  color: rgba(228, 245, 232, 0.7);
  font-size: 0.75rem;
  font-weight: 600;
  text-decoration: none;
  white-space: nowrap;
  transition: background 0.15s ease, color 0.15s ease;
}

.nav-item:hover {
  background: rgba(255, 255, 255, 0.07);
  color: #fff;
}

.nav-item.active {
  background: #e5f2e7;
  border-color: rgba(255, 255, 255, 0.3);
  color: #143b27;
  box-shadow: 0 6px 16px rgba(2, 18, 9, 0.16);
}

.nav-icon {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  width: 22px;
  color: rgba(210, 237, 218, 0.72);
}

.nav-item.active .nav-icon {
  color: #216c45;
}

.nav-label {
  overflow: hidden;
  text-overflow: ellipsis;
}

.nav-active-dot {
  width: 5px;
  height: 5px;
  margin-left: auto;
  border-radius: 50%;
  background: #2d8a58;
}

.sidebar-footer {
  padding-top: 0.6rem;
  border-top: 1px solid rgba(221, 244, 226, 0.12);
}

.utility-link {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  min-height: 29px;
  padding: 0.28rem 0.6rem;
  border-radius: 8px;
  color: rgba(222, 242, 227, 0.61);
  font-size: 0.72rem;
  font-weight: 600;
  text-decoration: none;
  white-space: nowrap;
}

.utility-link:hover {
  background: rgba(255, 255, 255, 0.06);
  color: #fff;
}

.workspace-card {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  margin-top: 0.4rem;
  padding: 0.55rem;
  border: 1px solid rgba(222, 244, 227, 0.12);
  border-radius: 11px;
  background: rgba(255, 255, 255, 0.05);
  white-space: nowrap;
}

.workspace-avatar {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  border-radius: 9px;
  background: rgba(207, 238, 215, 0.12);
  color: #d9f0de;
  font-size: 0.68rem;
  font-weight: 800;
}

.workspace-copy {
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}

.workspace-copy strong {
  overflow: hidden;
  color: #edf9ef;
  font-size: 0.7rem;
  text-overflow: ellipsis;
}

.workspace-copy small {
  margin-top: 0.05rem;
  overflow: hidden;
  color: rgba(218, 241, 224, 0.48);
  font-size: 0.6rem;
  text-overflow: ellipsis;
}

.live-dot {
  flex: 0 0 auto;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #62d18b;
  box-shadow: 0 0 0 3px rgba(98, 209, 139, 0.14);
}

.workspace-card .live-dot {
  margin-left: auto;
}

.sidebar-collapsed .brand-copy,
.sidebar-collapsed .nav-label,
.sidebar-collapsed .nav-group-label,
.sidebar-collapsed .workspace-copy,
.sidebar-collapsed .nav-active-dot {
  display: none;
}

.sidebar-collapsed .brand-row {
  justify-content: center;
  padding-inline: 0;
}

.sidebar-collapsed .brand {
  gap: 0;
}

.sidebar-collapsed .desktop-collapse {
  position: absolute;
  top: 4.9rem;
  right: -0.1rem;
  width: 22px;
  height: 22px;
  background: #143a26;
  transform: none;
}

.sidebar-collapsed .nav-item,
.sidebar-collapsed .utility-link {
  justify-content: center;
  padding-inline: 0.4rem;
}

.sidebar-collapsed .workspace-card {
  justify-content: center;
  padding: 0.5rem;
}

.sidebar-collapsed .workspace-card .live-dot {
  display: none;
}

.app-column {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.topbar {
  position: sticky;
  top: 0;
  z-index: 15;
  display: flex;
  align-items: center;
  gap: 1rem;
  min-height: 72px;
  padding: 0.75rem 1.5rem;
  border-bottom: 1px solid #e2e9e1;
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(14px);
}

.topbar-leading {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  min-width: 175px;
}

.page-heading p {
  margin: 0 0 0.12rem;
  color: #7a8b7d;
  font-size: 0.62rem;
  font-weight: 800;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.page-heading h1 {
  margin: 0;
  color: #173321;
  font-size: 1.12rem;
  letter-spacing: -0.025em;
  white-space: nowrap;
}

.topbar-search {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: min(380px, 36vw);
  margin: 0 auto;
  padding: 0.48rem 0.6rem 0.48rem 0.75rem;
  border: 1px solid #dfe7de;
  border-radius: 10px;
  background: #f6f9f5;
  color: #7b8e80;
}

.topbar-search:focus-within {
  border-color: #9ec6a8;
  background: #fff;
  box-shadow: 0 0 0 3px rgba(46, 121, 79, 0.08);
}

.topbar-search input {
  min-width: 0;
  flex: 1;
  border: 0;
  outline: 0;
  background: transparent;
  color: #223d2b;
  font-size: 0.78rem;
}

.topbar-search kbd {
  border: 1px solid #dfe7de;
  border-radius: 5px;
  background: #fff;
  padding: 0.12rem 0.34rem;
  color: #93a096;
  font-size: 0.58rem;
  font-weight: 700;
}

.topbar-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 0;
}

.date-pill,
.live-status {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  white-space: nowrap;
  color: #637a69;
  font-size: 0.7rem;
  font-weight: 600;
}

.live-status {
  padding: 0.32rem 0.55rem;
  border: 1px solid #d8e9dc;
  border-radius: 999px;
  background: #f2faf4;
  color: #33714d;
  font-size: 0.64rem;
  font-weight: 700;
}

.user-menu {
  position: relative;
}

.user-trigger {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  padding: 0.3rem 0.35rem;
  border: 1px solid transparent;
  border-radius: 10px;
  background: transparent;
  cursor: pointer;
  text-align: left;
}

.user-trigger:hover {
  border-color: #dde7de;
  background: #f8faf7;
}

.user-avatar {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  border-radius: 10px;
  background: linear-gradient(145deg, #2e704b, #1a4d32);
  color: #f2fff4;
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.02em;
}

.user-copy {
  display: flex;
  flex-direction: column;
  max-width: 150px;
  overflow: hidden;
}

.user-copy strong {
  overflow: hidden;
  color: #203b29;
  font-size: 0.74rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-copy small {
  margin-top: 0.06rem;
  overflow: hidden;
  color: #849086;
  font-size: 0.62rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chevron {
  display: grid;
  color: #7a8d7f;
  transition: transform 0.15s ease;
}

.chevron.open {
  transform: rotate(180deg);
}

.user-dropdown {
  position: absolute;
  top: calc(100% + 0.55rem);
  right: 0;
  width: 238px;
  padding: 0.45rem;
  border: 1px solid #dfe7de;
  border-radius: 13px;
  background: #fff;
  box-shadow: 0 18px 40px rgba(18, 45, 28, 0.16);
}

.dropdown-identity {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  padding: 0.55rem;
  border-bottom: 1px solid #edf2ed;
  margin-bottom: 0.3rem;
}

.dropdown-identity div {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.dropdown-identity strong {
  overflow: hidden;
  color: #1d3927;
  font-size: 0.74rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dropdown-identity small {
  overflow: hidden;
  color: #88948b;
  font-size: 0.64rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dropdown-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.5rem 0.55rem;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: #3e5646;
  font-size: 0.72rem;
  font-weight: 600;
  text-decoration: none;
  cursor: pointer;
  text-align: left;
}

.dropdown-item:hover {
  background: #f2f8f3;
  color: #1e5436;
}

.dropdown-item.danger {
  color: #9a3d3d;
}

.dropdown-item.danger:hover {
  background: #fdf3f3;
  color: #822727;
}

.icon-button {
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  border: 1px solid #dde7de;
  border-radius: 9px;
  background: #fff;
  color: #45604f;
  cursor: pointer;
}

.icon-button.mobile-menu {
  display: none;
}

.mobile-scrim {
  display: none;
}

.app-main {
  flex: 1;
  min-width: 0;
}

.page-frame {
  width: 100%;
  max-width: 1640px;
  margin: 0 auto;
  padding: 1.35rem 1.5rem 2rem;
}

@media (max-width: 1180px) {
  .date-pill {
    display: none;
  }

  .topbar-search {
    width: min(300px, 30vw);
  }
}

@media (max-width: 960px) {
  .app-shell,
  .app-shell.sidebar-collapsed {
    --sidebar-width: 264px;
  }

  .app-sidebar {
    position: fixed;
    left: 0;
    top: 0;
    transform: translateX(-100%);
    box-shadow: 16px 0 40px rgba(8, 28, 16, 0.23);
  }

  .app-sidebar.mobile-open {
    transform: translateX(0);
  }

  .sidebar-collapsed .brand-copy,
  .sidebar-collapsed .workspace-copy {
    display: flex;
  }

  .sidebar-collapsed .nav-label {
    display: inline;
  }

  .sidebar-collapsed .nav-group-label {
    display: block;
  }

  .sidebar-collapsed .nav-active-dot {
    display: block;
  }

  .sidebar-collapsed .nav-item,
  .sidebar-collapsed .utility-link {
    justify-content: flex-start;
    padding-inline: 0.62rem;
  }

  .sidebar-collapsed .brand-row {
    justify-content: flex-start;
    padding-inline: 0.35rem;
  }

  .sidebar-collapsed .brand {
    gap: 0.7rem;
  }

  .sidebar-collapsed .workspace-card {
    justify-content: flex-start;
    padding: 0.65rem;
  }

  .sidebar-collapsed .workspace-card .live-dot {
    display: block;
  }

  .desktop-collapse {
    display: none;
  }

  .mobile-close,
  .icon-button.mobile-menu {
    display: grid;
  }

  .mobile-scrim {
    display: block;
    position: fixed;
    inset: 0;
    z-index: 19;
    background: rgba(9, 24, 14, 0.45);
    backdrop-filter: blur(2px);
  }

  .topbar-search {
    width: min(280px, 34vw);
  }

  .user-copy {
    display: none;
  }
}

@media (max-width: 720px) {
  .topbar {
    gap: 0.55rem;
    padding: 0.65rem 0.9rem;
  }

  .page-heading p {
    display: none;
  }

  .page-heading h1 {
    font-size: 1rem;
  }

  .topbar-leading {
    min-width: 0;
  }

  .topbar-search {
    flex: 1;
    width: auto;
  }

  .topbar-search kbd,
  .live-status {
    display: none;
  }

  .page-frame {
    padding: 1rem 0.9rem 1.5rem;
  }
}

@media (max-width: 520px) {
  .topbar-search {
    display: none;
  }

  .topbar-actions {
    margin-left: auto;
  }
}
</style>
