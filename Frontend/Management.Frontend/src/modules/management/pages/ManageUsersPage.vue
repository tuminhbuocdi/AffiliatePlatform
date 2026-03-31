<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Quản lý người dùng</h1>
        <div class="muted">Danh sách user, thêm / sửa / xoá.</div>
      </div>

      <div class="toolbar">
        <button class="btn" type="button" @click="openCreate">Thêm user</button>
        <button class="btn secondary" type="button" @click="load" :disabled="loading">{{ loading ? 'Đang tải...' : 'Tải' }}</button>
      </div>
    </div>

    <div v-if="error" class="error">{{ error }}</div>

    <div v-if="loading" class="loading">Đang tải...</div>

    <div v-else class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th>Username</th>
            <th>Họ tên</th>
            <th>Email</th>
            <th>Phone</th>
            <th>Role</th>
            <th>Active</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in users" :key="u.userId">
            <td class="mono">{{ u.username }}</td>
            <td>{{ u.fullName || '-' }}</td>
            <td>{{ u.email || '-' }}</td>
            <td>{{ u.phone || '-' }}</td>
            <td>{{ u.userRole || 'user' }}</td>
            <td>
              <span :class="['pill', u.isActive ? 'ok' : 'bad']">{{ u.isActive ? 'Yes' : 'No' }}</span>
            </td>
            <td class="actions">
              <button class="link" type="button" @click="openEdit(u)">Sửa</button>
              <button class="link danger" type="button" @click="remove(u)" :disabled="removingId === u.userId">Xoá</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="modalOpen" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <div class="modal-header">
          <div class="modal-title">{{ isCreate ? 'Thêm user' : 'Sửa user' }}</div>
          <button class="icon" type="button" @click="closeModal">×</button>
        </div>

        <div class="modal-body">
          <div class="form">
            <label>
              <div class="lbl">Username</div>
              <input v-model="model.username" class="input" :disabled="!isCreate" />
            </label>

            <label v-if="isCreate">
              <div class="lbl">Password</div>
              <input v-model="model.password" class="input" type="password" placeholder="Tối thiểu 6 ký tự" />
            </label>

            <label>
              <div class="lbl">Họ tên</div>
              <input v-model="model.fullName" class="input" />
            </label>

            <label>
              <div class="lbl">Email</div>
              <input v-model="model.email" class="input" />
            </label>

            <label>
              <div class="lbl">Phone</div>
              <input v-model="model.phone" class="input" />
            </label>

            <label>
              <div class="lbl">Role</div>
              <select v-model="model.userRole" class="input">
                <option value="user">user</option>
                <option value="admin">admin</option>
              </select>
            </label>

            <label class="row">
              <input v-model="model.isActive" type="checkbox" />
              <span>Active</span>
            </label>
          </div>

          <div v-if="modalError" class="error">{{ modalError }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn" type="button" @click="save" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Lưu' }}</button>
          <button class="btn secondary" type="button" @click="closeModal" :disabled="saving">Đóng</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import api from '@/infrastructure/http/apiClient'

type UserItem = {
  userId: string
  username: string
  fullName?: string | null
  email?: string | null
  phone?: string | null
  userRole?: string | null
  isActive: boolean
}

const users = ref<UserItem[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

const modalOpen = ref(false)
const isCreate = ref(true)
const saving = ref(false)
const modalError = ref<string | null>(null)
const editingId = ref<string | null>(null)

const removingId = ref<string | null>(null)

const model = ref({
  username: '',
  password: '',
  fullName: '',
  email: '',
  phone: '',
  userRole: 'user',
  isActive: true,
})

const load = async () => {
  loading.value = true
  error.value = null
  try {
    const data = await api.get<UserItem[]>('admin/users')
    users.value = data
  } catch (e: any) {
    error.value = e?.message ?? 'Không tải được danh sách user.'
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  isCreate.value = true
  editingId.value = null
  modalError.value = null
  model.value = { username: '', password: '', fullName: '', email: '', phone: '', userRole: 'user', isActive: true }
  modalOpen.value = true
}

const openEdit = (u: UserItem) => {
  isCreate.value = false
  editingId.value = u.userId
  modalError.value = null
  model.value = {
    username: u.username,
    password: '',
    fullName: u.fullName ?? '',
    email: u.email ?? '',
    phone: u.phone ?? '',
    userRole: (u.userRole || 'user') as any,
    isActive: !!u.isActive,
  }
  modalOpen.value = true
}

const closeModal = () => {
  if (saving.value) return
  modalOpen.value = false
}

const save = async () => {
  saving.value = true
  modalError.value = null
  try {
    if (isCreate.value) {
      await api.post('admin/users', {
        username: model.value.username,
        password: model.value.password,
        fullName: model.value.fullName,
        email: model.value.email,
        phone: model.value.phone,
        userRole: model.value.userRole,
        isActive: model.value.isActive,
      })
    } else if (editingId.value) {
      await api.put(`admin/users/${editingId.value}`, {
        fullName: model.value.fullName,
        email: model.value.email,
        phone: model.value.phone,
        userRole: model.value.userRole,
        isActive: model.value.isActive,
      })
    }

    await load()
    closeModal()
  } catch (e: any) {
    modalError.value = e?.message ?? 'Lưu thất bại.'
  } finally {
    saving.value = false
  }
}

const remove = async (u: UserItem) => {
  if (!confirm(`Xoá user ${u.username}?`)) return
  removingId.value = u.userId
  try {
    await api.delete(`admin/users/${u.userId}`)
    await load()
  } catch (e: any) {
    alert(e?.message ?? 'Xoá thất bại')
  } finally {
    removingId.value = null
  }
}

onMounted(() => {
  load()
})
</script>

<style scoped>
.page {
  padding: 16px;
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
}

h1 {
  margin: 0;
  font-size: 18px;
}

.muted {
  color: #667085;
  font-size: 13px;
  margin-top: 4px;
}

.toolbar {
  display: flex;
  gap: 8px;
}

.btn {
  height: 36px;
  border-radius: 10px;
  padding: 0 12px;
  border: 1px solid #111827;
  background: #111827;
  color: #fff;
  cursor: pointer;
}

.btn.secondary {
  background: #fff;
  color: #111827;
}

.table-wrap {
  border: 1px solid #eee;
  border-radius: 14px;
  overflow: auto;
  background: #fff;
}

.table {
  width: 100%;
  border-collapse: collapse;
  min-width: 820px;
}

th,
td {
  padding: 12px 12px;
  border-bottom: 1px solid #f1f1f1;
  font-size: 13px;
}

th {
  text-align: left;
  color: #667085;
  font-weight: 700;
  background: #fafafa;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
}

.actions {
  white-space: nowrap;
}

.link {
  border: none;
  background: transparent;
  color: #111827;
  cursor: pointer;
  font-weight: 700;
  padding: 0 6px;
}

.link.danger {
  color: #b42318;
}

.pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 3px 8px;
  border-radius: 999px;
  font-size: 12px;
  border: 1px solid #eee;
}

.pill.ok {
  background: #ecfdf3;
  border-color: #abefc6;
  color: #067647;
}

.pill.bad {
  background: #fef3f2;
  border-color: #fecdca;
  color: #b42318;
}

.loading {
  padding: 12px 0;
}

.error {
  color: #b42318;
  background: #fef3f2;
  border: 1px solid #fecdca;
  padding: 10px 12px;
  border-radius: 12px;
  margin-bottom: 12px;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 14px;
}

.modal {
  width: 100%;
  max-width: 640px;
  background: #fff;
  border-radius: 14px;
  overflow: hidden;
  border: 1px solid #eee;
  display: flex;
  flex-direction: column;
  max-height: 90vh;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 14px;
  border-bottom: 1px solid #eee;
}

.modal-title {
  font-weight: 800;
}

.icon {
  width: 34px;
  height: 34px;
  border-radius: 10px;
  border: 1px solid #eee;
  background: #fff;
  font-size: 18px;
  cursor: pointer;
}

.modal-body {
  padding: 14px;
  overflow: auto;
}

.form {
  display: grid;
  gap: 12px;
}

.lbl {
  font-size: 12px;
  color: #667085;
  margin-bottom: 6px;
}

.input {
  height: 36px;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 0 12px;
  outline: none;
  width: 100%;
}

.row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.modal-footer {
  padding: 12px 14px;
  border-top: 1px solid #eee;
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}

@media (max-width: 640px) {
  .page-header {
    flex-direction: column;
    align-items: stretch;
  }
  .toolbar {
    justify-content: flex-end;
  }
  .table {
    min-width: 720px;
  }
}
</style>
