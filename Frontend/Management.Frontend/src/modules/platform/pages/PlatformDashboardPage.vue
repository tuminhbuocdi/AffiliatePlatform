<template>
  <div class="platform-page">
    <div class="card">
      <div class="section-title">
        Thông tin chung
        <span v-if="dueCount > 0" class="pill warn" style="margin-left: 8px">Nhắc lịch: {{ dueCount }}</span>
      </div>
      <div class="form two-col" style="margin-bottom: 16px">
        <div class="field">
          <label>Video upload</label>
          <input type="file" accept="video/*" :disabled="working" @change="onPickVideo" />
          <div v-if="common.videoFile" class="muted" style="margin-top: 6px">{{ common.videoFile.name }}</div>
        </div>
        <div class="field">
          <label>Thumbnail (tuỳ chọn)</label>
          <input type="file" accept="image/*" :disabled="working" @change="onPickThumb" />
          <div v-if="common.thumbFile" class="muted" style="margin-top: 6px">{{ common.thumbFile.name }}</div>
        </div>
        <div class="field" style="margin-bottom: 16px">
        <label>AffiliateLink</label>
        <div class="aff-row">
          <BaseInput v-model="affiliateLink" placeholder="https://..." :disabled="working" />
          <BaseButton variant="secondary" :disabled="working || !affiliateLink.trim()" title="Copy" @click="copyAffiliateLink">Copy</BaseButton>
          <BaseButton variant="secondary" :disabled="working" @click="openPicker">Chọn sản phẩm</BaseButton>
        </div>
      </div>

      <div class="field" style="margin-bottom: 16px">
        <label>Chế độ</label>
        <select v-model="publishMode" class="input" :disabled="working">
          <option value="public">Public (đăng ngay)</option>
          <option value="schedule">Lập lịch (nhắc nhở)</option>
        </select>
        <div v-if="publishMode === 'schedule'" style="margin-top: 8px">
          <label>Giờ đăng (UTC+7)</label>
          <input v-model="scheduledAt" class="input" type="datetime-local" :disabled="working" />
        </div>
      </div>
      </div>

      <div v-if="fb.mode === 'reel'">
        <ReelMultiUploadPanel
          :disabled="working"
          :image-urls="[]"
          :video-urls="[]"
          :default-caption="fb.description"
          :video-input="{ kind: 'file', videoFile: common.videoFile ?? null }"
          :thumb-file="common.thumbFile"
          :u="reelUploader"
          @submit="onSubmitReelPanel"
        />
      </div>

      <div v-else class="platform-grid">
        <div class="platform-col">
          <div class="platform-head">
            <div class="platform-name">YouTube</div>
            <BaseButton
              variant="secondary"
              size="tiny"
              :disabled="working || ytLoading"
              :loading="ytLoading"
              aria-label="Tải kênh"
              title="Tải kênh"
              @click="loadYoutubeChannels"
            >
              <svg
                viewBox="0 0 24 24"
                width="16"
                height="16"
                fill="none"
                xmlns="http://www.w3.org/2000/svg"
                aria-hidden="true"
              >
                <path
                  d="M1 4v6h6"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
                <path
                  d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
              </svg>
            </BaseButton>
          </div>

          <div class="field">
            <label>Chọn kênh</label>
            <SelectMultiTags
              :model-value="ytSelectedChannelIds"
              :options="ytChannelOptions"
              placeholder="Chọn kênh..."
              :disabled="working || ytLoading"
              @update:model-value="updateYtSelectedChannelIds"
            />
          </div>

          <div class="field">
            <label class="label-row">
              <span>Title</span>
              <span class="char-counter">{{ (yt.title ?? '').trim().length }}/100</span>
            </label>
            <BaseInput v-model="yt.title" placeholder="Nhập title..." :disabled="working" />
          </div>

          <div class="field">
            <label class="label-row">
              <span>Description</span>
              <span class="char-counter">{{ (yt.description ?? '').trim().length }}/5000</span>
            </label>
            <BaseTextarea v-model="yt.description" :rows="4" placeholder="Nhập mô tả..." :disabled="working" />
          </div>

          <div class="field">
            <label>Privacy</label>
            <select v-model="yt.privacyStatus" class="input" :disabled="working">
              <option value="public">public</option>
              <option value="unlisted">unlisted</option>
              <option value="private">private</option>
            </select>
          </div>

          <div class="field">
            <label>Tags</label>
            <BaseInput v-model="yt.tagsText" placeholder="vd: shorts, trending" :disabled="working" />
          </div>

          <div v-if="ytSelectedChannelIds.length > 0" class="field">
            <label>Playlist</label>
            <div class="muted" style="margin-bottom: 8px">Chọn kênh trước, hệ thống sẽ load playlist tương ứng cho từng kênh.</div>

            <div v-for="cid in ytSelectedChannelIds" :key="cid" class="playlist-per-channel">
              <div class="playlist-title">
                <div class="muted" v-if="ytPlaylistsLoading[cid]" style="font-size: 12px">Đang tải playlist...</div>
              </div>

              <SelectMultiTags
                v-if="Array.isArray(ytSelectedPlaylistIdsByChannel[cid])"
                :model-value="ytSelectedPlaylistIdsByChannel[cid]"
                :options="ytPlaylistOptions(cid)"
                placeholder="Chọn playlist..."
                :disabled="working || ytPlaylistsLoading[cid] || (ytPlaylistsByChannel[cid]?.length ?? 0) === 0"
                @update:model-value="(v) => updateYtSelectedPlaylistIds(cid, v)"
              />
            </div>
            
            <div v-if="ytSelectedChannelIds.length > 0" class="channel-actions">
              <BaseButton variant="secondary" size="tiny" :disabled="working || ytSavingDefaultsAll" :loading="ytSavingDefaultsAll" @click="saveAllChannelDefaults">
                {{ ytSavingDefaultsAll ? 'Đang lưu...' : 'Lưu mặc định' }}
              </BaseButton>
            </div>
          </div>

          <div v-if="ytResult" class="result" :class="ytResult.ok ? 'ok' : 'error'">{{ ytResult.text }}</div>
        </div>

        <div class="platform-col">
          <div class="platform-head">
            <div class="platform-name">Facebook</div>
            <BaseButton
              variant="secondary"
              size="tiny"
              :disabled="working || fbLoading"
              :loading="fbLoading"
              aria-label="Tải pages"
              title="Tải pages"
              @click="loadFacebookPages"
            >
              <svg
                viewBox="0 0 24 24"
                width="16"
                height="16"
                fill="none"
                xmlns="http://www.w3.org/2000/svg"
                aria-hidden="true"
              >
                <path
                  d="M1 4v6h6"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
                <path
                  d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
              </svg>
            </BaseButton>
          </div>

          <div class="field">
            <label>Chọn Page</label>
            <SelectMultiTags
              :model-value="fbSelectedPageIds"
              :options="fbPageOptions"
              placeholder="Chọn page..."
              :disabled="working || fbLoading"
              @update:model-value="updateFbSelectedPageIds"
            />
          </div>

          <div class="field">
            <label>Title</label>
            <BaseInput v-model="fb.title" placeholder="Nhập title..." :disabled="working" />
          </div>

          <div class="field">
            <label>Caption / Description</label>
            <BaseTextarea v-model="fb.description" :rows="4" placeholder="Nhập caption Facebook..." :disabled="working" />
          </div>

          <div class="field">
            <label>Kiểu đăng</label>
            <select v-model="fb.mode" class="input" :disabled="working">
              <option value="reel">Reel</option>
              <option value="post">Post</option>
            </select>
          </div>

          <div class="field" v-if="fb.mode === 'reel'">
            <label>Link (tuỳ chọn)</label>
            <BaseInput v-model="fb.link" placeholder="https://..." :disabled="working" />
            <div class="muted" style="margin-top: 6px">Link sẽ được gắn vào caption (CTA ads không hỗ trợ cho Reel organic).</div>
          </div>

          <div class="field" v-else>
            <label>Link (tuỳ chọn)</label>
            <BaseInput v-model="fb.link" placeholder="https://..." :disabled="working" />
          </div>

          <div class="field" v-if="fb.mode === 'post'">
            <label>Lịch đăng (UTC+7, tuỳ chọn)</label>
            <input v-model="fb.publishAt" class="input" type="datetime-local" :disabled="working" />
          </div>

          <div v-if="fbSelectedPageIds.length > 0" class="channel-actions">
            <BaseButton variant="secondary" size="tiny" :disabled="working || fbSavingDefaultsAll" :loading="fbSavingDefaultsAll" @click="saveAllFbPageDefaults">
              {{ fbSavingDefaultsAll ? 'Đang lưu...' : 'Lưu mặc định' }}
            </BaseButton>
          </div>

          <div v-if="fbResult" class="result" :class="fbResult.ok ? 'ok' : 'error'">{{ fbResult.text }}</div>
        </div>
      </div>

      <div class="actions" style="margin-top: 16px">
        <BaseButton :disabled="working || !canUpload" :loading="working" @click="uploadAll">
          {{ working ? 'Đang upload...' : 'Upload' }}
        </BaseButton>
        <BaseButton variant="secondary" :disabled="working" @click="resetForm">Làm mới</BaseButton>
      </div>
    </div>

    <div v-if="pickerOpen" class="modal-overlay" @click.self="closePicker">
      <div class="modal card">
        <div class="modal-head">
          <div class="modal-title">Chọn sản phẩm</div>
          <button class="icon" type="button" @click="closePicker">×</button>
        </div>

        <div class="modal-body">
          <div class="picker-toolbar">
            <BaseInput v-model="pickerSearch" placeholder="Tìm theo ExternalItemId / tên..." :disabled="pickerLoading" @keyup.enter="applyPickerSearch" />
            <BaseButton variant="secondary" :disabled="pickerLoading" @click="applyPickerSearch">Tìm</BaseButton>
          </div>

          <div v-if="pickerError" class="msg error">{{ pickerError }}</div>
          <div v-if="pickerLoading" class="muted">Đang tải...</div>

          <div v-else class="picker-list">
            <button
              v-for="p in pickerItems"
              :key="p.externalItemId"
              type="button"
              class="picker-item"
              :class="{ active: selectedPick?.externalItemId === p.externalItemId }"
              @click="selectPick(p)"
            >
              <img class="picker-thumb" :src="p.imageUrl || '/placeholder-product.jpg'" alt="" />
              <div class="picker-meta">
                <div class="picker-name">
                  <span class="picker-name-text">{{ p.name }}</span>
                  <span v-if="p.hasSocialLinks" class="pill ok" title="Đã có SocialLinks">✓</span>
                </div>
                <div class="picker-sub muted mono">{{ p.externalItemId }}</div>
                <div class="picker-sub truncate"><span class="muted">Affiliate:</span> {{ p.affiliateLink || '-' }}</div>
              </div>
            </button>
          </div>

          <div v-if="!pickerLoading" class="picker-paging">
            <div class="muted">Tổng: {{ pickerTotal }}</div>
            <div class="paging-actions">
              <BaseButton variant="secondary" :disabled="pickerLoading || pickerPage <= 1" @click="goPickerPage(pickerPage - 1)">Trước</BaseButton>
              <div class="mono">Trang {{ pickerPage }} / {{ pickerTotalPages }}</div>
              <BaseButton variant="secondary" :disabled="pickerLoading || pickerPage >= pickerTotalPages" @click="goPickerPage(pickerPage + 1)">Sau</BaseButton>
            </div>
          </div>
        </div>

        <div class="modal-foot">
          <BaseButton :disabled="!selectedPick || working" @click="confirmPick">OK</BaseButton>
          <BaseButton variant="secondary" :disabled="working" @click="closePicker">Đóng</BaseButton>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import api from '@/infrastructure/http/apiClient'
