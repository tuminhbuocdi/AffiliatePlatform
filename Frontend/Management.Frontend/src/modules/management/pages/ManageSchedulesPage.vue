<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Lịch đã lập</h1>
        <div class="muted">Xem danh sách lịch theo ngày/giờ. (UTC+7)</div>
      </div>

      <div class="toolbar">
        <input v-model="day" class="input" type="date" :disabled="loading" />
        <select v-model="status" class="input" :disabled="loading">
          <option value="">Tất cả trạng thái</option>
          <option value="Scheduled">Scheduled</option>
          <option value="Due">Due</option>
          <option value="Done">Done</option>
          <option value="Canceled">Canceled</option>
        </select>
        <button class="btn" type="button" @click="load" :disabled="loading">{{ loading ? 'Đang tải...' : 'Tải' }}</button>
      </div>
    </div>

    <div v-if="error" class="error">{{ error }}</div>

    <div v-if="loading" class="loading">Đang tải...</div>

    <div v-else>
      <div v-if="groups.length === 0" class="muted">Chưa có lịch trong ngày này.</div>

      <div v-for="g in groups" :key="g.key" class="card" style="margin-bottom: 12px">
        <div style="display:flex;align-items:center;justify-content:space-between;gap:12px">
          <div>
            <div style="font-weight: 700">{{ g.label }}</div>
            <div class="muted" style="font-size: 12px">{{ g.items.length }} lịch</div>
          </div>
        </div>

        <div class="table-wrap" style="margin-top: 10px">
          <table class="table">
            <thead>
              <tr>
                <th style="width: 120px">Giờ</th>
                <th>Nội dung</th>
                <th style="width: 120px">Trạng thái</th>
                <th style="width: 160px"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="it in g.items" :key="it.id">
                <td class="mono">{{ formatLocalTime(it.scheduledAtLocal) }}</td>
                <td>
                  <div class="truncate" style="max-width: 700px">{{ it.caption || '-' }}</div>
                  <div class="muted" style="font-size: 12px">Asset: {{ it.assets?.length ?? 0 }}</div>
                </td>
                <td>
                  <span :class="['pill', it.status === 'Done' ? 'ok' : it.status === 'Canceled' ? 'bad' : it.status === 'Due' ? 'warn' : '']">{{ it.status }}</span>
                </td>
                <td class="actions">
                  <button class="link" type="button" :disabled="loading" @click="openSchedule(it)">Mở</button>
                  <button class="link" type="button" :disabled="workingId === it.id || it.status === 'Done' || it.status === 'Canceled'" @click="markDone(it)">Done</button>
                  <button class="link danger" type="button" :disabled="workingId === it.id || it.status === 'Done' || it.status === 'Canceled'" @click="cancel(it)">Huỷ</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div v-if="open" class="modal-overlay" @click.self="close">
      <div class="modal modal-wide">
        <div class="modal-header">
          <div class="modal-title">Đăng ngay từ lịch</div>
          <button class="icon" type="button" @click="close">×</button>
        </div>

        <div class="modal-body">
          <ReelMultiUploadPanel
            :disabled="submitting"
            :image-urls="[]"
            :video-urls="openVideoUrls"
            :default-caption="openCaption"
            :video-input="{ kind: 'url', videoUrl: openVideoUrls[0] ?? '' }"
            :u="u"
            @submit="submitNow"
          />
          <div v-if="submitError" class="error" style="margin-top: 10px">{{ submitError }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn secondary" type="button" :disabled="submitting" @click="close">Đóng</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import api from '@/infrastructure/http/apiClient'
import ReelMultiUploadPanel from '@/shared/ui/ReelMultiUploadPanel.vue'
import { useReelMultiUpload, type VideoInput } from '@/shared/ui/useReelMultiUpload'

type ScheduleAsset = {
  type: string
  url: string
  sortOrder: number
}

type ScheduledPost = {
  id: string
  pageId?: string | null
  caption: string
  scheduledAtLocal: string
  scheduledAtUtc: string
  timezone: string
  targetsJson: string
  status: string
  remindedAtUtc?: string | null
  assets?: ScheduleAsset[]
}

const loading = ref(false)
const error = ref<string | null>(null)
const items = ref<ScheduledPost[]>([])
const workingId = ref<string | null>(null)

const open = ref(false)
const openItem = ref<ScheduledPost | null>(null)
const openCaption = ref('')
const openVideoUrls = ref<string[]>([])
const submitting = ref(false)
const submitError = ref<string | null>(null)

const u = useReelMultiUpload()

const today = () => {
  const d = new Date()
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${dd}`
}

const day = ref(today())
const status = ref('')

const toUtcIsoRange = (d: string) => {
  const date = (d ?? '').trim()
  const fromLocal = `${date}T00:00:00+07:00`
  const toLocal = `${date}T23:59:59+07:00`
  const fromUtc = new Date(fromLocal).toISOString()
  const toUtc = new Date(toLocal).toISOString()
  return { fromUtc, toUtc }
}

const formatLocalTime = (scheduledAtLocal: string) => {
  const s = (scheduledAtLocal ?? '').trim()
  if (!s) return ''
  // backend returns yyyy-MM-ddTHH:mm:ss
  const t = s.split('T')[1] ?? ''
  return t.slice(0, 5)
}

const formatLocalDate = (scheduledAtLocal: string) => {
  const s = (scheduledAtLocal ?? '').trim()
  if (!s) return ''
  return (s.split('T')[0] ?? '').trim()
}

const load = async () => {
  loading.value = true
  error.value = null
  try {
    const { fromUtc, toUtc } = toUtcIsoRange(day.value)
    const qs = new URLSearchParams()
    qs.set('fromUtc', fromUtc)
    qs.set('toUtc', toUtc)
    if (status.value) qs.set('status', status.value)

    const data = await api.get<ScheduledPost[]>(`admin/scheduled-posts?${qs.toString()}`)
    items.value = Array.isArray(data) ? data : []
  } catch (e: any) {
    error.value = e?.message ?? 'Tải lịch thất bại.'
    items.value = []
  } finally {
    loading.value = false
  }
}

const groups = computed(() => {
  const map = new Map<string, ScheduledPost[]>()
  for (const it of items.value) {
    const k = formatLocalDate(it.scheduledAtLocal)
    if (!map.has(k)) map.set(k, [])
    map.get(k)!.push(it)
  }

  const arr = Array.from(map.entries())
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([k, list]) => {
      const sorted = [...list].sort((x, y) => (x.scheduledAtLocal ?? '').localeCompare(y.scheduledAtLocal ?? ''))
      return { key: k, label: k, items: sorted }
    })

  return arr
})

const markDone = async (it: ScheduledPost) => {
  if (!it?.id) return
  workingId.value = it.id
  error.value = null
  try {
    await api.post(`admin/scheduled-posts/${encodeURIComponent(it.id)}/mark-done`, {})
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Cập nhật thất bại.'
  } finally {
    workingId.value = null
  }
}

const cancel = async (it: ScheduledPost) => {
  if (!it?.id) return
  workingId.value = it.id
  error.value = null
  try {
    await api.post(`admin/scheduled-posts/${encodeURIComponent(it.id)}/cancel`, {})
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Cập nhật thất bại.'
  } finally {
    workingId.value = null
  }
}

const close = () => {
  open.value = false
  openItem.value = null
  openCaption.value = ''
  openVideoUrls.value = []
  submitError.value = null
  u.reset({ keepTargets: true, keepSelections: false })
}

const openSchedule = (it: ScheduledPost) => {
  openItem.value = it
  openCaption.value = (it?.caption ?? '').toString()

  const assets = Array.isArray(it?.assets) ? it.assets : []
  const videos = assets
    .filter((x) => (x?.type ?? '').toString().toLowerCase() === 'video')
    .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
    .map((x) => (x.url ?? '').toString().trim())
    .filter(Boolean)
  openVideoUrls.value = videos

  // Restore selections + forms from TargetsJson (if stored)
  try {
    const t = JSON.parse((it?.targetsJson ?? '{}').toString()) as any
    u.targets.value = { fb: Boolean(t?.facebook), yt: Boolean(t?.youtube) }
    u.fbSelectedPageIds.value = Array.isArray(t?.fbPageIds) ? t.fbPageIds.filter((x: any) => typeof x === 'string') : []
    u.ytSelectedChannelIds.value = Array.isArray(t?.ytChannelIds) ? t.ytChannelIds.filter((x: any) => typeof x === 'string') : []

    if (t?.ytPlaylistIdsByChannel && typeof t.ytPlaylistIdsByChannel === 'object') {
      u.ytSelectedPlaylistIdsByChannel.value = t.ytPlaylistIdsByChannel
    }

    u.fbForm.value = {
      title: (t?.fbTitle ?? '').toString(),
      description: (t?.fbDescription ?? '').toString() || openCaption.value,
      link: (t?.fbLink ?? '').toString(),
    }
    u.ytForm.value = {
      title: (t?.ytTitle ?? '').toString(),
      description: (t?.ytDescription ?? '').toString() || openCaption.value,
      privacyStatus: (t?.ytPrivacyStatus ?? 'public') === 'private' || (t?.ytPrivacyStatus ?? 'public') === 'unlisted' || (t?.ytPrivacyStatus ?? 'public') === 'public' ? t.ytPrivacyStatus : 'public',
      tagsText: (t?.ytTagsText ?? '').toString(),
      publishAt: (t?.ytPublishAt ?? '').toString(),
    }
  } catch {
    // ignore
  }

  open.value = true
}

const submitNow = async (input: VideoInput) => {
  if (!openItem.value) return
  submitting.value = true
  submitError.value = null
  try {
    await u.submit(input, null)
    // After posting successfully, auto mark done
    await api.post(`admin/scheduled-posts/${encodeURIComponent(openItem.value.id)}/mark-done`, {})
    await load()
    close()
  } catch (e: any) {
    submitError.value = e?.message ?? 'Đăng thất bại.'
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void load()
})
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  z-index: 50;
}

.modal {
  width: 100%;
  max-width: 720px;
  background: #fff;
  border-radius: 14px;
  overflow: hidden;
  border: 1px solid #eee;
  display: flex;
  flex-direction: column;
  max-height: 90vh;
}

.modal.modal-wide {
  width: 100%;
  max-width: 1100px;
  min-width: 980px;
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

.modal-footer {
  padding: 12px 14px;
  border-top: 1px solid #eee;
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}

@media (max-width: 560px) {
  .modal.modal-wide {
    min-width: 0;
    max-width: 100%;
  }
}

@media (max-width: 1024px) {
  .modal.modal-wide {
    min-width: 0;
  }
}
</style>
