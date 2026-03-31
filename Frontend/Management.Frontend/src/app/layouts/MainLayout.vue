<template>
  <div class="layout">
    <aside class="sidebar" :class="{ collapsed: sidebarCollapsed }">
      <div class="sidebar-brand">
        <div class="brand-logo">AP</div>
      </div>

      <nav class="sidebar-nav">
        <router-link class="nav-item" to="/dashboard" active-class="active" :title="t('common.dashboard')">
          <span class="nav-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M10 3h4a1 1 0 0 1 1 1v4h-6V4a1 1 0 0 1 1-1Z" stroke="currentColor" stroke-width="1.5" />
              <path d="M4 10h7v10H5a1 1 0 0 1-1-1V10Z" stroke="currentColor" stroke-width="1.5" />
              <path d="M13 10h7v9a1 1 0 0 1-1 1h-6V10Z" stroke="currentColor" stroke-width="1.5" />
            </svg>
          </span>
          <span class="nav-text">{{ t('common.dashboard') }}</span>
        </router-link>

        <div class="nav-section">
          <div class="nav-section-title">{{ t('nav.commission') }}</div>
          <router-link class="nav-item" to="/products" active-class="active" :title="t('nav.productCommission')">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M7 4h10a2 2 0 0 1 2 2v13a1 1 0 0 1-1.6.8L12 16l-5.4 3.8A1 1 0 0 1 5 19V6a2 2 0 0 1 2-2Z" stroke="currentColor" stroke-width="1.5" />
              </svg>
            </span>
            <span class="nav-text">{{ t('nav.productCommission') }}</span>
          </router-link>
        </div>

        <div class="nav-section">
          <div class="nav-section-title">{{ t('nav.settings') }}</div>
          <router-link class="nav-item" to="/settings/user" active-class="active" :title="t('nav.user')">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M4 20a8 8 0 0 1 16 0" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
              </svg>
            </span>
            <span class="nav-text">{{ t('nav.user') }}</span>
          </router-link>
        </div>

        <div class="nav-section">
          <div class="nav-section-title">{{ t('nav.payments') }}</div>
          <router-link class="nav-item" to="/payments/topup" active-class="active" :title="t('nav.topup')">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M12 3v18" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M7 8l5-5 5 5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
                <path d="M7 21h10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
              </svg>
            </span>
            <span class="nav-text">{{ t('nav.topup') }}</span>
          </router-link>
          <router-link class="nav-item" to="/payments/history" active-class="active" :title="t('nav.history')">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M8 8h8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M8 12h8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M8 16h5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
              </svg>
            </span>
            <span class="nav-text">{{ t('nav.history') }}</span>
          </router-link>
        </div>

        <div v-if="isAdmin" class="nav-section">
          <div class="nav-section-title">Quản lý</div>
          <router-link class="nav-item" to="/admin/manage/products" active-class="active" title="Quản lý sản phẩm">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M7 4h10a2 2 0 0 1 2 2v13a1 1 0 0 1-1.6.8L12 16l-5.4 3.8A1 1 0 0 1 5 19V6a2 2 0 0 1 2-2Z" stroke="currentColor" stroke-width="1.5" />
              </svg>
            </span>
            <span class="nav-text">Quản lý sản phẩm</span>
          </router-link>

          <router-link class="nav-item" to="/admin/manage/users" active-class="active" title="Quản lý người dùng">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M4 20a8 8 0 0 1 16 0" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
              </svg>
            </span>
            <span class="nav-text">Quản lý người dùng</span>
          </router-link>

          <router-link class="nav-item" to="/admin/manage/affiliate-links" active-class="active" title="Quản lý link Affiliate">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M10 13a5 5 0 0 1 0-7l.6-.6a5 5 0 0 1 7 7l-1 1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
                <path d="M14 11a5 5 0 0 1 0 7l-.6.6a5 5 0 0 1-7-7l1-1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </span>
            <span class="nav-text">Quản lý link Affiliate</span>
          </router-link>

          <router-link class="nav-item" to="/admin/manage/music" active-class="active" title="Quản lý music">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M9 18a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M17 17a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M11 16V6l8-2v10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </span>
            <span class="nav-text">Quản lý music</span>
          </router-link>

          <router-link class="nav-item" to="/admin/manage/schedules" active-class="active" title="Lịch đã lập">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M7 3v3" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M17 3v3" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M4 7h16" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M6 5h12a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M8 11h4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M8 15h6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
              </svg>
            </span>
            <span class="nav-text">Lịch đã lập</span>
          </router-link>

          <router-link class="nav-item" to="/admin/manage/my-studio" active-class="active" title="My Studio">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M4 7a2 2 0 0 1 2-2h3l2-2h5a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7Z" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" />
                <path d="M10 10l5 3-5 3v-6Z" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" />
              </svg>
            </span>
            <span class="nav-text">My Studio</span>
          </router-link>

          <router-link class="nav-item" to="/admin/manage/ass-subtitles" active-class="active" title="ASS Subtitles">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M8 8h8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M8 12h8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                <path d="M8 16h5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
              </svg>
            </span>
            <span class="nav-text">ASS Subtitles</span>
          </router-link>
        </div>

        <div v-if="isAdmin" class="nav-section">
        
          <div class="nav-section-title">Phân hệ Quản lý nền tảng</div>
          <router-link class="nav-item" to="/platform" active-class="active" title="Dashboard">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M10 3h4a1 1 0 0 1 1 1v4h-6V4a1 1 0 0 1 1-1Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M4 10h7v10H5a1 1 0 0 1-1-1V10Z" stroke="currentColor" stroke-width="1.5" />
                <path d="M13 10h7v9a1 1 0 0 1-1 1h-6V10Z" stroke="currentColor" stroke-width="1.5" />
              </svg>
            </span>
            <span class="nav-text">Dashboard</span>
          </router-link>
          <router-link class="nav-item" to="/admin/youtube" active-class="active" title="YouTube">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M10 15l5-3-5-3v6Z" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" />
                <path d="M3 7.6c0-1 0.7-1.9 1.7-2.1C6.8 5 9.6 4.5 12 4.5s5.2.5 7.3 1c1 .2 1.7 1.1 1.7 2.1v8.8c0 1-.7 1.9-1.7 2.1-2.1.5-4.9 1-7.3 1s-5.2-.5-7.3-1C3.7 18.3 3 17.4 3 16.4V7.6Z" stroke="currentColor" stroke-width="1.5" />
              </svg>
            </span>
            <span class="nav-text">YouTube</span>
          </router-link>
          <router-link class="nav-item" to="/admin/facebook" active-class="active" title="Facebook">
            <span class="nav-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M18 2H6a4 4 0 0 0-4 4v12a4 4 0 0 0 4 4h6V14H9v-2h3v-2a3 3 0 0 1 3-3h2v2h-2a1 1 0 0 0-1 1v2h3l-1 2h-2v8h3a4 4 0 0 0 4-4V6a4 4 0 0 0-4-4Z" stroke="currentColor" stroke-width="1.5" />
              </svg>
            </span>
            <span class="nav-text">Facebook</span>
          </router-link>
        </div>
      </nav>

      <div class="sidebar-bottom">
        <button class="sidebar-toggle" type="button" @click="toggleSidebar" :aria-expanded="!sidebarCollapsed">
          {{ sidebarCollapsed ? '»' : '«' }}
        </button>
      </div>
    </aside>

    <div class="main">
      <header class="topbar">
        <div class="topbar-left"></div>
        <div class="topbar-right">
          <button class="topbar-btn" type="button" @click="toggleLocale">{{ localeLabel }}</button>
          <button class="topbar-btn" type="button">🔔</button>
          <div class="balance" v-if="me">
            <span class="balance-label">{{ t('common.balance') }}</span>
            <span class="balance-value">{{ formatPoints(me.totalPoints) }}</span>
          </div>
          <div class="dropdown-wrap" ref="dropdownWrap">
            <div class="topbar-user" @click="toggleDropdown">
              <div class="avatar">
                <img v-if="me?.avatarUrl" class="avatar-img" :src="me.avatarUrl" alt="avatar" />
                <span v-else class="avatar-text">{{ initials }}</span>
              </div>
              <span class="username">{{ displayName }}</span>
              <span class="caret">▾</span>
            </div>

            <div v-if="dropdownOpen" class="dropdown">
              <button class="dd-item" type="button" @click="goTopUp">{{ t('userMenu.topup') }}</button>
              <button class="dd-item" type="button" @click="goSettings">{{ t('userMenu.settings') }}</button>
              <div class="dd-sep"></div>
              <button class="dd-item danger" type="button" @click="logout">{{ t('userMenu.logout') }}</button>
            </div>
          </div>
        </div>
      </header>

      <main class="content">
        <router-view />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import api from '@/infrastructure/http/apiClient'
