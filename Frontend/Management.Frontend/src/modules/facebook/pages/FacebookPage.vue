<template>
  <div class="wrap">
    <div class="card">
      <div class="card-title">Facebook</div>
      <div class="toolbar">
        <button class="btn" type="button" :disabled="working" @click="connectFacebook">
          {{ working ? 'Đang xử lý...' : 'Kết nối Facebook' }}
        </button>
        <button class="btn secondary" type="button" :disabled="working" @click="syncPages">Sync Pages</button>
        <button class="btn secondary" type="button" :disabled="working" @click="loadPages">Tải Pages</button>
        <div class="spacer" />
        <div v-if="connected" class="badge ok">Connected</div>
        <div v-else class="badge">Not connected</div>
      </div>

      <div v-if="message" class="alert" :class="messageType">{{ message }}</div>
    </div>

    <button class="post-fab" type="button" :disabled="!activePageId" @click="openPostSidebar">
      <span class="fab-icon">+</span>
      <span class="fab-label">Đăng bài</span>
    </button>

    <div class="content-wrapper">
      <div class="main-content">
        <div class="card">
          <div class="card-title">Pages</div>
          <div class="pages">
            <div v-if="pagesLoading" class="muted">Đang tải...</div>
            <div v-else-if="pages.length === 0" class="muted">Chưa có Page nào. Hãy kết nối Facebook rồi bấm "Tải Pages".</div>
            <div v-else class="pages-dropdown">
              <div class="pages-row">
                <select v-model="selectedPageIds" class="input" multiple>
                  <option v-for="p in pages" :key="p.id" :value="p.id">{{ p.name }}</option>
                </select>
                <button class="btn secondary" type="button" :disabled="working || selectedPageIds.length === 0" @click="loadPosts">
                  Tải bài viết
                </button>
              </div>
              <div class="muted">Đã chọn: {{ selectedPageIds.length }} page(s)</div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-title">Bài viết</div>
          <div class="toolbar" style="margin-bottom: 10px">
            <button
              class="btn secondary"
              type="button"
              :disabled="postsLoading"
              :class="{ activeToggle: viewMode === 'grid' }"
              @click="viewMode = 'grid'"
            >
              Grid list
            </button>
            <button
              class="btn secondary"
              type="button"
              :disabled="postsLoading"
              :class="{ activeToggle: viewMode === 'cards' }"
              @click="viewMode = 'cards'"
            >
              Cards
            </button>
            <div class="spacer" />
            <button class="btn secondary" type="button" :disabled="postsLoading || selectedPageIds.length === 0" @click="loadPosts">Refresh</button>
          </div>
          <div v-if="postsLoading" class="muted">Đang tải...</div>
          <div v-else-if="posts.length === 0" class="muted">Chưa có bài viết (hoặc chưa tải).</div>
          <div v-else>
            <div v-if="viewMode === 'grid'" class="posts-gridlist">
              <div class="grid-head">
                <div>Page</div>
                <div>Nội dung</div>
                <div>Thống kê</div>
                <div>Thời gian</div>
                <div></div>
              </div>
              <div v-for="p in posts" :key="p.id" class="grid-row">
                <div class="cell page">
                  <div class="page-name">{{ p.pageName }}</div>
                  <div class="page-id">{{ p.pageId }}</div>
                </div>
                <div class="cell msg">
                  <div class="post-msg">{{ p.message || '(no message)' }}</div>
                </div>
                <div class="cell metrics">
                  <div class="metric-row">
                    <span class="metric">View: {{ p.viewCount ?? 0 }}</span>
                    <span class="metric">Like: {{ p.likeCount ?? 0 }}</span>
                    <span class="metric">Comment: {{ p.commentCount ?? 0 }}</span>
                    <span class="metric">Share: {{ p.shareCount ?? 0 }}</span>
                  </div>
                </div>
                <div class="cell time">
                  <div class="post-time" v-if="p.createdTime">{{ formatDate(p.createdTime) }}</div>
                </div>
                <div class="cell actions">
                  <div class="post-actions">
                    <a v-if="p.permalinkUrl" class="link" :href="p.permalinkUrl" target="_blank" rel="noreferrer">Mở</a>
                    <button class="btn tiny secondary" type="button" :disabled="working" @click="openEdit(p)">Sửa</button>
                    <button class="btn tiny danger" type="button" :disabled="working" @click="deletePost(p)">Xoá</button>
                  </div>
                </div>
              </div>
            </div>

            <div v-else class="posts-cards">
              <div v-for="p in posts" :key="p.id" class="post-card">
                <img v-if="p.pictureUrl" class="post-img" :src="p.pictureUrl" alt="post" />
                <div class="post-card-body">
                  <div class="post-top">
                    <div>
                      <div class="page-name">{{ p.pageName }}</div>
                      <div class="post-id">{{ p.id }}</div>
                    </div>
                    <div class="post-actions">
                      <a v-if="p.permalinkUrl" class="link" :href="p.permalinkUrl" target="_blank" rel="noreferrer">Mở</a>
                      <button class="btn tiny secondary" type="button" :disabled="working" @click="openEdit(p)">Sửa</button>
                      <button class="btn tiny danger" type="button" :disabled="working" @click="deletePost(p)">Xoá</button>
                    </div>
                  </div>
                  <div class="post-msg">{{ p.message || '(no message)' }}</div>
                  <div class="metric-row" style="margin-top: 8px">
                    <span class="metric">View: {{ p.viewCount ?? 0 }}</span>
                    <span class="metric">Like: {{ p.likeCount ?? 0 }}</span>
                    <span class="metric">Comment: {{ p.commentCount ?? 0 }}</span>
                    <span class="metric">Share: {{ p.shareCount ?? 0 }}</span>
                  </div>
                  <div class="post-time" v-if="p.createdTime" style="margin-top: 8px">{{ formatDate(p.createdTime) }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="postSidebarOpen" class="right-sidebar-backdrop" :class="{ show: postSidebarOpen }" @click.self="closePostSidebar">
        <div class="right-sidebar" :class="{ show: postSidebarOpen }">
          <div class="sidebar-header">
            <div class="sidebar-title">Đăng bài</div>
            <button class="sidebar-close" type="button" @click="closePostSidebar">✕</button>
          </div>

          <div class="sidebar-content">
            <div class="form">
              <div class="actions" style="justify-content: flex-start">
                <button class="btn secondary" type="button" :disabled="working" :class="{ activeToggle: postMode === 'post' }" @click="postMode = 'post'">Post</button>
                <button class="btn secondary" type="button" :disabled="working" :class="{ activeToggle: postMode === 'reel' }" @click="postMode = 'reel'">Reel</button>
              </div>

              <div class="field">
                <label>Page</label>
                <select v-model="postTargetPageId" class="input" :disabled="pages.length === 0">
                  <option value="">Chọn page...</option>
                  <option v-for="p in pages" :key="p.id" :value="p.id">{{ p.name }}</option>
                </select>
              </div>

              <template v-if="postMode === 'post'">
                <div class="field">
                  <label>Nội dung</label>
                  <textarea v-model="postForm.message" class="input" rows="4" placeholder="Nhập nội dung bài viết..." />
                </div>
                <div class="field">
                  <label>Link (tuỳ chọn)</label>
                  <input v-model="postForm.link" class="input" type="text" placeholder="https://..." />
                </div>
                <div class="field">
                  <label>Lịch đăng (UTC+7, tuỳ chọn)</label>
                  <input v-model="postForm.publishAt" class="input" type="datetime-local" />
                </div>
              </template>

              <template v-else>
                <div class="field">
                  <label>Video Reel</label>
                  <input type="file" accept="video/*" :disabled="working" @change="onPickReelVideo" />
                  <div v-if="reelVideoFile" class="muted" style="margin-top: 6px">{{ reelVideoFile.name }}</div>
                </div>

                <div class="field">
                  <label>Title (tuỳ chọn)</label>
                  <input v-model="reelForm.title" class="input" type="text" placeholder="Tiêu đề..." />
                </div>

                <div class="field">
                  <label>Caption / Description</label>
                  <textarea v-model="reelForm.description" class="input" rows="4" placeholder="Nhập caption..." />
                </div>

                <div class="field">
                  <label>Link (tuỳ chọn)</label>
                  <input v-model="reelForm.link" class="input" type="text" placeholder="https://..." />
                  <div class="muted" style="margin-top: 6px">Lưu ý: Nút CTA "Tìm hiểu thêm" là tính năng quảng cáo; link sẽ được gắn vào caption.</div>
                </div>
              </template>

              <div class="actions">
                <button v-if="postMode === 'post'" class="btn" type="button" :disabled="working || !canPost" @click="createPost">Đăng bài</button>
                <button v-else class="btn" type="button" :disabled="working || !canUploadReel" @click="uploadReel">Đăng Reel</button>
                <button class="btn secondary" type="button" :disabled="working" @click="closePostSidebar">Đóng</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="editOpen" class="modal-backdrop" @click.self="closeEdit">
      <div class="modal">
        <div class="modal-title">Sửa bài</div>
        <div class="form">
          <div class="field">
            <label>PostId</label>
            <div class="code">{{ editForm.postId }}</div>
          </div>
          <div class="field">
            <label>Nội dung</label>
            <textarea v-model="editForm.message" class="input" rows="5" />
          </div>

          <div class="actions">
            <button class="btn" type="button" :disabled="working" @click="updatePost">Lưu</button>
            <button class="btn secondary" type="button" :disabled="working" @click="closeEdit">Đóng</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '@/infrastructure/http/apiClient'

type FbPage = {
  id: string
  name: string
  pictureUrl?: string | null
}

type FbPost = {
  id: string
  pageId: string
  pageName: string
  message?: string | null
  createdTime?: string | null
  permalinkUrl?: string | null
  pictureUrl?: string | null
  likeCount?: number | null
  commentCount?: number | null
  shareCount?: number | null
  viewCount?: number | null
}

const route = useRoute()

const message = ref('')
const messageType = ref<'success' | 'error'>('success')
const setMsg = (msg: string, type: 'success' | 'error' = 'success') => {
  message.value = msg
  messageType.value = type
}
const clearMsg = () => {
  message.value = ''
}

const working = ref(false)

const pagesLoading = ref(false)
const pages = ref<FbPage[]>([])
const selectedPageIds = ref<string[]>([])

const activePageId = computed(() => selectedPageIds.value[0] ?? '')

const postsLoading = ref(false)
const posts = ref<FbPost[]>([])

const viewMode = ref<'grid' | 'cards'>('grid')

const postTargetPageId = ref('')

const postMode = ref<'post' | 'reel'>('post')

const reelVideoFile = ref<File | null>(null)
const reelForm = reactive({
  title: '',
  description: '',
  link: '',
})

const canUploadReel = computed(() => Boolean(postTargetPageId.value) && Boolean(reelVideoFile.value))

const postForm = reactive({
  message: '',
  link: '',
  publishAt: '',
})

const editOpen = ref(false)
const editForm = reactive({
  pageId: '',
  postId: '',
  message: '',
})

const postSidebarOpen = ref(false)

const connected = computed(() => pages.value.length > 0)
const canPost = computed(() => Boolean(activePageId.value) && Boolean(postForm.message.trim()))

const toIsoUtc7 = (local: string) => {
  const v = (local ?? '').trim()
  if (!v) return ''
  return `${v}:00+07:00`
}

const connectFacebook = async () => {
  clearMsg()
  working.value = true
  try {
    const data = await api.get<{ url: string }>('facebook/oauth/url')
    if (!data?.url) throw new Error('Missing oauth url')
    window.location.href = data.url
  } catch (e: any) {
    setMsg(e?.message ?? 'Không tạo được OAuth url.', 'error')
  } finally {
    working.value = false
  }
}

const syncPages = async () => {
  clearMsg()
  working.value = true
  try {
    await api.post('facebook/pages/sync')
    setMsg('Đã đồng bộ pages.', 'success')
    await loadPages()
    await loadPosts()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không đồng bộ được pages.', 'error')
  } finally {
    working.value = false
  }
}

const loadPages = async () => {
  clearMsg()
  pagesLoading.value = true
  try {
    const data = await api.get<FbPage[]>('facebook/pages')
    pages.value = Array.isArray(data) ? data : []
    if (selectedPageIds.value.length === 0 && pages.value.length > 0) {
      const first = pages.value[0]
      if (first) selectedPageIds.value = [first.id]
    }
  } catch (e: any) {
    setMsg(e?.message ?? 'Không tải được pages.', 'error')
  } finally {
    pagesLoading.value = false
  }
}

const loadPosts = async () => {
  if (selectedPageIds.value.length === 0) return
  clearMsg()
  postsLoading.value = true
  try {
    const selected = pages.value.filter((x) => selectedPageIds.value.includes(x.id))
    const all = await Promise.all(
      selected.map(async (pg) => {
        const data = await api.get<any[]>(`facebook/pages/${encodeURIComponent(pg.id)}/posts?limit=25`)
        const items = Array.isArray(data) ? data : []
        return items.map((it) => ({
          id: it.id,
          pageId: pg.id,
          pageName: pg.name,
          message: it.message ?? null,
          createdTime: it.createdTime ?? null,
          permalinkUrl: it.permalinkUrl ?? null,
          pictureUrl: it.pictureUrl ?? null,
          likeCount: it.likeCount ?? 0,
          commentCount: it.commentCount ?? 0,
          shareCount: it.shareCount ?? 0,
          viewCount: it.viewCount ?? 0,
        } as FbPost))
      }),
    )

    const merged = all.flat()
    merged.sort((a, b) => {
      const ta = a.createdTime ? new Date(a.createdTime).getTime() : 0
      const tb = b.createdTime ? new Date(b.createdTime).getTime() : 0
      return tb - ta
    })
    posts.value = merged
  } catch (e: any) {
    setMsg(e?.message ?? 'Không tải được bài viết.', 'error')
  } finally {
    postsLoading.value = false
  }
}

const createPost = async () => {
  if (!canPost.value) return
  clearMsg()
  working.value = true
  try {
    await api.post('facebook/posts', {
      pageId: postTargetPageId.value,
      message: postForm.message,
      link: postForm.link || null,
      publishAt: toIsoUtc7(postForm.publishAt) || null,
    })
    setMsg('Đăng bài thành công.', 'success')
    postForm.message = ''
    postForm.link = ''
    postForm.publishAt = ''
    await loadPosts()
  } catch (e: any) {
    setMsg(e?.message ?? 'Đăng bài thất bại.', 'error')
  } finally {
    working.value = false
  }
}

const openEdit = (p: FbPost) => {
  if (!p.pageId) return
  editForm.pageId = p.pageId
  editForm.postId = p.id
  editForm.message = p.message ?? ''
  editOpen.value = true
}

const closeEdit = () => {
  editOpen.value = false
}

const openPostSidebar = () => {
  postTargetPageId.value = activePageId.value
  postMode.value = 'post'
  postSidebarOpen.value = true
}

const closePostSidebar = () => {
  postSidebarOpen.value = false
}

const onPickReelVideo = (e: Event) => {
  const input = e.target as HTMLInputElement
  reelVideoFile.value = input.files?.[0] ?? null
}

const uploadReel = async () => {
  if (!canUploadReel.value || !reelVideoFile.value) return
  clearMsg()
  working.value = true
  try {
    const form = new FormData()
    form.append('video', reelVideoFile.value)
    form.append('pageId', postTargetPageId.value)
    if (reelForm.title.trim()) form.append('title', reelForm.title)
    if (reelForm.description.trim()) form.append('description', reelForm.description)
    if (reelForm.link.trim()) form.append('link', reelForm.link)

    await api.post('facebook/reels', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    } as any)

    setMsg('Đăng Reel thành công.', 'success')
    reelForm.title = ''
    reelForm.description = ''
    reelForm.link = ''
    reelVideoFile.value = null
    closePostSidebar()
    await loadPosts()
  } catch (e: any) {
    setMsg(e?.message ?? 'Đăng Reel thất bại.', 'error')
  } finally {
    working.value = false
  }
}