import SelectMultiTags from '@/shared/ui/SelectMultiTags.vue'
import ReelMultiUploadPanel from '@/shared/ui/ReelMultiUploadPanel.vue'
import { useReelMultiUpload, type VideoInput } from '@/shared/ui/useReelMultiUpload'
import BaseButton from '@/shared/ui/BaseButton.vue'
import BaseInput from '@/shared/ui/BaseInput.vue'
import BaseTextarea from '@/shared/ui/BaseTextarea.vue'
import { toast } from '@/shared/ui/toast'

type YoutubeChannel = {
  id: string
  title: string
}

type YoutubePlaylist = {
  id: string
  title: string
}

type FbPage = {
  id: string
  name: string
}

const working = ref(false)

const publishMode = ref<'public' | 'schedule'>('public')
const scheduledAt = ref('')
const dueCount = ref(0)
let duePollTimer: number | null = null

const message = ref('')
const messageType = ref<'success' | 'error'>('success')
const setMsg = (msg: string, type: 'success' | 'error' = 'success') => {
  message.value = msg
  messageType.value = type
}
const clearMsg = () => {
  message.value = ''
}

const common = reactive({
  videoFile: null as File | null,
  thumbFile: null as File | null,
})

const affiliateLink = ref('')

type PickerItem = {
  externalItemId: string
  name: string
  imageUrl?: string | null
  affiliateLink?: string | null
  hasSocialLinks: boolean
}