import { clearAuthSession, isAdminToken } from '@/shared/auth/jwt'
import { useRouter } from 'vue-router'
import { setSavedLocale, type Locale } from '@/app/i18n'

type MeResponse = {
  userId: string
  username: string
  fullName?: string | null
  email?: string | null
  phone?: string | null
  avatarUrl?: string | null
  totalPoints: number
  level: number
}

const toggleDropdown = () => {
  dropdownOpen.value = !dropdownOpen.value
}

const closeDropdown = () => {
  dropdownOpen.value = false
}

const onDocClick = (e: MouseEvent) => {
  const el = dropdownWrap.value
  if (!el) return
  if (e.target && el.contains(e.target as Node)) return
  closeDropdown()
}

const goTopUp = async () => {
  closeDropdown()
  await router.push('/payments/topup')
}

const goSettings = async () => {
  closeDropdown()
  await router.push('/settings/user')
}

const logout = async () => {
  closeDropdown()
  clearAuthSession()
  await router.push('/login')
}

const me = ref<MeResponse | null>(null)
const dropdownOpen = ref(false)
const dropdownWrap = ref<HTMLElement | null>(null)
const router = useRouter()

const isAdmin = computed(() => {
  const token = localStorage.getItem('token')
  if (!token) return false
  return isAdminToken(token)
})

