<template>
  <div class="box">
    <div class="head">
      <div class="muted">Nền tảng</div>
    </div>
    <div class="row">
      <label class="item"><input type="checkbox" v-model="u.targets.yt" :disabled="disabled" /> <span>YouTube</span></label>
      <label class="item"><input type="checkbox" v-model="u.targets.fb" :disabled="disabled" /> <span>Facebook Reel</span></label>
      
    </div>
  </div>

  <div class="platform-scroll">
  <div class="col">
      <div v-if="u.targets.yt" class="box">
        <div class="head">
          <div class="muted">YouTube</div>
          <div class="actions">
            <button class="btn tiny secondary" type="button" :disabled="disabled || u.ytLoading" @click="u.loadYtChannels">
              {{ u.ytLoading ? '...' : 'Tải kênh' }}
            </button>
          </div>
        </div>
      <div class="grid" style="margin-top: 10px">
        <label>
          <SelectMultiTags
            :model-value="ytSelectedChannelIdsModel"
            :options="ytChannelOptions"
            placeholder="Chọn kênh..."
            :disabled="disabled || u.ytLoading"
            @update:model-value="updateYtSelectedChannelIds"
          />
        </label>
      </div>
      <div v-for="cid in u.ytSelectedChannelIds" :key="cid" style="margin-top: 10px">
        <div class="grid" style="margin-top: 0">
          <label>
            <SelectMultiTags
              :model-value="u.ytSelectedPlaylistIdsByChannel[cid] ?? []"
              :options="ytPlaylistOptions(cid)"
              placeholder="Chọn playlist..."
              :disabled="disabled || u.ytPlaylistsLoading[cid] || (u.ytPlaylistsByChannel[cid] ?? []).length === 0"
              @update:model-value="(v) => updateYtSelectedPlaylistIds(cid, v)"
            />
          </label>
          <div v-if="(u.ytPlaylistsByChannel[cid] ?? []).length === 0" class="muted">Không có playlist.</div>
        </div>
      </div>

      <div class="grid" style="margin-top: 10px">
        <label>
          <div class="input-wrap">
            <input v-model="u.ytForm.title" class="input has-suffix" type="text" placeholder="Tiêu đề video..." :disabled="disabled" />
            <div class="input-suffix" :class="{ danger: ytTitleTooLong }">{{ ytTitleLen }}/{{ YT_TITLE_MAX }}</div>
          </div>
        </label>
        <label>
          <textarea v-model="u.ytForm.description" class="textarea" rows="4" placeholder="Mô tả..." :disabled="disabled" />
        </label>
        <label>
          <input v-model="u.ytForm.tagsText" class="input" type="text" placeholder="tag1, tag2, ..." :disabled="disabled" />
        </label>
        <label>
          <div class="lbl">Privacy</div>
          <select v-model="u.ytForm.privacyStatus" class="input" :disabled="disabled">
            <option value="public">public</option>
            <option value="unlisted">unlisted</option>
            <option value="private">private</option>
          </select>
        </label>
        <label>
          <div class="lbl">PublishAt (ISO datetime, tuỳ chọn)</div>
          <input v-model="u.ytForm.publishAt" class="input" type="text" placeholder="2026-03-14T10:30:00+07:00" :disabled="disabled" />
        </label>
        
      
      </div>
      </div>
    </div>
    <div class="col">
      <div v-if="u.targets.fb" class="box">
        <div class="head">
          <div class="muted">Facebook pages</div>
          <div class="actions">
            <button class="btn tiny secondary" type="button" :disabled="disabled || u.fbLoading" @click="u.loadFbPages">
              {{ u.fbLoading ? '...' : 'Tải' }}
            </button>
          </div>
        </div>

        <div class="grid" style="margin-top: 0">
          <label>
            <SelectMultiTags
              :model-value="fbSelectedPageIdsModel"
              :options="fbPageOptions"
              placeholder="Chọn page..."
              :disabled="disabled || u.fbLoading"
              @update:model-value="updateFbSelectedPageIds"
            />
          </label>
        </div>

        <div class="grid">
          <label>
            <input v-model="u.fbForm.title" class="input" type="text" placeholder="Tiêu đề..." :disabled="disabled" />
          </label>
          <label>
            <textarea v-model="u.fbForm.description" class="textarea" rows="4" placeholder="Nhập caption..." :disabled="disabled" />
          </label>
        </div>
      </div>

    </div>

    
  </div>

  <div class="box" style="margin-top: 12px">
    <div class="head">
      <div class="muted">Video</div>
      <div class="actions">
        <button class="btn tiny secondary" type="button" :disabled="disabled" @click="pickLocalVideo">
          Chọn video
        </button>
      </div>
    </div>

    <input ref="videoFileInputEl" type="file" accept="video/*" style="display: none" :disabled="disabled" @change="onPickLocalVideo" />

    <div v-if="selectedVideoUrlValue.trim()" class="muted" style="margin-top: 6px">Đang chọn: {{ selectedVideoUrlValue }}</div>
    <div v-else-if="localVideoFile" class="muted" style="margin-top: 6px">Đang chọn file: {{ localVideoFile.name }}</div>
    <div v-else-if="props.videoInput.kind === 'file' && props.videoInput.videoFile" class="muted" style="margin-top: 6px">
      Đang dùng file: {{ props.videoInput.videoFile.name }}
    </div>
    <div v-else class="muted" style="margin-top: 6px">Chưa chọn video.</div>

    <div class="video-list" style="margin-top: 10px">
      <button
        v-for="v in videoOptions"
        :key="v.key"
        class="video-item"
        type="button"
        :class="{ active: isVideoOptionActive(v) }"
        @click="selectVideoOption(v)"
      >
        {{ v.label }}
      </button>
      <div v-if="videoOptions.length === 0" class="muted">Không có video.</div>
    </div>
  </div>

  <div class="footer">
    <button class="btn" type="button" :disabled="disabled || !u.canSubmit || !resolvedInput" @click="onSubmit">{{ disabled ? 'Đang chạy...' : 'Chạy upload' }}</button>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, proxyRefs, ref, watch, watchEffect } from 'vue'