type PickerResponse = {
  items: PickerItem[]
  total: number
  page: number
  pageSize: number
}

const pickerOpen = ref(false)
const pickerLoading = ref(false)
const pickerError = ref<string | null>(null)
const pickerItems = ref<PickerItem[]>([])
const selectedPick = ref<PickerItem | null>(null)

const pickerSearch = ref('')
const pickerAppliedSearch = ref('')
const pickerPage = ref(1)
const pickerPageSize = ref(20)
const pickerTotal = ref(0)

const pickerTotalPages = computed(() => {
  const total = Math.max(0, pickerTotal.value)
  const size = Math.max(1, pickerPageSize.value)
  return Math.max(1, Math.ceil(total / size))
})

const copyAffiliateLink = async () => {
  const text = affiliateLink.value.trim()
  if (!text) return
  try {
    await navigator.clipboard.writeText(text)
    setMsg('Đã copy AffiliateLink.', 'success')
  } catch {
    try {
      const ta = document.createElement('textarea')
      ta.value = text
      ta.style.position = 'fixed'
      ta.style.left = '-9999px'
      document.body.appendChild(ta)
      ta.select()
      document.execCommand('copy')
      document.body.removeChild(ta)
      setMsg('Đã copy AffiliateLink.', 'success')
    } catch {
      setMsg('Copy thất bại.', 'error')
    }
  }
}

const loadPicker = async () => {
  pickerLoading.value = true
  pickerError.value = null
  try {
    const q = pickerAppliedSearch.value.trim()
    const qs = new URLSearchParams()
    if (q) qs.set('search', q)
    qs.set('page', String(pickerPage.value))
    qs.set('pageSize', String(pickerPageSize.value))
    const data = await api.get<PickerResponse>(`admin/affiliate-picker/missing-social-links?${qs.toString()}`)

    pickerItems.value = Array.isArray(data?.items) ? data.items : []
    pickerTotal.value = typeof data?.total === 'number' ? data.total : 0
    pickerPage.value = typeof data?.page === 'number' ? data.page : pickerPage.value
    pickerPageSize.value = typeof data?.pageSize === 'number' ? data.pageSize : pickerPageSize.value
  } catch (e: any) {
    pickerItems.value = []
    pickerTotal.value = 0
    pickerError.value = e?.message ?? 'Không tải được danh sách sản phẩm.'
  } finally {
    pickerLoading.value = false
  }
}

const applyPickerSearch = async () => {
  pickerAppliedSearch.value = pickerSearch.value
  pickerPage.value = 1
  await loadPicker()
}

const goPickerPage = async (nextPage: number) => {
  const p = Math.max(1, Math.min(nextPage, pickerTotalPages.value))
  if (p === pickerPage.value) return
  pickerPage.value = p
  await loadPicker()
}

const openPicker = async () => {
  clearMsg()
  pickerOpen.value = true
  selectedPick.value = null
  pickerSearch.value = ''
  pickerAppliedSearch.value = ''
  pickerPage.value = 1
  pickerPageSize.value = 20
  pickerTotal.value = 0
  await loadPicker()
}

const closePicker = () => {
  if (pickerLoading.value) return
  pickerOpen.value = false
}

const selectPick = (p: PickerItem) => {
  selectedPick.value = p
}

const confirmPick = () => {
  if (!selectedPick.value) return
  affiliateLink.value = selectedPick.value.affiliateLink ?? ''
  closePicker()
}

const ytLoading = ref(false)
const ytChannels = ref<YoutubeChannel[]>([])
const ytSelectedChannelIds = ref<string[]>([])
const yt = reactive({
  title: '',
  description: '',
  privacyStatus: 'public',
  tagsText: '',
})

const ytPlaylistsByChannel: Record<string, YoutubePlaylist[]> = reactive({})
const ytPlaylistsLoading: Record<string, boolean> = reactive({})
const ytSelectedPlaylistIdsByChannel: Record<string, string[]> = reactive({})
const ytChannelDefaults: Record<string, { defaultDescription?: string; defaultTags?: string }> = reactive({})
const ytSavingDefaults: Record<string, boolean> = reactive({})
const ytSavingDefaultsAll = ref(false)

const fbLoading = ref(false)
const fbPages = ref<FbPage[]>([])
const fbSelectedPageIds = ref<string[]>([])
const fbPageDefaults: Record<string, { defaultTitle?: string; defaultDescription?: string; defaultLink?: string; defaultMode?: string }> = reactive({})
const fbSavingDefaults: Record<string, boolean> = reactive({})
const fbSavingDefaultsAll = ref(false)
const fb = reactive({
  mode: 'reel' as 'reel' | 'post',
  title: '',
  description: '',
  link: '',
  publishAt: '',
})

const reelUploader = useReelMultiUpload()