const updatePost = async () => {
  if (!editForm.pageId || !editForm.postId || !editForm.message.trim()) return
  clearMsg()
  working.value = true
  try {
    await api.put('facebook/posts', {
      pageId: editForm.pageId,
      postId: editForm.postId,
      message: editForm.message,
    })
    setMsg('Cập nhật thành công.', 'success')
    closeEdit()
    await loadPosts()
  } catch (e: any) {
    setMsg(e?.message ?? 'Cập nhật thất bại.', 'error')
  } finally {
    working.value = false
  }
}

const deletePost = async (p: FbPost) => {
  if (!p.pageId) return
  if (!confirm('Xoá bài viết này?')) return
  clearMsg()
  working.value = true
  try {
    await api.delete(`facebook/posts/${encodeURIComponent(p.id)}?pageId=${encodeURIComponent(p.pageId)}`)
    setMsg('Đã xoá bài.', 'success')
    await loadPosts()
  } catch (e: any) {
    setMsg(e?.message ?? 'Xoá thất bại.', 'error')
  } finally {
    working.value = false
  }
}

const formatDate = (val: string) => {
  const dt = new Date(val)
  if (Number.isNaN(dt.getTime())) return val
  return dt.toLocaleString()
}

onMounted(async () => {
  const fbError = route.query.fbError as string | undefined
  const fbConnected = route.query.fbConnected as string | undefined
  if (fbError) setMsg(fbError, 'error')
  if (fbConnected) setMsg('Kết nối Facebook thành công.', 'success')

  await loadPages()
  await loadPosts()
})