import { useReelMultiUpload, type VideoInput } from './useReelMultiUpload'
import SelectMultiTags, { type SelectMultiTagsOption } from './SelectMultiTags.vue'

const props = defineProps<{
  disabled: boolean
  imageUrls: string[]
  videoUrls: string[]
  defaultCaption?: string
  videoInput: VideoInput
  thumbFile?: File | null
  u: ReturnType<typeof useReelMultiUpload>
}>()

const emit = defineEmits<{
  (e: 'submit', input: VideoInput, thumbFile?: File | null): void
}>()

const u = proxyRefs(props.u) as ReturnType<typeof useReelMultiUpload>

const YT_TITLE_MAX = 100
const ytTitleLen = computed(() => (u.ytForm?.title ?? '').toString().length)
const ytTitleTooLong = computed(() => ytTitleLen.value > YT_TITLE_MAX)

const didAutoFillFbDesc = ref(false)
const didAutoFillYtDesc = ref(false)

const fbSelectedPageIdsModel = computed<string[]>({
  get: () => [...(props.u.fbSelectedPageIds.value ?? [])],
  set: (value) => {
    props.u.fbSelectedPageIds.value = [...value]
  },
})

const ytSelectedChannelIdsModel = computed<string[]>({
  get: () => [...(props.u.ytSelectedChannelIds.value ?? [])],
  set: (value) => {
    props.u.ytSelectedChannelIds.value = [...value]
  },
})

const updateFbSelectedPageIds = (value: string[]) => {
  props.u.fbSelectedPageIds.value = [...value]
}

const updateYtSelectedChannelIds = (value: string[]) => {
  props.u.ytSelectedChannelIds.value = [...value]

  for (const cid of value ?? []) {
    const loaded = (u.ytPlaylistsByChannel?.[cid] ?? []).length > 0
    const loading = Boolean(u.ytPlaylistsLoading?.[cid])
    if (!loaded && !loading) {
      void u.loadYtPlaylists(cid)
    }
  }
}

const updateYtSelectedPlaylistIds = (channelId: string, value: string[]) => {
  const cid = (channelId ?? '').trim()
  if (!cid) return
  u.ytSelectedPlaylistIdsByChannel[cid] = [...(value ?? [])]
}

const ytPlaylistOptions = (channelId: string): SelectMultiTagsOption[] => {
  const cid = (channelId ?? '').trim()
  if (!cid) return []
  return (u.ytPlaylistsByChannel?.[cid] ?? []).map((p: any) => ({ value: p.id, label: p.title }))
}