const toIsoUtc7 = (local: string) => {
  const v = (local ?? '').trim()
  if (!v) return ''
  return `${v}:00+07:00`
}

const uploadToCache = async (file: File) => {
  const form = new FormData()
  form.append('file', file, file.name)
  const res = await api.post<any>('admin/media/upload', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 900000,
  } as any)
  return (res?.url ?? '').toString()
}

const getScheduleCaption = () => {
  const a = (reelUploader.fbForm.value?.description ?? '').trim()
  if (a) return a
  const b = (reelUploader.ytForm.value?.description ?? '').trim()
  if (b) return b
  const c = (fb.description ?? '').trim()
  if (c) return c
  const d = (yt.description ?? '').trim()
  if (d) return d
  return ''
}

const createSchedule = async (caption: string) => {
  const whenIso = toIsoUtc7(scheduledAt.value)
  if (!whenIso) throw new Error('Vui lòng chọn giờ lập lịch (UTC+7).')
  if (!common.videoFile) throw new Error('Vui lòng chọn video.')

  const captionFinal = (caption || '').trim()
  if (!captionFinal) throw new Error('Vui lòng nhập nội dung (description) trước khi lập lịch.')

  const t = toast.loading('Đang upload media để lưu lịch...')
  try {
    const videoUrl = await uploadToCache(common.videoFile)
    if (!videoUrl) throw new Error('Upload video thất bại.')

    let thumbUrl = ''
    if (common.thumbFile) {
      thumbUrl = await uploadToCache(common.thumbFile)
    }

    const targets = {
      facebook: fbSelectedPageIds.value.length > 0,
      tiktok: false,
      youtube: ytSelectedChannelIds.value.length > 0,

      fbPageIds: [...(reelUploader.fbSelectedPageIds.value ?? [])],
      fbTitle: (reelUploader.fbForm.value?.title ?? '').toString(),
      fbDescription: (reelUploader.fbForm.value?.description ?? '').toString(),
      fbLink: (reelUploader.fbForm.value?.link ?? '').toString(),

      ytChannelIds: [...(reelUploader.ytSelectedChannelIds.value ?? [])],
      ytPlaylistIdsByChannel: { ...(reelUploader.ytSelectedPlaylistIdsByChannel.value ?? {}) },
      ytTitle: (reelUploader.ytForm.value?.title ?? '').toString(),
      ytDescription: (reelUploader.ytForm.value?.description ?? '').toString(),
      ytTagsText: (reelUploader.ytForm.value?.tagsText ?? '').toString(),
      ytPrivacyStatus: (reelUploader.ytForm.value?.privacyStatus ?? '').toString(),
      ytPublishAt: (reelUploader.ytForm.value?.publishAt ?? '').toString(),
    }

    const assets: Array<{ type: string; url: string; sortOrder: number }> = []
    assets.push({ type: 'video', url: videoUrl, sortOrder: 0 })
    if (thumbUrl) assets.push({ type: 'image', url: thumbUrl, sortOrder: 1 })

    await api.post('admin/scheduled-posts', {
      pageId: null,
      caption: captionFinal,
      scheduledAtLocal: whenIso,
      timezone: 'Asia/Ho_Chi_Minh',
      targets,
      assets,
    })

    t.success('Đã lưu lịch. Đến giờ hệ thống sẽ nhắc bạn.')
  } catch (e: any) {
    t.error(e?.message ?? 'Lưu lịch thất bại.')
    throw e
  }
}

const pollDueSchedules = async () => {
  try {
    const due = (await api.post<any[]>('admin/scheduled-posts/due', {})) as any
    const list = Array.isArray(due) ? due : []
    dueCount.value = list.length
    if (list.length > 0) {
      toast.info(`Có ${list.length} bài đến giờ cần đăng.`)
    }
  } catch {
  }
}

const syncReelUploaderFromForm = () => {
  reelUploader.targets.value = { fb: true, yt: true }
  reelUploader.fbSelectedPageIds.value = [...fbSelectedPageIds.value]
  reelUploader.fbForm.value = { title: fb.title, description: fb.description, link: affiliateLink.value.trim() || fb.link }
  reelUploader.ytSelectedChannelIds.value = [...ytSelectedChannelIds.value]
  reelUploader.ytForm.value = {
    title: yt.title,
    description: yt.description,
    privacyStatus: yt.privacyStatus,
    tagsText: yt.tagsText,
    publishAt: '',
  }
  // Share playlists selection
  reelUploader.ytSelectedPlaylistIdsByChannel.value = { ...ytSelectedPlaylistIdsByChannel }
}

const onSubmitReelPanel = async (input: VideoInput, thumbFile?: File | null) => {
  if (working.value) return
  clearMsg()
  working.value = true
  try {
    if (publishMode.value === 'schedule') {
      const cap = getScheduleCaption()
      await createSchedule(cap)
      return
    }
    const linkToUse = affiliateLink.value.trim() || fb.link.trim()
    if (linkToUse && !reelUploader.fbForm.value.link.trim()) {
      reelUploader.fbForm.value.link = linkToUse
    }
    const result = await reelUploader.submit(input, thumbFile ?? null)

    fbResult.value =
      result.fbFail.length === 0
        ? { ok: true, text: `Facebook: Upload thành công ${result.fbOk}/${reelUploader.fbSelectedPageIds.value.length} page.` }
        : { ok: result.fbOk > 0, text: `Facebook: Thành công ${result.fbOk}/${reelUploader.fbSelectedPageIds.value.length}. Lỗi: ${result.fbFail.join(' | ')}` }

    ytResult.value =
      result.ytFail.length === 0
        ? { ok: true, text: `YouTube: Upload thành công ${result.ytOk}/${reelUploader.ytSelectedChannelIds.value.length} kênh.` }
        : { ok: result.ytOk > 0, text: `YouTube: Thành công ${result.ytOk}/${reelUploader.ytSelectedChannelIds.value.length}. Lỗi: ${result.ytFail.join(' | ')}` }

    setMsg('Đã chạy upload Reel đa nền tảng.', result.fbFail.length || result.ytFail.length ? 'error' : 'success')
  } catch (e: any) {
    setMsg(e?.message ?? 'Upload Reel đa nền tảng thất bại.', 'error')
  } finally {
    working.value = false
  }
}