watch(selectedPageIds, async () => {
  await loadPosts()
})
</script>

<style scoped>
.content-wrapper {
  display: flex;
  gap: 20px;
  align-items: flex-start;
}

.main-content {
  flex: 1;
  min-width: 0;
}

@media (max-width: 1024px) {
  .content-wrapper {
    flex-direction: column;
  }
}

.wrap {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.card {
  background: #fff;
  border-radius: 10px;
  padding: 20px;
}

.card-title {
  font-weight: 700;
  margin-bottom: 10px;
}

.muted {
  color: #999;
  font-size: 13px;
}

.toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
}

.spacer {
  flex: 1;
}

.btn {
  border: 1px solid #eee;
  background: #f26f3b;
  color: #fff;
  border-radius: 8px;
  padding: 8px 12px;
  cursor: pointer;
  font-size: 13px;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn.secondary {
  background: #fff;
  color: #333;
}

.btn.tiny {
  padding: 6px 10px;
  border-radius: 7px;
  font-size: 12px;
}

.btn.danger {
  background: #fff;
  color: #d1242f;
}

.badge {
  border: 1px solid #eee;
  background: #fafafa;
  border-radius: 999px;
  padding: 4px 10px;
  font-size: 12px;
  color: #666;
}

.badge.ok {
  color: #1a7f37;
  border-color: #d1f5db;
  background: #effaf2;
}

.alert {
  margin-top: 12px;
  padding: 10px 12px;
  border-radius: 8px;
  border: 1px solid #eee;
  font-size: 13px;
}

.alert.success {
  background: #effaf2;
  border-color: #d1f5db;
  color: #1a7f37;
}

.alert.error {
  background: #fff2f3;
  border-color: #ffd1d4;
  color: #b42318;
}

.page-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.page-item {
  border: 1px solid #eee;
  background: #fff;
  border-radius: 10px;
  padding: 10px;
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  text-align: left;
}

.page-item.active {
  border-color: #f26f3b;
  box-shadow: 0 0 0 3px rgba(242, 111, 59, 0.12);
}

.page-pic {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  object-fit: cover;
}

.page-name {
  font-weight: 700;
  font-size: 13px;
}

.page-id {
  color: #999;
  font-size: 12px;
  margin-top: 2px;
}

.form {
  display: grid;
  gap: 10px;
}

.field label {
  display: block;
  font-size: 12px;
  color: #666;
  margin-bottom: 6px;
}

.input {
  width: 100%;
  border: 1px solid #eee;
  border-radius: 8px;
  padding: 8px 10px;
  font-size: 13px;
  outline: none;
}

.actions {
}

.posts-gridlist {
  border: 1px solid #eee;
  border-radius: 10px;
  overflow: hidden;
}

.grid-head,
.grid-row {
  display: grid;
  grid-template-columns: 220px 1fr 220px 180px 190px;
  gap: 10px;
  align-items: start;
  padding: 10px;
}

.grid-head {
  background: #fafafa;
  border-bottom: 1px solid #eee;
  font-size: 12px;
  color: #666;
  font-weight: 700;
}

.grid-row {
  border-bottom: 1px solid #f1f1f1;
}

.grid-row:last-child {
  border-bottom: none;
}

.cell {
  min-width: 0;
}

.metric-row {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.metric {
  font-size: 12px;
  color: #666;
  border: 1px solid #eee;
  background: #fafafa;
  padding: 4px 8px;
  border-radius: 999px;
}

.posts-cards {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}

.post-card {
  border: 1px solid #eee;
  border-radius: 12px;
  overflow: hidden;
  background: #fff;
}

.post-img {
  width: 100%;
  height: 180px;
  object-fit: cover;
  background: #fafafa;
}

.post-card-body {
  padding: 10px;
}

.activeToggle {
  border-color: #f26f3b;
  box-shadow: 0 0 0 3px rgba(242, 111, 59, 0.12);
}

@media (max-width: 980px) {
  .posts-cards {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .grid-head,
  .grid-row {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .posts-cards {
    grid-template-columns: 1fr;
  }
}

.post-item {
  border: 1px solid #eee;
  border-radius: 10px;
  padding: 10px;
}

.post-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 8px;
}

.post-id {
  font-size: 12px;
  color: #999;
  word-break: break-all;
}

.post-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.post-msg {
  font-size: 13px;
  color: #222;
  white-space: pre-wrap;
}

.post-time {
  margin-top: 6px;
  color: #999;
  font-size: 12px;
}

.link {
  color: #f26f3b;
  text-decoration: none;
  font-size: 12px;
}

.code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
  font-size: 12px;
  padding: 6px 8px;
  border: 1px solid #eee;
  border-radius: 8px;
  background: #fafafa;
  color: #333;
  word-break: break-all;
}

.post-fab {
  position: fixed;
  bottom: 24px;
  right: 24px;
  display: flex;
  align-items: center;
  gap: 8px;
  background: #f26f3b;
  color: #fff;
  border: none;
  border-radius: 999px;
  padding: 12px 20px;
  font-size: 14px;
  font-weight: 700;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(242, 111, 59, 0.3);
  transition: all 0.2s ease;
  z-index: 40;
}

.post-fab:hover {
  background: #e55a2b;
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(242, 111, 59, 0.35);
}

.post-fab:disabled {
  background: #ccc;
  cursor: not-allowed;
  box-shadow: none;
  transform: none;
}

.fab-icon {
  font-size: 20px;
  line-height: 1;
}

.fab-label {
  font-size: 14px;
}

.right-sidebar-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.35);
  z-index: 50;
}

