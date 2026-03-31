<template>
  <div class="settings-page">
    <div class="page-header">
      <div class="breadcrumb">
        <span>Trang chủ</span>
        <span class="sep">/</span>
        <span>Thiết lập</span>
        <span class="sep">/</span>
        <span class="current">Người dùng</span>
      </div>

      <div class="header-row">
        <h1 class="page-title">Thiết lập Người dùng</h1>
      </div>
    </div>

    <div class="grid">
      <div class="card">
        <div class="card-title">Thông tin cá nhân</div>

        <div v-if="loading" class="muted">Đang tải...</div>

        <div v-else class="form">
          <div class="avatar-row">
            <div class="avatar-preview">
              <img v-if="form.avatarUrl" :src="form.avatarUrl" alt="avatar" />
              <div v-else class="avatar-placeholder">{{ initials }}</div>
            </div>
            <div class="field" style="flex: 1">
              <label>Avatar URL</label>
              <input type="text" v-model="form.avatarUrl" placeholder="https://..." />
            </div>
          </div>

          <div class="field">
            <label>Username</label>
            <input type="text" :value="form.username" disabled />
          </div>

          <div class="field">
            <label>Họ và tên</label>
            <input type="text" v-model="form.fullName" placeholder="Nhập họ và tên" />
          </div>

          <div class="field">
            <label>Email</label>
            <input type="email" v-model="form.email" placeholder="Nhập email" />
          </div>

          <div class="field">
            <label>Số điện thoại</label>
            <input type="text" v-model="form.phone" placeholder="Nhập số điện thoại" />
          </div>

          <div class="actions">
            <button class="btn" :disabled="saving" @click="saveProfile">
              {{ saving ? 'Đang lưu...' : 'Lưu thay đổi' }}
            </button>
            <div v-if="profileMessage" :class="['msg', profileMessageType]">{{ profileMessage }}</div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-title">Đổi mật khẩu</div>

        <div class="form">
          <div class="field">
            <label>Mật khẩu hiện tại</label>
            <input type="password" v-model="pwd.currentPassword" placeholder="Nhập mật khẩu hiện tại" />
          </div>

          <div class="field">
            <label>Mật khẩu mới</label>
            <input type="password" v-model="pwd.newPassword" placeholder="Nhập mật khẩu mới" />
          </div>

          <div class="field">
            <label>Xác nhận mật khẩu mới</label>
            <input type="password" v-model="pwd.confirmPassword" placeholder="Nhập lại mật khẩu mới" />
          </div>

          <div class="actions">
            <button class="btn" :disabled="changing" @click="changePassword">
              {{ changing ? 'Đang đổi...' : 'Đổi mật khẩu' }}
            </button>
            <div v-if="pwdMessage" :class="['msg', pwdMessageType]">{{ pwdMessage }}</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import apiClient from '@/infrastructure/apiClient'

type MeResponse = {
  userId: string
  username: string
  fullName?: string | null
  email?: string | null
  phone?: string | null
}

const loading = ref(true)
const saving = ref(false)
const changing = ref(false)

const profileMessage = ref('')
const profileMessageType = ref<'success' | 'error'>('success')

const pwdMessage = ref('')
const pwdMessageType = ref<'success' | 'error'>('success')

const form = reactive({
  username: '',
  fullName: '',
  email: '',
  phone: '',
  avatarUrl: '',
})

const initials = ref('U')

const pwd = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const loadMe = async () => {
  loading.value = true
  profileMessage.value = ''
  try {
    const me = await apiClient.get('/api/users/me')
    const data = me.data as MeResponse
    form.username = data.username ?? ''
    form.fullName = data.fullName ?? ''
    form.email = data.email ?? ''
    form.phone = data.phone ?? ''
    form.avatarUrl = (data as any).avatarUrl ?? ''
    localStorage.setItem('username', form.username)
    if (form.avatarUrl) {
      localStorage.setItem('avatarUrl', form.avatarUrl)
    } else {
      localStorage.removeItem('avatarUrl')
    }

    const computeInitials = (name: string) => {
      const trimmed = (name ?? '').trim()
      if (!trimmed) return 'U'
      const parts = trimmed.split(/\s+/).filter(Boolean)
      const first = parts[0]?.[0] ?? 'U'
      const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : ''
      return (first + last).toUpperCase()
    }

    const name = (form.fullName || form.username).trim()
    initials.value = computeInitials(name)
  } catch (e: any) {
    profileMessageType.value = 'error'
    profileMessage.value = e?.response?.data ?? 'Không thể tải thông tin người dùng.'
  } finally {
    loading.value = false
  }
}