const ytResult = ref<{ ok: boolean; text: string } | null>(null)
const fbResult = ref<{ ok: boolean; text: string } | null>(null)

const canUpload = computed(() => {
  if (!common.videoFile) return false
  return ytSelectedChannelIds.value.length > 0 || fbSelectedPageIds.value.length > 0
})

const ytChannelOptions = computed(() => ytChannels.value.map((c) => ({ value: c.id, label: c.title })))

const fbPageOptions = computed(() => fbPages.value.map((p) => ({ value: p.id, label: p.name })))

const ytPlaylistOptions = (channelId: string) => {
  const list = ytPlaylistsByChannel[channelId] ?? []
  return list.map((p) => ({ value: p.id, label: p.title }))
}

const updateYtSelectedChannelIds = (ids: string[]) => {
  ytSelectedChannelIds.value = [...ids]
}

const updateFbSelectedPageIds = (ids: string[]) => {
  fbSelectedPageIds.value = [...ids]
}

const updateYtSelectedPlaylistIds = (channelId: string, ids: string[]) => {
  ytSelectedPlaylistIdsByChannel[channelId] = [...ids]
}

const onPickVideo = (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  common.videoFile = file
  if (file) {
    const guess = file.name.replace(/\.[^.]+$/, '')
    if (!yt.title.trim()) yt.title = guess
    if (!fb.title.trim()) fb.title = guess
  }
}

const loadFbPageDefaults = async (pageId: string) => {
  if (!pageId) return
  try {
    const data = await api.get<any>(`facebook/pages/${encodeURIComponent(pageId)}`)
    fbPageDefaults[pageId] = {
      defaultTitle: data.defaultTitle ?? '',
      defaultDescription: data.defaultDescription ?? '',
      defaultLink: data.defaultLink ?? '',
      defaultMode: data.defaultMode ?? '',
    }

    if (!fb.title.trim() && data.defaultTitle) {
      fb.title = data.defaultTitle
    }
    if (!fb.description.trim() && data.defaultDescription) {
      fb.description = data.defaultDescription
    }
    if (!fb.link.trim() && data.defaultLink) {
      fb.link = data.defaultLink
    }
    if (data.defaultMode && (data.defaultMode === 'reel' || data.defaultMode === 'post')) {
      fb.mode = data.defaultMode
    }

    if (!reelUploader.fbForm.value.title.trim() && data.defaultTitle) {
      reelUploader.fbForm.value.title = data.defaultTitle
    }
    if (!reelUploader.fbForm.value.description.trim() && data.defaultDescription) {
      reelUploader.fbForm.value.description = data.defaultDescription
    }
    if (!reelUploader.fbForm.value.link.trim() && data.defaultLink) {
      reelUploader.fbForm.value.link = data.defaultLink
    }
  } catch {
    fbPageDefaults[pageId] = { defaultTitle: '', defaultDescription: '', defaultLink: '', defaultMode: '' }
  }
}

const saveFbPageDefaults = async (pageId: string, silent = false) => {
  if (!pageId) return
  fbSavingDefaults[pageId] = true
  try {
    await api.put(`facebook/pages/${encodeURIComponent(pageId)}/defaults`, {
      defaultTitle: fb.title.trim() || null,
      defaultDescription: fb.description.trim() || null,
      defaultLink: fb.link.trim() || null,
      defaultMode: fb.mode,
    })
    fbPageDefaults[pageId] = {
      defaultTitle: fb.title.trim() || '',
      defaultDescription: fb.description.trim() || '',
      defaultLink: fb.link.trim() || '',
      defaultMode: fb.mode,
    }
    if (!silent) setMsg('Đã lưu thiết lập mặc định cho page.', 'success')
  } catch (e: any) {
    if (e?.status === 404) {
      if (!silent) setMsg('Page chưa được kết nối hoặc đã bị xoá khỏi danh sách. Hãy bấm tải pages/sync lại.', 'error')
    } else {
      if (!silent) setMsg(e?.message ?? 'Không lưu được thiết lập mặc định cho page.', 'error')
    }
    throw e
  } finally {
    fbSavingDefaults[pageId] = false
  }
}

const saveAllFbPageDefaults = async () => {
  const connected = new Set((fbPages.value ?? []).map((p) => p.id))
  const ids = Array.from(new Set(fbSelectedPageIds.value ?? [])).filter((x) => x && connected.has(x))
  if (ids.length === 0) return
  clearMsg()
  fbSavingDefaultsAll.value = true
  let okCount = 0
  const errors: string[] = []
  try {
    for (const pid of ids) {
      try {
        await saveFbPageDefaults(pid, true)
        okCount += 1
      } catch (e: any) {
        if (e?.status === 404) {
          errors.push(`${pid}: not found (page chưa kết nối / cache cũ)`)
        } else {
          errors.push(`${pid}: ${e?.message ?? 'save fail'}`)
        }
      }
    }
  } finally {
    fbSavingDefaultsAll.value = false
  }

  if (errors.length === 0) {
    setMsg(`Đã lưu thiết lập mặc định cho ${okCount}/${ids.length} page.`, 'success')
  } else {
    setMsg(`Lưu mặc định: thành công ${okCount}/${ids.length}. Lỗi: ${errors.join(' | ')}`, 'error')
  }
}

const onPickThumb = (e: Event) => {
  const input = e.target as HTMLInputElement
  common.thumbFile = input.files?.[0] ?? null
}

const loadYoutubeChannels = async () => {
  clearMsg()
  ytLoading.value = true
  try {
    const data = await api.get<any[]>('youtube/channels')
    const items = Array.isArray(data) ? data : []
    ytChannels.value = items.map((x) => ({ id: x.id, title: x.title } as YoutubeChannel))
  } catch (e: any) {
    ytChannels.value = []
    setMsg(e?.message ?? 'Không tải được danh sách kênh YouTube.', 'error')
  } finally {
    ytLoading.value = false
  }
}