const displayName = computed(() => {
  if (!me.value) return 'user'
  return (me.value.fullName && me.value.fullName.trim()) ? me.value.fullName : me.value.username
})

const initials = computed(() => {
  const name = displayName.value.trim()
  if (!name) return 'U'
  const parts = name.split(/\s+/).filter(Boolean)
  const first = parts[0]?.[0] ?? 'U'
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : ''
  return (first + last).toUpperCase()
})

const formatPoints = (points: number) => {
  return new Intl.NumberFormat('vi-VN').format(points)
}

const { locale, t } = useI18n()

const SIDEBAR_KEY = 'sidebarCollapsed'
const sidebarCollapsed = ref(localStorage.getItem(SIDEBAR_KEY) === '1')

const toggleSidebar = () => {
  sidebarCollapsed.value = !sidebarCollapsed.value
  localStorage.setItem(SIDEBAR_KEY, sidebarCollapsed.value ? '1' : '0')
}

const localeLabel = computed(() => {
  return locale.value === 'en' ? t('common.en') : t('common.vi')
})

const toggleLocale = () => {
  const next = (locale.value === 'en' ? 'vi' : 'en') as Locale
  locale.value = next
  setSavedLocale(next)
}

const loadMe = async () => {
  try {
    const data = await api.get<MeResponse>('users/me')
    me.value = data
    localStorage.setItem('username', data.username)
    if (data.avatarUrl) {
      localStorage.setItem('avatarUrl', data.avatarUrl)
    } else {
      localStorage.removeItem('avatarUrl')
    }
    localStorage.setItem('totalPoints', String(data.totalPoints ?? 0))
  } catch {
    me.value = null
  }
}

onMounted(() => {
  loadMe()
  document.addEventListener('click', onDocClick)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocClick)
})
</script>

