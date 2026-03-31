<template>
  <div class="page">
    <div class="page-head">
      <div>
        <div class="title">ASS Subtitles</div>
        <div class="muted">Danh sách subtitle ASS đã lưu (lưu từ My Studio).</div>
      </div>
      <div class="actions">
        <button class="btn secondary" type="button" :disabled="loading" @click="load">{{ loading ? 'Loading...' : 'Refresh' }}</button>
      </div>
    </div>

    <div v-if="error" class="error">{{ error }}</div>

    <div v-if="items.length === 0 && !loading" class="card" style="margin-top: 10px;">
      <div class="muted">Chưa có subtitle nào.</div>
    </div>

    <div class="list" v-else>
      <div v-for="it in items" :key="it.id" class="card item">
        <div class="row">
          <div>
            <div class="mono" style="font-weight: 800;">{{ it.sourceFileName }}</div>
            <div class="muted" style="margin-top: 4px;">{{ it.createdAtUtc }}</div>
          </div>
          <div class="item-actions">
            <button class="btn secondary" type="button" :disabled="renamingId === it.id" @click="rename(it)">
              {{ renamingId === it.id ? 'Saving...' : 'Rename' }}
            </button>
            <button class="btn secondary" type="button" @click="toggleView(it)">
              {{ it._open ? 'Hide' : 'View' }}
            </button>
            <button class="btn" type="button" @click="downloadAss(it)">Download</button>
            <button class="btn secondary" type="button" :disabled="deletingId === it.id" @click="del(it)">
              {{ deletingId === it.id ? 'Deleting...' : 'Delete' }}
            </button>
          </div>
        </div>

        <div v-if="it._open" class="detail">
          <textarea class="textarea mono" rows="14" :value="it.assText" readonly></textarea>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import api from '@/infrastructure/http/apiClient'

type GeneratedSubtitleDto = {
  id: string
  sourceFileName: string
  assTemplateId?: string | null
  assText: string
  createdAtUtc: string
  updatedAtUtc: string
}

type UiItem = GeneratedSubtitleDto & { _open?: boolean }

const items = ref<UiItem[]>([])
const loading = ref(false)
const error = ref('')
const deletingId = ref<string>('')
const renamingId = ref<string>('')

const load = async () => {
  error.value = ''
  try {
    loading.value = true
    const data = await api.get<GeneratedSubtitleDto[]>('admin/generated-subtitles?limit=50')
    items.value = (data ?? []).map((x) => ({ ...x, _open: false }))
  } catch (e: any) {
    error.value = e?.message ?? String(e)
  } finally {
    loading.value = false
  }
}

const rename = async (it: UiItem) => {
  if (!it?.id) return
  const current = String(it.sourceFileName ?? '').trim()
  const next = prompt('Tên ASS mới', current)
  if (next == null) return
  const name = String(next).trim()
  if (!name) return

  try {
    renamingId.value = it.id
    const updated = await api.put<GeneratedSubtitleDto>(`admin/generated-subtitles/${it.id}/name`, {
      sourceFileName: name,
    })
    it.sourceFileName = updated?.sourceFileName ?? name
    it.updatedAtUtc = updated?.updatedAtUtc ?? it.updatedAtUtc
  } catch (e: any) {
    alert(e?.message ?? String(e))
  } finally {
    renamingId.value = ''
  }
}

const toggleView = (it: UiItem) => {
  it._open = !it._open
}

const downloadAss = (it: UiItem) => {
  const blob = new Blob([String(it.assText ?? '')], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  const base = (it.sourceFileName ?? 'subtitle').replace(/\.[^.]+$/, '')
  a.download = `${base || 'subtitle'}.ass`
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

const del = async (it: UiItem) => {
  if (!it?.id) return
  if (!confirm('Delete subtitle này?')) return

  try {
    deletingId.value = it.id
    await api.delete(`admin/generated-subtitles/${it.id}`)
    items.value = items.value.filter((x) => x.id !== it.id)
  } catch (e: any) {
    alert(e?.message ?? String(e))
  } finally {
    deletingId.value = ''
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

.page-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.title {
  font-size: 18px;
  font-weight: 800;
}

.muted {
  opacity: 0.75;
  font-size: 13px;
}

.actions {
  display: flex;
  gap: 10px;
}

.list {
  margin-top: 10px;
  display: grid;
  gap: 10px;
}

.card {
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 12px;
  padding: 12px;
  background: #fff;
}

.item .row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.item-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.detail {
  margin-top: 10px;
}

.textarea {
  width: 100%;
  resize: vertical;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
}

.error {
  margin-top: 10px;
  color: #d1242f;
}

.btn {
  border: 1px solid rgba(0, 0, 0, 0.12);
  background: #f26f3b;
  color: #fff;
  border-radius: 10px;
  padding: 8px 12px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 700;
}

.btn.secondary {
  background: #fff;
  color: #333;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