const loadYoutubePlaylistsForChannel = async (channelId: string) => {
  if (!channelId) return
  if (ytPlaylistsLoading[channelId]) return

  ytPlaylistsLoading[channelId] = true
  try {
    const data = await api.get<any[]>(`youtube/channels/${encodeURIComponent(channelId)}/playlists`)
    const items = Array.isArray(data) ? data : []
    ytPlaylistsByChannel[channelId] = items.map((x) => ({ id: x.id, title: x.title } as YoutubePlaylist))
    if (!Array.isArray(ytSelectedPlaylistIdsByChannel[channelId])) {
      ytSelectedPlaylistIdsByChannel[channelId] = []
    }
  } catch {
    ytPlaylistsByChannel[channelId] = []
    if (!Array.isArray(ytSelectedPlaylistIdsByChannel[channelId])) {
      ytSelectedPlaylistIdsByChannel[channelId] = []
    }
  } finally {
    ytPlaylistsLoading[channelId] = false
  }
}

const loadChannelDefaults = async (channelId: string) => {
  if (!channelId) return
  try {
    const data = await api.get<any>(`youtube/channels/${encodeURIComponent(channelId)}`)
    ytChannelDefaults[channelId] = {
      defaultDescription: data.defaultDescription ?? '',
      defaultTags: data.defaultTags ?? '',
    }
    // Auto-fill YouTube description and tags if they are empty
    if (!yt.description.trim() && data.defaultDescription) {
      yt.description = data.defaultDescription
    }
    if (!yt.tagsText.trim() && data.defaultTags) {
      yt.tagsText = data.defaultTags
    }

    if (!reelUploader.ytForm.value.description.trim() && data.defaultDescription) {
      reelUploader.ytForm.value.description = data.defaultDescription
    }
    if (!reelUploader.ytForm.value.tagsText.trim() && data.defaultTags) {
      reelUploader.ytForm.value.tagsText = data.defaultTags
    }
  } catch {
    ytChannelDefaults[channelId] = { defaultDescription: '', defaultTags: '' }
  }
}

const saveChannelDefaults = async (channelId: string, silent = false) => {
  if (!channelId) return
  ytSavingDefaults[channelId] = true
  try {
    // Save current YouTube description and tags as defaults for this channel
    await api.put(`youtube/channels/${encodeURIComponent(channelId)}/defaults`, {
      defaultDescription: yt.description.trim() || null,
      defaultTags: yt.tagsText.trim() || null,
    })
    // Update local cache
    ytChannelDefaults[channelId] = {
      defaultDescription: yt.description.trim() || '',
      defaultTags: yt.tagsText.trim() || '',
    }
    if (!silent) setMsg('Đã lưu thiết lập mặc định cho kênh.', 'success')
  } catch (e: any) {
    if (!silent) setMsg(e?.message ?? 'Không lưu được thiết lập mặc định.', 'error')
    throw e
  } finally {
    ytSavingDefaults[channelId] = false
  }
}

const saveAllChannelDefaults = async () => {
  const ids = Array.from(new Set(ytSelectedChannelIds.value ?? [])).filter(Boolean)
  if (ids.length === 0) return
  clearMsg()
  ytSavingDefaultsAll.value = true
  let okCount = 0
  const errors: string[] = []
  try {
    for (const cid of ids) {
      try {
        await saveChannelDefaults(cid, true)
        okCount += 1
      } catch (e: any) {
        errors.push(`${cid}: ${e?.message ?? 'save fail'}`)
      }
    }
  } finally {
    ytSavingDefaultsAll.value = false
  }

  if (errors.length === 0) {
    setMsg(`Đã lưu thiết lập mặc định cho ${okCount}/${ids.length} kênh.`, 'success')
  } else {
    setMsg(`Lưu mặc định: thành công ${okCount}/${ids.length}. Lỗi: ${errors.join(' | ')}`, 'error')
  }
}

const loadFacebookPages = async () => {
  clearMsg()
  fbLoading.value = true
  try {
    const data = await api.get<any[]>('facebook/pages')
    const items = Array.isArray(data) ? data : []
    fbPages.value = items.map((x) => ({ id: x.id, name: x.name } as FbPage))
  } catch (e: any) {
    fbPages.value = []
    setMsg(e?.message ?? 'Không tải được danh sách Facebook pages.', 'error')
  } finally {
    fbLoading.value = false
  }
}

const uploadYoutube = async () => {
  ytResult.value = null
  if (!common.videoFile) return
  if (ytSelectedChannelIds.value.length === 0) return

  const ytTitleLen = (yt.title ?? '').trim().length
  const ytDescLen = (yt.description ?? '').trim().length
  if (ytTitleLen > 100) {
    ytResult.value = { ok: false, text: `YouTube: Tiêu đề tối đa 100 ký tự (hiện tại ${ytTitleLen}).` }
    return
  }
  if (ytDescLen > 5000) {
    ytResult.value = { ok: false, text: `YouTube: Mô tả tối đa 5000 ký tự (hiện tại ${ytDescLen}).` }
    return
  }

  const tags = yt.tagsText
    .split(',')
    .map((x) => x.trim())
    .filter(Boolean)

  let okCount = 0
  const errors: string[] = []

  for (const channelId of ytSelectedChannelIds.value) {
    try {
      const formData = new FormData()
      formData.append('file', common.videoFile)
      formData.append('title', yt.title)
      formData.append('description', yt.description)
      formData.append('privacyStatus', yt.privacyStatus)
      tags.forEach((t) => formData.append('tags', t))

      const playlistIds = ytSelectedPlaylistIdsByChannel[channelId] ?? []
      playlistIds.forEach((pid) => formData.append('playlistIds', pid))

      await api.post<void>(`youtube/channels/${encodeURIComponent(channelId)}/videos/upload`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 180000,
      } as any)

      okCount += 1
    } catch (e: any) {
      errors.push(`${channelId}: ${e?.message ?? 'upload fail'}`)
    }
  }

  if (errors.length === 0) {
    ytResult.value = { ok: true, text: `YouTube: Upload thành công ${okCount}/${ytSelectedChannelIds.value.length} kênh.` }
  } else {
    ytResult.value = { ok: okCount > 0, text: `YouTube: Thành công ${okCount}/${ytSelectedChannelIds.value.length}. Lỗi: ${errors.join(' | ')}` }
  }
}