<style scoped>
.layout {
  display: flex;
  height: 100vh;
  background: #f6f6f6;
  overflow: hidden;
}

.sidebar {
  width: 220px;
  background: #fff;
  border-right: 1px solid #eee;
  display: flex;
  flex-direction: column;
  transition: width 0.18s ease;
}

.sidebar.collapsed {
  width: 72px;
}

.sidebar-brand {
  height: 52px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 14px;
  border-bottom: 1px solid #f0f0f0;
}

.brand-logo {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  background: #f26f3b;
  color: #fff;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
}

.sidebar-toggle {
  border: 1px solid #eee;
  background: #fff;
  border-radius: 6px;
  width: 28px;
  height: 28px;
  cursor: pointer;
  color: #666;
  font-size: 14px;
  line-height: 1;
}

.sidebar-toggle:hover {
  background: #fafafa;
}

.sidebar-nav {
  padding: 10px 8px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.sidebar-bottom {
  padding: 10px 8px;
  border-top: 1px solid #f0f0f0;
  display: flex;
  justify-content: center;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 4px;
  color: #333;
  text-decoration: none;
  font-size: 13px;
}

.nav-icon {
  width: 18px;
  height: 18px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: currentColor;
}

.nav-text {
  white-space: nowrap;
}

.sidebar.collapsed .nav-item {
  justify-content: center;
}

.sidebar.collapsed .nav-text {
  display: none;
}

.sidebar.collapsed .nav-section-title {
  display: none;
}

.nav-item:hover {
  background: #f7f7f7;
}

.nav-item.active {
  background: #fff3ee;
  color: #f26f3b;
  font-weight: 600;
}

.nav-section {
  margin-top: 8px;
}

.nav-section-title {
  font-size: 12px;
  color: #999;
  padding: 8px 10px 4px 10px;
}

.main {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
}

.topbar {
  height: 52px;
  background: #fff;
  border-bottom: 1px solid #eee;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
}

.topbar-right {
  display: flex;
  align-items: center;
  gap: 10px;
}

.dropdown-wrap {
  position: relative;
}

.caret {
  font-size: 12px;
  color: #999;
}

.dropdown {
  position: absolute;
  top: 38px;
  right: 0;
  min-width: 180px;
  background: #fff;
  border: 1px solid #eee;
  border-radius: 8px;
  box-shadow: 0 10px 25px rgba(0, 0, 0, 0.08);
  padding: 6px;
  z-index: 20;
}

.dd-item {
  width: 100%;
  text-align: left;
  border: none;
  background: transparent;
  padding: 10px 10px;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  color: #333;
}

.dd-item:hover {
  background: #f7f7f7;
}

.dd-item.danger {
  color: #d1242f;
}

.dd-sep {
  height: 1px;
  background: #f0f0f0;
  margin: 6px 4px;
}

.topbar-btn {
  border: 1px solid #eee;
  background: #fff;
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 12px;
  cursor: pointer;
}

.topbar-btn:hover {
  background: #fafafa;
}

.topbar-user {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid #eee;
  border-radius: 999px;
  padding: 5px 10px 5px 6px;
  cursor: pointer;
}

.balance {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid #eee;
  border-radius: 999px;
  padding: 5px 10px;
}

.balance-label {
  font-size: 12px;
  color: #999;
}

.balance-value {
  font-size: 12px;
  font-weight: 700;
  color: #f26f3b;
}

.avatar {
  width: 26px;
  height: 26px;
  border-radius: 999px;
  background: #f26f3b;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
  overflow: hidden;
}

.avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-text {
  line-height: 1;
}

.username {
  font-size: 12px;
  color: #333;
}

.content {
  padding: 16px;
  min-width: 0;
  overflow: auto;
  flex: 1;
  min-height: 0;
}

@media (max-width: 1024px) {
  .sidebar {
    width: 200px;
  }
}

@media (max-width: 768px) {
  .sidebar {
    display: none;
  }
  .content {
    padding: 12px;
  }
}
</style>