const fbPageOptions = computed<SelectMultiTagsOption[]>(() => (u.fbPages ?? []).map((p: any) => ({ value: p.id, label: p.name })))

const ytChannelOptions = computed<SelectMultiTagsOption[]>(() =>
  (u.ytChannels ?? [])
    .filter((c: any) => c.isAuthorized)
    .map((c: any) => ({ value: c.id, label: c.title })),
)

const selectedVideoUrl = ref('')
const useUrlOverride = ref(false)
const localVideoFile = ref<File | null>(null)
const selectedVideoKind = ref<'url' | 'file'>('url')
const videoFileInputEl = ref<HTMLInputElement | null>(null)

const selectedVideoUrlValue = computed(() => (selectedVideoUrl.value ?? '').toString())

type VideoOption =
  | { key: string; kind: 'url'; label: string; url: string }
  | { key: string; kind: 'file'; label: string; file: File }

const videoOptions = computed<VideoOption[]>(() => {
  const list: VideoOption[] = []
  const f = localVideoFile.value
  if (f) {
    list.push({
      key: `file:${f.name}:${f.size}:${f.lastModified}`,
      kind: 'file',
      label: `FILE: ${f.name}`,
      file: f,
    })
  }

  for (const v of props.videoUrls ?? []) {
    const url = (v ?? '').toString().trim()
    if (!url) continue
    list.push({ key: `url:${url}`, kind: 'url', label: url, url })
  }

  return list
})

const isVideoOptionActive = (opt: VideoOption) => {
  if (opt.kind === 'file') {
    const f = localVideoFile.value
    if (!f) return false
    return selectedVideoKind.value === 'file' && f === opt.file
  }
  return selectedVideoKind.value === 'url' && (selectedVideoUrlValue.value || '').trim() === (opt.url || '').trim()
}

const selectVideoOption = (opt: VideoOption) => {
  if (opt.kind === 'file') {
    localVideoFile.value = opt.file
    selectedVideoKind.value = 'file'
    useUrlOverride.value = false
    selectedVideoUrl.value = ''
    return
  }

  selectedVideoKind.value = 'url'
  selectedVideoUrl.value = (opt.url ?? '').toString().trim()
  useUrlOverride.value = Boolean(selectedVideoUrl.value.trim())
}

const pickLocalVideo = () => {
  if (props.disabled) return
  const el = videoFileInputEl.value
  if (!el) return
  el.value = ''
  el.click()
}

const onPickLocalVideo = (e: Event) => {
  const input = e.target as HTMLInputElement | null
  const f = input?.files?.[0] ?? null
  if (!f) return
  localVideoFile.value = f
  selectedVideoKind.value = 'file'
  useUrlOverride.value = false
  selectedVideoUrl.value = ''
}

const selectVideoUrl = (url: string) => {
  selectedVideoUrl.value = (url ?? '').toString().trim()
  if (selectedVideoUrl.value.trim()) useUrlOverride.value = true
}

const autoLoad = async () => {
  if (u.targets.fb && (u.fbPages?.length ?? 0) === 0 && !u.fbLoading) {
    await u.loadFbPages()
  }
  if (u.targets.yt && (u.ytChannels?.length ?? 0) === 0 && !u.ytLoading) {
    await u.loadYtChannels()
  }
}

onMounted(() => {
  void autoLoad()
})

watch(
  () => [u.targets.fb, u.targets.yt],
  () => {
    void autoLoad()
  },
)

watch(
  () => [...(u.ytSelectedChannelIds ?? [])],
  (ids) => {
    for (const cid of ids ?? []) {
      const loaded = (u.ytPlaylistsByChannel?.[cid] ?? []).length > 0
      const loading = Boolean(u.ytPlaylistsLoading?.[cid])
      if (!loaded && !loading) {
        void u.loadYtPlaylists(cid)
      }
    }
  },
  { immediate: true },
)