const uploadFacebook = async () => {
  fbResult.value = null
  if (!common.videoFile) return
  if (fbSelectedPageIds.value.length === 0) return

  let okCount = 0
  const errors: string[] = []

  for (const pageId of fbSelectedPageIds.value) {
    try {
      if (fb.mode === 'reel') {
        const form = new FormData()
        form.append('video', common.videoFile)
        form.append('pageId', pageId)
        if (fb.title.trim()) form.append('title', fb.title)
        if (fb.description.trim()) form.append('description', fb.description)
        const linkToUse = affiliateLink.value.trim() || fb.link.trim()
        if (linkToUse) form.append('link', linkToUse)

        await api.post('facebook/reels', form, {
          headers: { 'Content-Type': 'multipart/form-data' },
          timeout: 180000,
        } as any)
      } else {
        await api.post('facebook/posts', {
          pageId,
          message: fb.description || fb.title,
          link: affiliateLink.value.trim() || fb.link || null,
          publishAt: toIsoUtc7(fb.publishAt) || null,
        })
      }

      okCount += 1
    } catch (e: any) {
      errors.push(`${pageId}: ${e?.message ?? 'upload fail'}`)
    }
  }

  if (errors.length === 0) {
    fbResult.value = { ok: true, text: `Facebook: Upload thành công ${okCount}/${fbSelectedPageIds.value.length} page.` }
  } else {
    fbResult.value = { ok: okCount > 0, text: `Facebook: Thành công ${okCount}/${fbSelectedPageIds.value.length}. Lỗi: ${errors.join(' | ')}` }
  }
}

const uploadAll = async () => {
  if (!canUpload.value) return
  clearMsg()
  working.value = true
  try {
    if (publishMode.value === 'schedule') {
      const cap = getScheduleCaption()
      await createSchedule(cap)
      setMsg('Đã lưu lịch. Đến giờ hệ thống sẽ nhắc bạn.', 'success')
      return
    }
    if (fb.mode === 'reel') {
      if (!common.videoFile) throw new Error('Vui lòng chọn video file.')
      syncReelUploaderFromForm()
      const input: VideoInput = { kind: 'file', videoFile: common.videoFile }
      const result = await reelUploader.submit(input, common.thumbFile)

      fbResult.value =
        result.fbFail.length === 0
          ? { ok: true, text: `Facebook: Upload thành công ${result.fbOk}/${reelUploader.fbSelectedPageIds.value.length} page.` }
          : { ok: result.fbOk > 0, text: `Facebook: Thành công ${result.fbOk}/${reelUploader.fbSelectedPageIds.value.length}. Lỗi: ${result.fbFail.join(' | ')}` }

      ytResult.value =
        result.ytFail.length === 0
          ? { ok: true, text: `YouTube: Upload thành công ${result.ytOk}/${reelUploader.ytSelectedChannelIds.value.length} kênh.` }
          : { ok: result.ytOk > 0, text: `YouTube: Thành công ${result.ytOk}/${reelUploader.ytSelectedChannelIds.value.length}. Lỗi: ${result.ytFail.join(' | ')}` }

      setMsg('Đã chạy upload Reel đa nền tảng.', result.fbFail.length || result.ytFail.length ? 'error' : 'success')
    } else {
      await uploadYoutube()
      await uploadFacebook()
      setMsg('Đã chạy upload đa nền tảng.', 'success')
    }
  } catch (e: any) {
    setMsg(e?.message ?? 'Upload đa nền tảng thất bại.', 'error')
  } finally {
    working.value = false
  }
}

const resetForm = () => {
  clearMsg()
  ytResult.value = null
  fbResult.value = null
  common.videoFile = null
  common.thumbFile = null
  affiliateLink.value = ''
  ytSelectedChannelIds.value = []
  fbSelectedPageIds.value = []
  yt.title = ''
  yt.description = ''
  yt.privacyStatus = 'public'
  yt.tagsText = ''
  fb.mode = 'reel'
  fb.title = ''
  fb.description = ''
  fb.link = ''
  fb.publishAt = ''
}

onMounted(async () => {
  await Promise.all([loadYoutubeChannels(), loadFacebookPages()])

  void pollDueSchedules()
  duePollTimer = window.setInterval(() => {
    void pollDueSchedules()
  }, 60000)

  // Load cached selections
  const cachedYtChannels = localStorage.getItem('platform_ytSelectedChannelIds')
  if (cachedYtChannels) {
    try {
      ytSelectedChannelIds.value = JSON.parse(cachedYtChannels)
    } catch {}
  }
  const cachedFbPages = localStorage.getItem('platform_fbSelectedPageIds')
  if (cachedFbPages) {
    try {
      fbSelectedPageIds.value = JSON.parse(cachedFbPages)
    } catch {}
  }
})

onBeforeUnmount(() => {
  if (duePollTimer != null) {
    window.clearInterval(duePollTimer)
    duePollTimer = null
  }
})

