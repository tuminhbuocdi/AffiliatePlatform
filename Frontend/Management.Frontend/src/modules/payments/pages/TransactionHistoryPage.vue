<template>
  <div class="history-page">
    <div class="page-header">
      <div class="breadcrumb">
        <span>Trang chủ</span>
        <span class="sep">/</span>
        <span>Thanh toán</span>
        <span class="sep">/</span>
        <span class="current">Lịch sử giao dịch</span>
      </div>

      <div class="header-row">
        <h1 class="page-title">Lịch sử giao dịch</h1>
      </div>
    </div>

    <BaseGrid
      class="grid"
      :loading="loading"
      :is-empty="!loading && items.length === 0"
      empty-title="Dữ liệu trống"
      empty-description="Chưa có giao dịch nào."
    >
      <template #header>
        <div class="card-title">Giao dịch</div>
      </template>

      <div v-if="items.length > 0" class="table">
        <div class="thead">
          <div>Thời gian</div>
          <div>Loại</div>
          <div>Số tiền</div>
          <div>Trạng thái</div>
          <div>Ghi chú</div>
        </div>
        <div class="tbody">
          <div v-for="it in items" :key="it.transactionId" class="tr">
            <div class="mono">{{ formatDate(it.createdAt) }}</div>
            <div>{{ formatType(it.type) }}</div>
            <div class="amount">{{ formatMoney(it.amount) }}</div>
            <div>
              <span class="badge" :class="it.status">{{ formatStatus(it.status) }}</span>
            </div>
            <div class="note">{{ it.note || '-' }}</div>
          </div>
        </div>

        <div class="pager">
          <button class="btn-outline" :disabled="page===1 || loading" @click="prev">Trước</button>
          <div class="muted">Trang {{ page }}</div>
          <button class="btn-outline" :disabled="items.length < pageSize || loading" @click="next">Sau</button>
        </div>
      </div>

      <div v-if="error" class="error">{{ error }}</div>
    </BaseGrid>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import apiClient from '@/infrastructure/apiClient'
import BaseGrid from '@/shared/ui/BaseGrid.vue'

type Tx = {
  transactionId: string
  userId: string
  type: string
  amount: number
  status: string
  note?: string | null
  createdAt: string
  updatedAt?: string | null
}

const loading = ref(true)
const error = ref('')
const items = ref<Tx[]>([])
const page = ref(1)
const pageSize = 20

const load = async () => {
  loading.value = true
  error.value = ''
  try {
    const res = await apiClient.get('/api/payments/transactions', { params: { page: page.value, pageSize } })
    items.value = res.data as Tx[]
  } catch (e: any) {
    error.value = e?.response?.data ?? 'Không thể tải lịch sử giao dịch.'
    items.value = []
  } finally {
    loading.value = false
  }
}

const prev = async () => {
  if (page.value <= 1) return
  page.value -= 1
  await load()
}

const next = async () => {
  page.value += 1
  await load()
}

const formatType = (t: string) => {
  if (t === 'topup') return 'Nạp tiền'
  if (t === 'withdraw') return 'Rút tiền'
  return t
}

const formatStatus = (s: string) => {
  if (s === 'pending') return 'Chờ'
  if (s === 'success') return 'Thành công'
  if (s === 'failed') return 'Thất bại'
  return s
}

const formatMoney = (amount: number) => {
  return new Intl.NumberFormat('vi-VN').format(amount)
}

const formatDate = (iso: string) => {
  try {
    const d = new Date(iso)
    return d.toLocaleString('vi-VN')
  } catch {
    return iso
  }
}

onMounted(() => {
  load()
})
</script>

<style scoped>
.history-page {
  padding: 0;
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
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
}

.grid {
  flex: 1;
  min-height: 0;
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

.muted {
  font-size: 12px;
  color: #999;
}

.table {
  display: grid;
  gap: 10px;
}

.thead {
  display: grid;
  grid-template-columns: 160px 120px 140px 120px 1fr;
  gap: 10px;
  font-size: 12px;
  color: #777;
  padding: 8px 10px;
  border-bottom: 1px solid #f0f0f0;
}

.tr {
  display: grid;
  grid-template-columns: 160px 120px 140px 120px 1fr;
  gap: 10px;
  padding: 10px;
  border: 1px solid #f3f3f3;
  border-radius: 6px;
  font-size: 13px;
  align-items: center;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
}

.amount {
  font-weight: 700;
  color: #f26f3b;
}

.badge {
  display: inline-flex;
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 12px;
  border: 1px solid #eee;
}

.badge.pending {
  color: #8a6d3b;
  background: #fff8e1;
  border-color: #ffecb5;
}

.badge.success {
  color: #1a7f37;
  background: #e6ffed;
  border-color: #b7eb8f;
}

.badge.failed {
  color: #d1242f;
  background: #fff1f0;
  border-color: #ffccc7;
}

.note {
  color: #666;
}

.pager {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
}

.btn-outline {
  border: 1px solid #eee;
  background: #fff;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 12px;
  cursor: pointer;
}

.btn-outline:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.error {
  margin-top: 10px;
  font-size: 12px;
  color: #d1242f;
  background: #fff1f0;
  border: 1px solid #ffccc7;
  padding: 8px 10px;
  border-radius: 4px;
}

@media (max-width: 1024px) {
  .thead,
  .tr {
    grid-template-columns: 140px 100px 120px 110px 1fr;
  }
}

@media (max-width: 768px) {
  .thead {
    display: none;
  }

  .tr {
    grid-template-columns: 1fr;
    gap: 6px;
  }
}
</style>
