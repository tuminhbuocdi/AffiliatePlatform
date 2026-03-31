import { computed, ref, watch } from 'vue'
import api from '@/infrastructure/http/apiClient'

export type FbPage = { id: string; name: string }
export type YtChannel = { id: string; title: string; thumbnailUrl?: string | null; isAuthorized: boolean }
export type YtPlaylist = { id: string; title: string }

export type ReelTargets = { fb: boolean; yt: boolean }

export type YtForm = {
  title: string
  description: string
  privacyStatus: 'public' | 'unlisted' | 'private'
  tagsText: string
  publishAt: string
}

export type FbForm = {
  title: string
  description: string
  link: string
}

export type VideoInput =
  | { kind: 'url'; videoUrl: string }
  | { kind: 'file'; videoFile: File | null }

export type SubmitResult = {
  fbOk: number
  fbFail: string[]
  ytOk: number
  ytFail: string[]
}

export const useReelMultiUpload = () => {
  const LS_FB_PAGES = 'reel_fbSelectedPageIds'
  const LS_YT_CHANNELS = 'reel_ytSelectedChannelIds'
  const LS_FB_FORM = 'reel_fbForm'
  const LS_YT_FORM = 'reel_ytForm'

  const targets = ref<ReelTargets>({ fb: true, yt: true })

  const fbPages = ref<FbPage[]>([])
  const fbLoading = ref(false)
  const fbSelectedPageIds = ref<string[]>([])
  const fbForm = ref<FbForm>({ title: '', description: '', link: '' })

  const fbPageDefaultsById = ref<Record<string, { defaultTitle: string; defaultDescription: string; defaultLink: string }>>({})
  const fbPageDefaultsLoading = ref<Record<string, boolean>>({})

  const ytChannels = ref<YtChannel[]>([])
  const ytLoading = ref(false)
  const ytSelectedChannelIds = ref<string[]>([])
  const ytForm = ref<YtForm>({ title: '', description: '', privacyStatus: 'public', tagsText: '', publishAt: '' })

  const ytChannelDefaultsById = ref<Record<string, { defaultDescription: string; defaultTags: string }>>({})
  const ytChannelDefaultsLoading = ref<Record<string, boolean>>({})

  const ytThumbUrl = ref('')
  const ytPlaylistsByChannel = ref<Record<string, YtPlaylist[]>>({})
  const ytPlaylistsLoading = ref<Record<string, boolean>>({})
  const ytSelectedPlaylistIdsByChannel = ref<Record<string, string[]>>({})

  const ytChannelTitle = (cid: string) => ytChannels.value.find((x) => x.id === cid)?.title ?? cid

  const loadPersisted = () => {
    try {
      const rawFb = localStorage.getItem(LS_FB_PAGES)
      if (rawFb) {
        const v = JSON.parse(rawFb)
        if (Array.isArray(v)) fbSelectedPageIds.value = v.filter((x) => typeof x === 'string')
      }
    } catch {
      // ignore
    }
    try {
      const rawYt = localStorage.getItem(LS_YT_CHANNELS)
      if (rawYt) {
        const v = JSON.parse(rawYt)
        if (Array.isArray(v)) ytSelectedChannelIds.value = v.filter((x) => typeof x === 'string')
      }
    } catch {
      // ignore
    }

    try {
      const raw = localStorage.getItem(LS_FB_FORM)
      if (raw) {
        const v = JSON.parse(raw)
        const title = (v?.title ?? '').toString()
        const description = (v?.description ?? '').toString()
        const link = (v?.link ?? '').toString()
        fbForm.value = { title, description, link }
      }
    } catch {
      // ignore
    }

    try {
      const raw = localStorage.getItem(LS_YT_FORM)
      if (raw) {
        const v = JSON.parse(raw)
        const title = (v?.title ?? '').toString()
        const description = (v?.description ?? '').toString()
        const tagsText = (v?.tagsText ?? '').toString()
        const publishAt = (v?.publishAt ?? '').toString()
        const privacyStatusRaw = (v?.privacyStatus ?? '').toString()
        const privacyStatus = privacyStatusRaw === 'private' || privacyStatusRaw === 'unlisted' || privacyStatusRaw === 'public' ? privacyStatusRaw : 'public'
        ytForm.value = { title, description, privacyStatus, tagsText, publishAt }
      }
    } catch {
      // ignore
    }
  }

  const loadFbPageDefaults = async (pageId: string) => {
    const pid = (pageId ?? '').trim()
    if (!pid) return
    if (fbPageDefaultsById.value[pid]) return
    if (fbPageDefaultsLoading.value[pid]) return

    fbPageDefaultsLoading.value = { ...fbPageDefaultsLoading.value, [pid]: true }
    try {
      const data = await api.get<any>(`facebook/pages/${encodeURIComponent(pid)}`)
      fbPageDefaultsById.value = {
        ...fbPageDefaultsById.value,
        [pid]: {
          defaultTitle: (data?.defaultTitle ?? '').toString(),
          defaultDescription: (data?.defaultDescription ?? '').toString(),
          defaultLink: (data?.defaultLink ?? '').toString(),
        },
      }

      const d = fbPageDefaultsById.value[pid] ?? { defaultTitle: '', defaultDescription: '', defaultLink: '' }
      if (!fbForm.value.title.trim() && d.defaultTitle) fbForm.value.title = d.defaultTitle
      if (!fbForm.value.description.trim() && d.defaultDescription) fbForm.value.description = d.defaultDescription
      if (!fbForm.value.link.trim() && d.defaultLink) fbForm.value.link = d.defaultLink
    } catch {
      fbPageDefaultsById.value = {
        ...fbPageDefaultsById.value,
        [pid]: { defaultTitle: '', defaultDescription: '', defaultLink: '' },
      }
    } finally {
      fbPageDefaultsLoading.value = { ...fbPageDefaultsLoading.value, [pid]: false }
    }
  }

  loadPersisted()

  const loadFbPages = async () => {
    fbLoading.value = true
    try {
      const data = await api.get<any[]>('facebook/pages')
      const items = Array.isArray(data) ? data : []
      fbPages.value = items.map((x) => ({ id: x.id, name: x.name } as FbPage))

      // Filter persisted selections by existing pages
      const ids = new Set(fbPages.value.map((x) => x.id))
      fbSelectedPageIds.value = (fbSelectedPageIds.value ?? []).filter((x) => ids.has(x))
    } catch {
      fbPages.value = []
    } finally {
      fbLoading.value = false
    }
  }

  const loadYtChannels = async () => {
    ytLoading.value = true
    try {
      const data = await api.get<YtChannel[]>('youtube/channels')
      ytChannels.value = Array.isArray(data) ? data : []

      // Filter persisted selections by authorized channels
      const allowed = new Set(ytChannels.value.filter((x) => x.isAuthorized).map((x) => x.id))
      ytSelectedChannelIds.value = (ytSelectedChannelIds.value ?? []).filter((x) => allowed.has(x))
    } catch {
      ytChannels.value = []
    } finally {
      ytLoading.value = false
    }
  }

  const loadYtChannelDefaults = async (channelId: string) => {
    const cid = (channelId ?? '').trim()
    if (!cid) return
    if (ytChannelDefaultsById.value[cid]) return
    if (ytChannelDefaultsLoading.value[cid]) return

    ytChannelDefaultsLoading.value = { ...ytChannelDefaultsLoading.value, [cid]: true }
    try {
      const data = await api.get<any>(`youtube/channels/${encodeURIComponent(cid)}`)
      ytChannelDefaultsById.value = {
        ...ytChannelDefaultsById.value,
        [cid]: {
          defaultDescription: (data?.defaultDescription ?? '').toString(),
          defaultTags: (data?.defaultTags ?? '').toString(),
        },
      }

      const d = ytChannelDefaultsById.value[cid] ?? { defaultDescription: '', defaultTags: '' }
      if (!ytForm.value.description.trim() && d.defaultDescription) ytForm.value.description = d.defaultDescription
      if (!ytForm.value.tagsText.trim() && d.defaultTags) ytForm.value.tagsText = d.defaultTags
    } catch {
      ytChannelDefaultsById.value = { ...ytChannelDefaultsById.value, [cid]: { defaultDescription: '', defaultTags: '' } }
    } finally {
      ytChannelDefaultsLoading.value = { ...ytChannelDefaultsLoading.value, [cid]: false }
    }
  }

  watch(
    fbSelectedPageIds,
    (ids) => {
      try {
        localStorage.setItem(LS_FB_PAGES, JSON.stringify(Array.from(new Set(ids ?? [])).filter(Boolean)))
      } catch {
        // ignore
      }
    },
    { deep: true },
  )

  watch(
    fbSelectedPageIds,
    async (ids) => {
      const uniq = Array.from(new Set(ids ?? [])).filter(Boolean)
      for (const pid of uniq) {
        await loadFbPageDefaults(pid)
      }

      const keep = new Set(uniq)
      Object.keys(fbPageDefaultsById.value).forEach((pid) => {
        if (!keep.has(pid)) {
          const next = { ...fbPageDefaultsById.value }
          delete next[pid]
          fbPageDefaultsById.value = next
        }
      })
      Object.keys(fbPageDefaultsLoading.value).forEach((pid) => {
        if (!keep.has(pid)) {
          const next = { ...fbPageDefaultsLoading.value }
          delete next[pid]
          fbPageDefaultsLoading.value = next
        }
      })
    },
    { deep: true, immediate: true },
  )

  watch(
    fbForm,
    (v) => {
      try {
        localStorage.setItem(
          LS_FB_FORM,
          JSON.stringify({ title: (v?.title ?? '').toString(), description: (v?.description ?? '').toString(), link: (v?.link ?? '').toString() }),
        )
      } catch {
        // ignore
      }
    },
    { deep: true },
  )

  watch(
    ytSelectedChannelIds,
    (ids) => {
      try {
        localStorage.setItem(LS_YT_CHANNELS, JSON.stringify(Array.from(new Set(ids ?? [])).filter(Boolean)))
      } catch {
        // ignore
      }
    },
    { deep: true },
  )

  watch(
    ytSelectedChannelIds,
    async (ids) => {
      const uniq = Array.from(new Set(ids ?? [])).filter(Boolean)
      for (const cid of uniq) {
        await loadYtChannelDefaults(cid)
      }

      const keep = new Set(uniq)
      Object.keys(ytChannelDefaultsById.value).forEach((cid) => {
        if (!keep.has(cid)) {
          const next = { ...ytChannelDefaultsById.value }
          delete next[cid]
          ytChannelDefaultsById.value = next
        }
      })
      Object.keys(ytChannelDefaultsLoading.value).forEach((cid) => {
        if (!keep.has(cid)) {
          const next = { ...ytChannelDefaultsLoading.value }
          delete next[cid]
          ytChannelDefaultsLoading.value = next
        }
      })
    },
    { deep: true, immediate: true },
  )

  watch(
    ytForm,
    (v) => {
      try {
        localStorage.setItem(
          LS_YT_FORM,
          JSON.stringify({
            title: (v?.title ?? '').toString(),
            description: (v?.description ?? '').toString(),
            privacyStatus: (v?.privacyStatus ?? 'public').toString(),
            tagsText: (v?.tagsText ?? '').toString(),
            publishAt: (v?.publishAt ?? '').toString(),
          }),
        )
      } catch {
        // ignore
      }
    },
    { deep: true },
  )

  const loadYtPlaylists = async (channelId: string) => {
    const cid = (channelId ?? '').trim()
    if (!cid) return

    if (!Array.isArray(ytSelectedPlaylistIdsByChannel.value[cid])) {
      ytSelectedPlaylistIdsByChannel.value = { ...ytSelectedPlaylistIdsByChannel.value, [cid]: [] }
    }

    ytPlaylistsLoading.value = { ...ytPlaylistsLoading.value, [cid]: true }
    try {
      const data = await api.get<YtPlaylist[]>(`youtube/channels/${encodeURIComponent(cid)}/playlists`)
      ytPlaylistsByChannel.value = { ...ytPlaylistsByChannel.value, [cid]: Array.isArray(data) ? data : [] }
    } catch {
      ytPlaylistsByChannel.value = { ...ytPlaylistsByChannel.value, [cid]: [] }
    } finally {
      ytPlaylistsLoading.value = { ...ytPlaylistsLoading.value, [cid]: false }
    }
  }

  watch(
    ytSelectedChannelIds,
    (ids) => {
      const uniq = Array.from(new Set(ids ?? []))
      for (const cid of uniq) {
        if (!Array.isArray(ytSelectedPlaylistIdsByChannel.value[cid])) {
          ytSelectedPlaylistIdsByChannel.value = { ...ytSelectedPlaylistIdsByChannel.value, [cid]: [] }
        }
      }
    },
    { deep: true },
  )

  const ytTags = computed(() =>
    (ytForm.value.tagsText ?? '')
      .split(',')
      .map((x) => x.trim())
      .filter(Boolean),
  )

  const canSubmit = computed(() => {
    if (!targets.value.fb && !targets.value.yt) return false

    if (targets.value.fb && fbSelectedPageIds.value.length === 0) return false

    if (targets.value.yt) {
      if (ytSelectedChannelIds.value.length === 0) return false
      if (!ytForm.value.title.trim()) return false
      if (ytForm.value.title.length > 100) return false
    }

    return true
  })

  const submit = async (input: VideoInput, thumbFile?: File | null): Promise<SubmitResult> => {
    const res: SubmitResult = { fbOk: 0, fbFail: [], ytOk: 0, ytFail: [] }

    if (input.kind === 'file' && !input.videoFile) {
      throw new Error('Vui lòng chọn video file.')
    }

    if (targets.value.fb) {
      for (const pageId of fbSelectedPageIds.value) {
        try {
          if (input.kind === 'url') {
            await api.post(
              'facebook/reels/from-url',
              {
                pageId,
                videoUrl: input.videoUrl,
                title: fbForm.value.title.trim() || null,
                description: fbForm.value.description.trim() || null,
                link: fbForm.value.link.trim() || null,
              },
              { timeout: 120000 },
            )
          } else {
            const form = new FormData()
            form.append('video', input.videoFile as File)
            form.append('pageId', pageId)
            if (fbForm.value.title.trim()) form.append('title', fbForm.value.title.trim())
            if (fbForm.value.description.trim()) form.append('description', fbForm.value.description.trim())
            if (fbForm.value.link.trim()) form.append('link', fbForm.value.link.trim())

            await api.post('facebook/reels', form, {
              headers: { 'Content-Type': 'multipart/form-data' },
              timeout: 180000,
            } as any)
          }

          res.fbOk += 1
        } catch (e: any) {
          res.fbFail.push(`${pageId}: ${e?.message ?? 'upload fail'}`)
        }
      }
    }

    if (targets.value.yt) {
      for (const channelId of ytSelectedChannelIds.value) {
        try {
          const playlistIds = ytSelectedPlaylistIdsByChannel.value[channelId] ?? []

          if (input.kind === 'url') {
            await api.post(
              `youtube/channels/${encodeURIComponent(channelId)}/videos/upload-from-url`,
              {
                videoUrl: input.videoUrl,
                title: ytForm.value.title.trim(),
                description: ytForm.value.description.trim() || null,
                privacyStatus: ytForm.value.privacyStatus,
                tags: ytTags.value,
                playlistIds,
                publishAt: ytForm.value.publishAt.trim() || null,
                thumbnailUrl: ytThumbUrl.value.trim() || null,
              },
              { timeout: 600000 },
            )
          } else {
            const formData = new FormData()
            formData.append('file', input.videoFile as File)
            formData.append('title', ytForm.value.title.trim())
            formData.append('description', ytForm.value.description.trim())
            formData.append('privacyStatus', ytForm.value.privacyStatus)
            ytTags.value.forEach((t) => formData.append('tags', t))
            playlistIds.forEach((pid) => formData.append('playlistIds', pid))
            if (ytForm.value.publishAt.trim()) formData.append('publishAt', ytForm.value.publishAt.trim())

            const uploadRes = await api.post<any>(
              `youtube/channels/${encodeURIComponent(channelId)}/videos/upload`,
              formData,
              { headers: { 'Content-Type': 'multipart/form-data' }, timeout: 180000 } as any,
            )

            const videoId = uploadRes?.videoId
            if (videoId && thumbFile) {
              const f = new FormData()
              f.append('file', thumbFile)
              await api.post(`youtube/videos/${encodeURIComponent(videoId)}/thumbnail`, f, {
                headers: { 'Content-Type': 'multipart/form-data' },
                timeout: 60000,
              } as any)
            }
          }

          res.ytOk += 1
        } catch (e: any) {
          res.ytFail.push(`${channelId}: ${e?.message ?? 'upload fail'}`)
        }
      }
    }

    return res
  }

  const reset = (opts?: { keepTargets?: boolean; keepSelections?: boolean }) => {
    const keepTargets = opts?.keepTargets === true
    const keepSelections = opts?.keepSelections === true

    if (!keepTargets) targets.value = { fb: true, yt: true }
    if (!keepSelections) fbSelectedPageIds.value = []
    fbForm.value = { title: '', description: '', link: '' }

    if (!keepSelections) ytSelectedChannelIds.value = []
    ytForm.value = { title: '', description: '', privacyStatus: 'public', tagsText: '', publishAt: '' }
    ytThumbUrl.value = ''
    ytPlaylistsByChannel.value = {}
    ytPlaylistsLoading.value = {}
    ytSelectedPlaylistIdsByChannel.value = {}

    fbPageDefaultsById.value = {}
    fbPageDefaultsLoading.value = {}
    ytChannelDefaultsById.value = {}
    ytChannelDefaultsLoading.value = {}
  }

  return {
    targets,
    canSubmit,

    fbPages,
    fbLoading,
    fbSelectedPageIds,
    fbForm,
    loadFbPages,
    loadFbPageDefaults,

    ytChannels,
    ytLoading,
    ytSelectedChannelIds,
    ytForm,
    ytThumbUrl,
    loadYtChannelDefaults,
    ytPlaylistsByChannel,
    ytPlaylistsLoading,
    ytSelectedPlaylistIdsByChannel,
    ytChannelTitle,
    loadYtChannels,
    loadYtPlaylists,

    submit,
    reset,
  }
}