watch(
  ytSelectedChannelIds,
  async (ids) => {
    const uniq = Array.from(new Set(ids ?? []))
    for (const cid of uniq) {
      // Ensure defaults object exists before loading
      if (!ytChannelDefaults[cid]) {
        ytChannelDefaults[cid] = { defaultDescription: '', defaultTags: '' }
      }
      await loadYoutubePlaylistsForChannel(cid)
      await loadChannelDefaults(cid)
      // Ensure reactive arrays exist
      if (!Array.isArray(ytSelectedPlaylistIdsByChannel[cid])) {
        ytSelectedPlaylistIdsByChannel[cid] = []
      }
    }

    // Cleanup selection for removed channels
    const keep = new Set(uniq)
    Object.keys(ytSelectedPlaylistIdsByChannel).forEach((cid) => {
      if (!keep.has(cid)) delete ytSelectedPlaylistIdsByChannel[cid]
    })
    Object.keys(ytPlaylistsByChannel).forEach((cid) => {
      if (!keep.has(cid)) delete ytPlaylistsByChannel[cid]
    })
    Object.keys(ytPlaylistsLoading).forEach((cid) => {
      if (!keep.has(cid)) delete ytPlaylistsLoading[cid]
    })
    Object.keys(ytChannelDefaults).forEach((cid) => {
      if (!keep.has(cid)) delete ytChannelDefaults[cid]
    })
    Object.keys(ytSavingDefaults).forEach((cid) => {
      if (!keep.has(cid)) delete ytSavingDefaults[cid]
    })
    // Save to cache
    localStorage.setItem('platform_ytSelectedChannelIds', JSON.stringify(ytSelectedChannelIds.value))
  },
  { deep: true },
)

watch(
  fbSelectedPageIds,
  (ids) => {
    localStorage.setItem('platform_fbSelectedPageIds', JSON.stringify(fbSelectedPageIds.value))
  },
  { deep: true },
)

watch(
  fbSelectedPageIds,
  async (ids) => {
    const uniq = Array.from(new Set(ids ?? []))
    for (const pid of uniq) {
      if (!fbPageDefaults[pid]) {
        fbPageDefaults[pid] = { defaultTitle: '', defaultDescription: '', defaultLink: '', defaultMode: '' }
      }
      await loadFbPageDefaults(pid)
    }

    const keep = new Set(uniq)
    Object.keys(fbPageDefaults).forEach((pid) => {
      if (!keep.has(pid)) delete fbPageDefaults[pid]
    })
    Object.keys(fbSavingDefaults).forEach((pid) => {
      if (!keep.has(pid)) delete fbSavingDefaults[pid]
    })
  },
  { deep: true },
)
</script>

<style scoped>
.card {
  background: #fff;
  border-radius: 10px;
  padding: 20px;
  margin-bottom: 20px;
}
.card-title {
  font-weight: 700;
  margin-bottom: 6px;
}
.muted {
  color: #999;
  font-size: 13px;
}
.msg {
  margin-top: 10px;
  padding: 10px 12px;
  border-radius: 10px;
  font-size: 13px;
}
.msg.success {
  background: #ecfdf5;
  border: 1px solid #a7f3d0;
  color: #065f46;
}
.msg.error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #7f1d1d;
}
.section-title {
  font-weight: 700;
  margin: 16px 0 10px;
}
.platform-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.platform-col {
  border: 1px solid #eee;
  border-radius: 12px;
  padding: 12px;
  background: #fafafa;
}
.platform-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}
.platform-name {
  font-weight: 700;
}
.form.two-col {
  display: flex  ;
  gap: 12px;
}
.field label {
  display: block;
  font-weight: 600;
  margin-bottom: 6px;
}
.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
  font-size: 12px;
}
.playlist-per-channel {
  border: 1px dashed #e5e7eb;
  border-radius: 12px;
  padding: 10px;
  background: #fff;
  margin-bottom: 10px;
}
.playlist-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 8px;
}
.input,
.textarea,
select.input {
  width: 100%;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 10px 12px;
  background: #fff;
}
.actions {
  display: flex;
  gap: 10px;
}
.result {
  margin-top: 10px;
  padding: 10px 12px;
  border-radius: 10px;
  font-size: 13px;
}
.result.ok {
  background: #ecfdf5;
  border: 1px solid #a7f3d0;
  color: #065f46;
}
.result.error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #7f1d1d;
}
.channel-defaults {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}
.channel-defaults .field {
  display: flex;
  flex-direction: column;
}
.channel-defaults label.small {
  font-size: 12px;
  margin-bottom: 4px;
}
.channel-defaults .input.small,
.channel-defaults .textarea.small {
  padding: 6px 8px;
  font-size: 12px;
}
.channel-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #e5e7eb;
}
.label-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
}
.char-counter {
  font-size: 12px;
  opacity: 0.75;
}

.aff-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 14px;
  z-index: 50;
}

.modal {
  width: 100%;
  max-width: 900px;
  padding: 0;
  overflow: hidden;
}

.modal-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 14px;
  border-bottom: 1px solid #eee;
}

.modal-title {
  font-weight: 800;
}

.modal-body {
  padding: 14px;
  max-height: 70vh;
  overflow: auto;
}

.modal-foot {
  padding: 12px 14px;
  border-top: 1px solid #eee;
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.picker-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.picker-item {
  border: 1px solid #eee;
  background: #fff;
  border-radius: 10px;
  padding: 10px;
  display: flex;
  gap: 10px;
  text-align: left;
  cursor: pointer;
}

.picker-item:hover {
  border-color: #d0d5dd;
  background: #fafafa;
}

.picker-item.active {
  border-color: #f26f3b;
  box-shadow: 0 0 0 3px rgba(242, 111, 59, 0.12);
}

.picker-thumb {
  width: 56px;
  height: 56px;
  border-radius: 10px;
  object-fit: cover;
  background: #f2f2f2;
  flex: 0 0 auto;
}

.picker-meta {
  min-width: 0;
  flex: 1;
}

.picker-name {
  font-weight: 700;
  font-size: 13px;
  line-height: 1.3;
  margin-bottom: 2px;
}

.picker-toolbar {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}

.picker-paging {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #eee;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.paging-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border-radius: 999px;
  font-size: 12px;
  border: 1px solid #eee;
}

.pill.ok {
  background: #ecfdf3;
  border-color: #abefc6;
  color: #067647;
}

.picker-sub {
  font-size: 12px;
}

.truncate {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
@media (max-width: 980px) {
  .platform-grid {
    grid-template-columns: 1fr;
  }
  .form.two-col {
    grid-template-columns: 1fr;
  }
  .aff-row {
    flex-wrap: wrap;
  }
  .picker-list {
    grid-template-columns: 1fr;
  }
}
</style>