watchEffect(() => {
  const caption = (props.defaultCaption ?? '').toString()
  if (!caption.trim()) return

  const fbDesc = (u.fbForm?.description ?? '').toString()
  if (!didAutoFillFbDesc.value) {
    if (caption) {
      u.fbForm.description = caption
      didAutoFillFbDesc.value = true
    } else {
      didAutoFillFbDesc.value = true
    }
  }

  const ytDesc = (u.ytForm?.description ?? '').toString()
  if (!didAutoFillYtDesc.value) {
    if (caption) {
      u.ytForm.description = caption
      didAutoFillYtDesc.value = true
    } else {
      didAutoFillYtDesc.value = true
    }
  }
})

watch(
  () => [props.videoInput.kind, props.videoInput.kind === 'file' ? Boolean(props.videoInput.videoFile) : false, (props.videoUrls ?? []).join('|')],
  () => {
    const hasFile = props.videoInput.kind === 'file' && Boolean(props.videoInput.videoFile)
    if (selectedVideoUrl.value.trim()) return

    if (localVideoFile.value) return

    const urlFromProp = ((props.videoInput as any)?.videoUrl ?? '').toString().trim()
    if (urlFromProp) {
      selectedVideoUrl.value = urlFromProp
      useUrlOverride.value = true
      return
    }

    if (hasFile) return

    const first = (props.videoUrls ?? [])[0]
    if (first) {
      selectedVideoUrl.value = (first ?? '').toString().trim()
      useUrlOverride.value = true
      selectedVideoKind.value = 'url'
    }
  },
  { immediate: true },
)

const resolvedInput = computed<VideoInput | null>(() => {
  if (selectedVideoKind.value === 'file' && localVideoFile.value) {
    return { kind: 'file', videoFile: localVideoFile.value }
  }

  const v = ((props.videoUrl as any) ?? selectedVideoUrl.value ?? '').toString().trim()
  if (useUrlOverride.value && v) return { kind: 'url', videoUrl: v }

  const file = ((props.videoInput as any)?.videoFile ?? null) as File | null
  if (file) return { kind: 'file', videoFile: file }

  if (v) return { kind: 'url', videoUrl: v }

  return null
})

const onSubmit = async () => {
  if (!resolvedInput.value) return
  emit('submit', resolvedInput.value, props.thumbFile ?? null)
}
</script>

<style scoped>
.box {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 10px;
  background: #fff;
}

.platform-scroll {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  padding-bottom: 8px;
  margin-top: 10px;
}

.col {
  display: grid;
  gap: 12px;
  flex: 0 0 520px;
  min-width: 380px;
}

.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 8px;
}

.row {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
}

.item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.actions {
  display: flex;
  gap: 8px;
}

.list {
  display: grid;
  gap: 8px;
}

.list-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.grid {
  display: grid;
  gap: 12px;
  margin-top: 10px;
}

.video-list {
  display: grid;
  gap: 8px;
}

.video-item {
  text-align: left;
  border: 1px solid #e5e7eb;
  background: #fff;
  border-radius: 10px;
  padding: 8px 10px;
  cursor: pointer;
  font-size: 12px;
  color: #111827;
  word-break: break-all;
}

.video-item.active {
  border-color: #111827;
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

.textarea {
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 10px 12px;
  outline: none;
  width: 100%;
  resize: vertical;
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

.btn.tiny {
  height: 28px;
  padding: 0 10px;
  border-radius: 10px;
  font-size: 12px;
}

.muted {
  color: #667085;
  font-size: 13px;
}

.input-wrap {
  position: relative;
}

.input.has-suffix {
  padding-right: 64px;
}

.input-suffix {
  position: absolute;
  top: 50%;
  right: 10px;
  transform: translateY(-50%);
  font-size: 12px;
  color: #667085;
  pointer-events: none;
}

.input-suffix.danger {
  color: #b91c1c;
  font-weight: 600;
}

.preview {
  margin-top: 10px;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  overflow: hidden;
  background: #000;
}

.player {
  width: 100%;
  max-height: 320px;
  display: block;
}

.thumb-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
}

.thumb {
  border: 1px solid #e5e7eb;
  background: #fff;
  border-radius: 10px;
  padding: 0;
  overflow: hidden;
  cursor: pointer;
}

.thumb img {
  width: 100%;
  aspect-ratio: 1 / 1;
  object-fit: cover;
  display: block;
}

.thumb.active {
  border-color: #111827;
}

.footer {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 560px) {
  .col {
    flex-basis: 90vw;
    min-width: 90vw;
  }
}
</style>