.right-sidebar {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 420px;
  max-width: 90vw;
  background: #fff;
  box-shadow: -4px 0 12px rgba(0, 0, 0, 0.15);
  display: flex;
  flex-direction: column;
  z-index: 51;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid #eee;
}

.sidebar-title {
  font-size: 16px;
  font-weight: 700;
  color: #222;
}

.sidebar-close {
  background: none;
  border: none;
  font-size: 20px;
  color: #999;
  cursor: pointer;
  padding: 4px;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.sidebar-close:hover {
  background: #f3f3f3;
  color: #333;
}

.sidebar-content {
  flex: 1;
  padding: 20px;
  overflow-y: auto;
}

@media (max-width: 640px) {
  .post-fab {
    bottom: 16px;
    right: 16px;
    padding: 10px 16px;
    font-size: 13px;
  }

  .fab-icon {
    font-size: 18px;
  }

  .fab-label {
    display: none;
  }

  .right-sidebar {
    width: 100vw;
    max-width: 100vw;
  }
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px;
  z-index: 100;
}

.modal {
  width: 700px;
  max-width: 95vw;
  background: #fff;
  border-radius: 12px;
  padding: 16px;
}

.modal-title {
  font-weight: 700;
  margin-bottom: 10px;
}

@media (max-width: 980px) {
  .page-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .page-grid {
    grid-template-columns: 1fr;
  }
  .toolbar {
    flex-wrap: wrap;
  }
}
</style>
