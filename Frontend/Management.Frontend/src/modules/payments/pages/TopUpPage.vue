<template>
  <div class="topup-page">
    <div class="page-header">
      <div class="breadcrumb">
        <span>Trang chủ</span>
        <span class="sep">/</span>
        <span>Thanh toán</span>
        <span class="sep">/</span>
        <span class="current">Nạp tiền</span>
      </div>

      <div class="header-row">
        <h1 class="page-title">Nạp tiền</h1>
      </div>
    </div>

    <div class="grid">
      <div class="card">
        <div class="card-title">Quét mã QR</div>
        <div class="muted">Sử dụng app ngân hàng để quét mã và chuyển khoản đúng nội dung.</div>

        <div class="qr-box">
          <div class="qr-placeholder">
            <div class="qr-title">QR</div>
            <div class="qr-sub">(Bạn có thể thay bằng ảnh QR thật sau)</div>
          </div>
        </div>

        <div class="note">
          <div class="note-label">Nội dung chuyển khoản</div>
          <div class="note-value">{{ info?.transferNote || '-' }}</div>
          <button class="copy-btn" :disabled="!info?.transferNote" @click="copy(info!.transferNote)">Copy</button>
        </div>
      </div>

      <div class="card">
        <div class="card-title">Chuyển khoản thường</div>
        <div v-if="loading" class="muted">Đang tải thông tin...</div>
        <div v-else class="bank">
          <div class="row">
            <div class="label">Ngân hàng</div>
            <div class="value">{{ info?.bankName || '-' }}</div>
          </div>
          <div class="row">
            <div class="label">Số tài khoản</div>
            <div class="value mono">{{ info?.accountNumber || '-' }}</div>
          </div>
          <div class="row">
            <div class="label">Chủ tài khoản</div>
            <div class="value">{{ info?.accountName || '-' }}</div>
          </div>
          <div class="row">
            <div class="label">Nội dung</div>
            <div class="value mono">{{ info?.transferNote || '-' }}</div>
          </div>

          <div class="hint">
            Lưu ý: Mỗi user có 1 mã nội dung cố định. Vui lòng chuyển khoản đúng nội dung để hệ thống ghi nhận.
          </div>

          <div class="actions">
            <button class="btn" @click="copyBankInfo" :disabled="!info">Copy thông tin</button>
            <div v-if="message" class="msg">{{ message }}</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import apiClient from '@/infrastructure/apiClient'

type TopUpInfo = {
  bankName: string
  accountNumber: string
  accountName: string
  transferNote: string
  qrTemplate?: string | null
}

const loading = ref(true)
const info = ref<TopUpInfo | null>(null)
const message = ref('')

const loadInfo = async () => {
  loading.value = true
  message.value = ''
  try {
    const res = await apiClient.get('/api/payments/topup-info')
    info.value = res.data as TopUpInfo
  } catch (e: any) {
    message.value = e?.response?.data ?? 'Không thể tải thông tin nạp tiền.'
  } finally {
    loading.value = false
  }
}

const copy = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text)
    message.value = 'Đã copy.'
    setTimeout(() => (message.value = ''), 1500)
  } catch {
    message.value = 'Copy thất bại.'
  }
}

const copyBankInfo = async () => {
  if (!info.value) return
  const text = `Ngan hang: ${info.value.bankName}\nSo tai khoan: ${info.value.accountNumber}\nChu tai khoan: ${info.value.accountName}\nNoi dung: ${info.value.transferNote}`
  await copy(text)
}

onMounted(() => {
  loadInfo()
})
</script>

<style scoped>
.topup-page {
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
}

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
  margin-bottom: 8px;
  color: #333;
}

.muted {
  font-size: 12px;
  color: #999;
  margin-bottom: 12px;
}

.qr-box {
  display: flex;
  justify-content: center;
  padding: 10px 0 14px 0;
}

.qr-placeholder {
  width: 220px;
  height: 220px;
  border: 1px dashed #ddd;
  border-radius: 8px;
  background: #fafafa;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.qr-title {
  font-weight: 800;
  color: #f26f3b;
}

.qr-sub {
  font-size: 12px;
  color: #999;
}

.note {
  display: flex;
  align-items: center;
  gap: 10px;
  border: 1px solid #eee;
  border-radius: 6px;
  padding: 10px 12px;
}

.note-label {
  font-size: 12px;
  color: #999;
  min-width: 140px;
}

.note-value {
  flex: 1;
  font-size: 13px;
  font-weight: 700;
  color: #222;
}

.copy-btn {
  border: 1px solid #f26f3b;
  background: #fff;
  color: #f26f3b;
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 12px;
  cursor: pointer;
}

.copy-btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.bank {
  display: grid;
  gap: 10px;
}

.row {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 10px;
  align-items: center;
}

.label {
  font-size: 12px;
  color: #777;
}

.value {
  font-size: 13px;
  color: #222;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
}

.hint {
  margin-top: 6px;
  font-size: 12px;
  color: #999;
  line-height: 1.4;
}

.actions {
  margin-top: 10px;
  display: flex;
  align-items: center;
  gap: 10px;
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

.msg {
  font-size: 12px;
  color: #666;
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: 1fr;
  }
  .row {
    grid-template-columns: 1fr;
  }
  .note {
    flex-direction: column;
    align-items: stretch;
  }
  .note-label {
    min-width: 0;
  }
}
</style>