const saveProfile = async () => {
  if (saving.value) return
  saving.value = true
  profileMessage.value = ''
  try {
    await apiClient.put('/api/users/me', {
      fullName: form.fullName,
      email: form.email,
      phone: form.phone,
      avatarUrl: form.avatarUrl,
    })
    profileMessageType.value = 'success'
    profileMessage.value = 'Đã lưu thay đổi.'
    await loadMe()
  } catch (e: any) {
    profileMessageType.value = 'error'
    profileMessage.value = e?.response?.data ?? 'Lưu thất bại.'
  } finally {
    saving.value = false
  }
}

const changePassword = async () => {
  if (changing.value) return
  pwdMessage.value = ''

  if (!pwd.currentPassword || !pwd.newPassword) {
    pwdMessageType.value = 'error'
    pwdMessage.value = 'Vui lòng nhập đầy đủ mật khẩu.'
    return
  }

  if (pwd.newPassword !== pwd.confirmPassword) {
    pwdMessageType.value = 'error'
    pwdMessage.value = 'Xác nhận mật khẩu không khớp.'
    return
  }

  changing.value = true
  try {
    await apiClient.put('/api/users/me/password', {
      currentPassword: pwd.currentPassword,
      newPassword: pwd.newPassword,
    })

    pwdMessageType.value = 'success'
    pwdMessage.value = 'Đổi mật khẩu thành công.'
    pwd.currentPassword = ''
    pwd.newPassword = ''
    pwd.confirmPassword = ''
  } catch (e: any) {
    pwdMessageType.value = 'error'
    pwdMessage.value = e?.response?.data ?? 'Đổi mật khẩu thất bại.'
  } finally {
    changing.value = false
  }
}

onMounted(() => {
  loadMe()
})
</script>

<style scoped>
.settings-page {
  padding: 0;
}

.page-header {
  background: #fff;
  border: 1px solid #eee;
  border-radius: 4px;
  padding: 16px;
  margin-bottom: 14px;
}

.breadcrumb {
  font-size: 12px;
  color: #999;
  margin-bottom: 10px;
}

.breadcrumb .sep {
  margin: 0 6px;
  color: #bbb;
}

.breadcrumb .current {
  color: #333;
}

.header-row {
  display: flex;
  align-items: center;
  gap: 16px;
}

.page-title {
  font-size: 16px;
  font-weight: 600;
  color: #222;
  margin: 0;
}avatar-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.avatar-preview {
  width: 52px;
  height: 52px;
  border-radius: 999px;
  overflow: hidden;
  border: 1px solid #eee;
  background: #fafafa;
  display: flex;
  align-items: center;
  justify-content: center;
}

.avatar-preview img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  color: #f26f3b;
}

.

.grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
}

.card {
  background: #fff;
  border: 1px solid #eee;
  border-radius: 4px;
  padding: 16px;
}

.card-title {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 12px;
  color: #333;
}

.form {
  display: grid;
  gap: 12px;
}

.field {
  display: grid;
  gap: 6px;
}

label {
  font-size: 12px;
  color: #777;
}

input {
  border: 1px solid #ddd;
  border-radius: 4px;
  padding: 10px 12px;
  font-size: 13px;
  outline: none;
}

input:focus {
  border-color: #f26f3b;
}

.actions {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 4px;
}

.btn {
  border: none;
  border-radius: 4px;
  padding: 10px 14px;
  background: #f26f3b;
  color: #fff;
  font-size: 13px;
  cursor: pointer;
}

.btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.msg {
  font-size: 12px;
}

.msg.success {
  color: #1a7f37;
}

.msg.error {
  color: #d1242f;
}

.muted {
  font-size: 12px;
  color: #999;
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
