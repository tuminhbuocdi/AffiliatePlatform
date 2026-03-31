<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Quản lý sản phẩm</h1>
        <div class="muted">Danh sách sản phẩm dạng grid. Bấm vào sản phẩm để chỉnh sửa.</div>
      </div>

      <div class="toolbar">
        <input v-model="search" class="input" placeholder="Tìm kiếm..." @keyup.enter="load" />
        <select v-model="filter" class="input" @change="load">
          <option value="All">All</option>
          <option value="Have AffiliateLink">Have AffiliateLink</option>
          <option value="Not Affliliate Link">Not Affliliate Link</option>
          <option value="Have Socials">Have Socials</option>
          <option value="Not Socials">Not Socials</option>
        </select>
        <button class="btn" type="button" @click="load" :disabled="loading">{{ loading ? 'Đang tải...' : 'Tải' }}</button>
        <button class="btn secondary" type="button" @click="exportAffiliateLinks" :disabled="loading || exporting">
          {{ exporting ? 'Đang xuất...' : 'Xuất khẩu' }}
        </button>
        <button class="btn secondary" type="button" @click="openImport" :disabled="loading || importing">Import dữ liệu</button>
      </div>
    </div>

    <div v-if="error" class="error">{{ error }}</div>

    <div v-if="loading" class="loading">Đang tải...</div>

    <div v-else class="grid-wrap">
      <div class="grid">
        <div v-for="p in products" :key="p.id" class="card">
          <button type="button" class="card-main" @click="openEdit(p.id)">
            <img class="thumb" :src="p.imageUrl || '/placeholder-product.jpg'" alt="" />
            <div class="card-body">
              <div class="name">{{ p.name }}</div>
              <div v-if="p.description" class="desc">{{ p.description }}</div>
              <div class="info">
                <div class="info-item"><span class="k">Giá:</span> <span class="v">{{ formatMoney(p.price) }}</span></div>
                <div class="info-item"><span class="k">Đánh giá:</span> <span class="v">{{ formatRating(p.rating) }}</span></div>
                <div class="info-item"><span class="k">Đã bán:</span> <span class="v">{{ formatSold(p.sold) }}</span></div>
                <div class="info-item"><span class="k">Hoa hồng:</span> <span class="v">{{ formatPercent(p.commissionRate) }}</span></div>
                <div class="info-item">
                  <span class="k">Xử lý:</span>
                  <span class="v status">
                    <span class="status-dot" :class="p.isProcessed ? 'ok' : 'pending'"></span>
                    {{ p.isProcessed ? 'Đã xử lý' : 'Chưa xử lý' }}
                  </span>
                </div>
              </div>
            </div>
          </button>

          <div class="card-actions">
            <select class="input input-sm" :disabled="updatingProcessedId === p.id" :value="p.isProcessed ? 'processed' : 'unprocessed'" @change.stop="(e) => onChangeProcessed(p.id, (e.target as HTMLSelectElement).value)">
              <option value="unprocessed">Chưa xử lý</option>
              <option value="processed">Đã xử lý</option>
            </select>
            <button type="button" class="btn-icon" title="Đăng bài" @click.stop="openPost(p)">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M4 4h16v10H5.17L4 15.17V4zm0 14v2h10v-2H4zm12.5-1.5L22 21l-1.5 1.5L15 17l1.5-1.5z"/></svg>
            </button>
            <button type="button" class="btn-action" title="RenderReels" @click.stop="openRenderReels(p)">RenderReels</button>
            <button v-if="p.productLink" type="button" class="btn-icon" title="Xem Shopee" @click.stop="openLink(p.productLink)">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M14 3h7v7h-2V6.41l-9.29 9.3-1.42-1.42 9.3-9.29H14V3z"/><path fill="currentColor" d="M19 19H5V5h7V3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7z"/></svg>
            </button>
            <button type="button" class="btn-icon" :disabled="downloadingId === p.id" :title="downloadingId === p.id ? 'Đang tải...' : 'Lấy data Affiliate'" @click.stop="downloadAffiliate(p.id)">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M5 20h14v-2H5v2zM12 2v12l4-4 1.41 1.41L12 17.83l-5.41-5.42L8 10l4 4V2h0z"/></svg>
            </button>
            <button type="button" class="btn-icon" title="Sửa" @click.stop="openEdit(p.id)">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1.003 1.003 0 000-1.42l-2.34-2.34a1.003 1.003 0 00-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z"/></svg>
            </button>
            <button type="button" class="btn-icon danger" title="Xoá" @click.stop="remove(p.id)">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M6 7h12l-1 14H7L6 7zm3-3h6l1 2H8l1-2z"/></svg>
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="postOpen" class="modal-overlay" @click.self="closePost">
      <div class="modal modal-wide">
        <div class="modal-header">
          <div class="modal-title">Đăng bài</div>
          <button class="icon" type="button" @click="closePost">×</button>
        </div>

        <div class="modal-body">
          <div class="form">
            <div class="post-tabs" role="tablist" aria-label="Chọn chế độ đăng">
              <button class="post-tab" type="button" role="tab" :disabled="posting" :aria-selected="postMode === 'post'" :class="{ active: postMode === 'post' }" @click="postMode = 'post'">
                Post
              </button>
              <button
                v-if="canShowReelTab"
                class="post-tab"
                type="button"
                role="tab"
                :disabled="posting"
                :aria-selected="postMode === 'reel'"
                :class="{ active: postMode === 'reel' }"
                @click="postMode = 'reel'"
              >
                Reel
              </button>
            </div>

            <label v-if="postMode === 'post'">
              <div class="lbl">Page</div>
              <select v-model="postTargetPageId" class="input" :disabled="posting || fbPages.length === 0">
                <option value="">Chọn page...</option>
                <option v-for="pg in fbPages" :key="pg.id" :value="pg.id">{{ pg.name }}</option>
              </select>
            </label>

            <template v-if="postMode === 'post'">
              <label>
                <div class="lbl">Nội dung</div>
                <textarea v-model="postForm.message" class="textarea" rows="4" placeholder="Nhập nội dung bài viết..."></textarea>
              </label>

              <label>
                <div class="lbl">Link (tuỳ chọn)</div>
                <input v-model="postForm.link" class="input" type="text" placeholder="https://..." />
              </label>

              <label>
                <div class="lbl">Lịch đăng (UTC+7, tuỳ chọn)</div>
                <input v-model="postForm.publishAt" class="input" type="datetime-local" />
              </label>

              <div class="media-box">
                <div class="media-head">
                  <div class="muted">Ảnh: {{ selectedImageUrls.length }}/{{ postMedia.imageUrls.length }}</div>
                  <div class="media-actions">
                    <button class="btn tiny secondary" type="button" :disabled="posting" @click="selectAllImages">Chọn hết</button>
                    <button class="btn tiny secondary" type="button" :disabled="posting" @click="clearImages">Bỏ chọn</button>
                  </div>
                </div>
                <div v-if="postMedia.imageUrls.length > 1" class="muted" style="margin-bottom: 8px">Kéo thả để đổi thứ tự ảnh. Ảnh đứng đầu sẽ được ưu tiên hiển thị trước.</div>
                <div v-if="postMedia.imageUrls.length === 0" class="muted">Không có ảnh.</div>
                <div v-else class="media-grid">
                  <label
                    v-for="(u, idx) in postMedia.imageUrls"
                    :key="u"
                    class="media-item"
                    :class="{ dragging: draggingImageUrl === u, selected: selectedImageSet.has(u) }"
                    :draggable="!posting"
                    @dragstart="onImageDragStart(u)"
                    @dragover.prevent="onImageDragOver(u)"
                    @drop.prevent="onImageDrop(u)"
                    @dragend="onImageDragEnd"
                  >
                    <div class="media-order">#{{ idx + 1 }}</div>
                    <input type="checkbox" :disabled="posting" :checked="selectedImageSet.has(u)" @change="toggleImage(u)" />
                    <img class="media-thumb" :src="u" alt="" />
                  </label>
                </div>
              </div>

              <div class="media-box" style="margin-top: 10px">
                <div class="media-head">
                  <div class="muted">Video: {{ selectedVideoUrls.length }}/{{ postMedia.videoUrls.length }}</div>
                  <div class="media-actions">
                    <button class="btn tiny secondary" type="button" :disabled="posting" @click="selectAllVideos">Chọn hết</button>
                    <button class="btn tiny secondary" type="button" :disabled="posting" @click="clearVideos">Bỏ chọn</button>
                  </div>
                </div>
                <div v-if="postMedia.videoUrls.length === 0" class="muted">Không có video.</div>
                <div v-else class="media-list">
                  <label v-for="(u, idx) in postMedia.videoUrls" :key="u" class="media-row">
                    <input type="checkbox" :disabled="posting" :checked="selectedVideoSet.has(u)" @change="toggleVideo(u)" />
                    <span class="muted">Video {{ idx + 1 }}</span>
                    <button class="btn tiny secondary" type="button" :disabled="posting" @click.prevent.stop="setPostVideoPreview(u)">Xem</button>
                  </label>
                </div>

                <div v-if="postVideoPreviewUrl" class="video-preview">
                  <video :src="postVideoPreviewUrl" controls playsinline preload="metadata" class="video-player" />
                </div>
              </div>
            </template>

            <template v-else>
              <ReelMultiUploadPanel
                :disabled="posting"
                :image-urls="postMedia.imageUrls"
                :video-urls="postMedia.videoUrls"
                :default-caption="reelForm.description"
                :video-input="{ kind: 'url', videoUrl: '' }"
                :u="reelUploader"
                @submit="submitReelMulti"
              />
            </template>
          </div>

          <div v-if="postError" class="error" style="margin-top: 10px">{{ postError }}</div>
        </div>

        <div class="modal-footer">
          <button v-if="postMode === 'post'" class="btn" type="button" :disabled="posting || !canSubmitPost" @click="submitPost">
            {{ posting ? 'Đang đăng...' : 'Đăng' }}
          </button>
          <button class="btn secondary" type="button" :disabled="posting" @click="closePost">Đóng</button>
        </div>
      </div>
    </div>

    <div v-if="editOpen" class="modal-overlay" @click.self="closeEdit">
      <div class="modal">
        <div class="modal-header">
          <div class="modal-title">Chỉnh sửa sản phẩm</div>
          <button class="icon" type="button" @click="closeEdit">×</button>
        </div>

        <div v-if="editLoading" class="modal-body">Đang tải...</div>
        <div v-else class="modal-body">
          <div class="form">
            <label>
              <div class="lbl">Tiêu đề</div>
              <input v-model="editModel.name" class="input" />
            </label>

            <label>
              <div class="lbl">Mô tả</div>
              <textarea v-model="editModel.description" class="textarea" rows="8"></textarea>
            </label>
          </div>

          <div v-if="editError" class="error">{{ editError }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn" type="button" @click="save" :disabled="saving || editLoading">
            {{ saving ? 'Đang lưu...' : 'Lưu' }}
          </button>
          <button class="btn secondary" type="button" @click="closeEdit" :disabled="saving">Đóng</button>
        </div>
      </div>
    </div>

    <div v-if="showImportModal" class="modal-overlay" @click.self="closeImport">
      <div class="modal">
        <div class="modal-header">
          <div class="modal-title">Import dữ liệu sản phẩm</div>
          <button class="icon" type="button" @click="closeImport">×</button>
        </div>

        <div class="modal-body">
          <div class="muted">Dán JSON chứa danh sách sản phẩm vào textarea bên dưới.</div>
          <textarea v-model="importData" class="textarea" rows="10" placeholder="Paste JSON data here..."></textarea>

          <div v-if="importResult" class="import-result">{{ importResult }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn" type="button" @click="handleImport" :disabled="importing || !importData.trim()">
            {{ importing ? 'Đang import...' : 'Import dữ liệu' }}
          </button>
          <button class="btn secondary" type="button" @click="closeImport" :disabled="importing">Đóng</button>
        </div>
      </div>
    </div>

    <ProductRenderReelsDialog
      v-if="renderReelsOpen"
      :key="renderReelsProduct?.id ?? 0"
      v-model="renderReelsOpen"
      :product-name="renderReelsProduct?.name ?? ''"
      :image-urls="renderReelsMedia.imageUrls"
      :loading="renderReelsLoading"
      :error="renderReelsError"
      title="RenderReels"
      @rendered="onRenderReelsCompleted"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import api from '@/infrastructure/http/apiClient'
import { toast } from '@/shared/ui/toast'
import ReelMultiUploadPanel from '@/shared/ui/ReelMultiUploadPanel.vue'
import ProductRenderReelsDialog from '@/shared/ui/ProductRenderReelsDialog.vue'
import { useReelMultiUpload, type VideoInput } from '@/shared/ui/useReelMultiUpload'

const openLink = (url: string) => {
  try {
    window.open(url, '_blank', 'noopener,noreferrer')
  } catch {
    // ignore
  }
}

const exportAffiliateLinks = async () => {
  exporting.value = true
  try {
    const blob = (await api.request<any>({
      method: 'GET',
      url: 'admin/products/export-affiliate-links',
      responseType: 'blob',
      params: {
        search: search.value || undefined,
        filter: filter.value && filter.value !== 'All' ? filter.value : undefined,
      },
    })) as Blob

    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `affiliate_links.xlsx`
    document.body.appendChild(a)
    a.click()
    a.remove()
    window.URL.revokeObjectURL(url)
  } catch (e: any) {
    error.value = e?.message ?? 'Không xuất được file.'
  } finally {
    exporting.value = false
  }
}

const downloadAffiliate = async (id: number) => {
  downloadingId.value = id
  try {
    const blob = (await api.request<any>({
      method: 'GET',
      url: `admin/products/${id}/affiliate-data`,
      responseType: 'blob',
    })) as Blob

    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `product_${id}.zip`
    document.body.appendChild(a)
    a.click()
    a.remove()
    window.URL.revokeObjectURL(url)
  } catch (e: any) {
    error.value = e?.message ?? 'Không tải được data Affiliate.'
  } finally {
    if (downloadingId.value === id) downloadingId.value = null
  }
}

const closeImport = () => {
  if (importing.value) return
  showImportModal.value = false
  importResult.value = ''
}

const openImport = () => {
  showImportModal.value = true
  importResult.value = ''
}

const handleImport = async () => {
  if (!importData.value.trim()) return

  importing.value = true
  importResult.value = ''
  try {
    const res = await api.post<{ message?: string }>('products/import', {
      data: importData.value.trim(),
    })
    importResult.value = res?.message ?? 'Import thành công.'
    await load()
  } catch (e: any) {
    importResult.value = e?.message ?? 'Import thất bại. Vui lòng kiểm tra lại dữ liệu.'
  } finally {
    importing.value = false
  }
}

const formatMoney = (val?: number | null) => {
  if (val == null || Number.isNaN(val)) return '-'
  return new Intl.NumberFormat('vi-VN').format(val) + 'đ'
}

const formatRating = (val?: number | null) => {
  if (val == null || Number.isNaN(val)) return '-'
  return val.toFixed(1)
}

const formatSold = (val?: number | null) => {
  if (val == null || Number.isNaN(val)) return '-'
  return new Intl.NumberFormat('vi-VN').format(val)
}

const formatPercent = (val?: number | null) => {
  if (val == null || Number.isNaN(val)) return '-'
  if (val <= 1) return (val * 100).toFixed(2).replace(/\.00$/, '') + '%'
  return val.toFixed(2).replace(/\.00$/, '') + '%'
}

type ProductItem = {
  id: number
  name: string
  description?: string | null
  imageUrl?: string | null
  productLink?: string | null
  price?: number | null
  originalPrice?: number | null
  rating?: number | null
  sold?: number | null
  commissionRate?: number | null
  isProcessed: boolean
}

type FbPage = {
  id: string
  name: string
}

type ProductDetail = {
  id: number
  name: string
  description?: string | null
}

const products = ref<ProductItem[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const search = ref('')
const filter = ref('All')
const exporting = ref(false)

const editOpen = ref(false)
const editLoading = ref(false)
const saving = ref(false)
const editError = ref<string | null>(null)

const showImportModal = ref(false)
const importData = ref('')
const importing = ref(false)
const importResult = ref('')

const downloadingId = ref<number | null>(null)
const updatingProcessedId = ref<number | null>(null)

const renderReelsOpen = ref(false)
const renderReelsLoading = ref(false)
const renderReelsError = ref<string | null>(null)
const renderReelsProduct = ref<ProductItem | null>(null)
const renderReelsMedia = ref<{ imageUrls: string[] }>({ imageUrls: [] })

const postOpen = ref(false)
const posting = ref(false)
const postError = ref<string | null>(null)
const postMode = ref<'post' | 'reel'>('post')
const postTargetPageId = ref('')
const fbPages = ref<FbPage[]>([])
const postProduct = ref<ProductItem | null>(null)

const postMedia = ref<{ imageUrls: string[]; videoUrls: string[] }>({ imageUrls: [], videoUrls: [] })

const canShowReelTab = computed(() => (postMedia.value.videoUrls ?? []).length > 0)

const selectedImageSet = ref(new Set<string>())
const selectedVideoSet = ref(new Set<string>())

const selectedImageUrls = computed(() => postMedia.value.imageUrls.filter((x) => selectedImageSet.value.has(x)))
const selectedVideoUrls = computed(() => postMedia.value.videoUrls.filter((x) => selectedVideoSet.value.has(x)))

const postVideoPreviewUrl = ref('')
const draggingImageUrl = ref('')

const postForm = ref({ message: '', link: '', publishAt: '' })
const reelForm = ref({ videoUrl: '', title: '', description: '', link: '' })

const reelUploader = useReelMultiUpload()

const canSubmitPost = computed(() => {
  if (posting.value) return false
  if (postMode.value === 'post') return Boolean(postForm.value.message.trim())
  return true
})

const loadFbPages = async () => {
  try {
    const data = await api.get<FbPage[]>('facebook/pages')
    fbPages.value = Array.isArray(data) ? data : []
  } catch {
    fbPages.value = []
  }
}

const hashtagBlock = `#thoitrang #thoitrangnam #thoitrangnu #quanaodep #vaydep #damdep #phukien #tuixach`

const buildPostMessage = (productName: string, affiliateLink: string) => {
  const name = (productName || '').trim() || '[Tên sản phẩm]'
  const link = (affiliateLink || '').trim() || '[Link Affiliate]'
  return `Xem chi tiết: ${link}\n${name}\n${hashtagBlock}`
}

const toIsoUtc7 = (local: string) => {
  const v = (local ?? '').trim()
  if (!v) return ''
  return `${v}:00+07:00`
}

const openPost = async (p: ProductItem) => {
  postError.value = null
  postProduct.value = p
  postMode.value = 'post'
  postTargetPageId.value = ''
  postForm.value = { message: '', link: '', publishAt: '' }
  reelForm.value = { videoUrl: '', title: '', description: '', link: '' }
  reelUploader.reset({ keepSelections: true, keepTargets: true })
  postMedia.value = { imageUrls: [], videoUrls: [] }
  selectedImageSet.value = new Set<string>()
  selectedVideoSet.value = new Set<string>()
  postVideoPreviewUrl.value = ''
  postOpen.value = true

  await loadFbPages()
  if (fbPages.value.length > 0)
  {
    postTargetPageId.value = fbPages.value[0]?.id ?? ''
  }

  await Promise.all([reelUploader.loadFbPages(), reelUploader.loadYtChannels()])

  try {
    const media = await api.get<any>(`admin/products/${p.id}/media`)
    postMedia.value = {
      imageUrls: Array.isArray(media?.imageUrls) ? media.imageUrls : [],
      videoUrls: Array.isArray(media?.videoUrls) ? media.videoUrls : [],
    }

    selectedImageSet.value = new Set(postMedia.value.imageUrls)
    selectedVideoSet.value = new Set<string>()
    reelForm.value.videoUrl = ''
    postVideoPreviewUrl.value = ''

    const name = (media?.productName ?? p.name ?? '').toString()
    const link = (media?.affiliateLink ?? '').toString()
    const msg = buildPostMessage(name, link)
    postForm.value.message = msg
    reelForm.value.description = msg

    if (canShowReelTab.value) {
      const productName = name.trim()
      if (productName) {
        if (!reelUploader.ytForm.value.title.trim()) reelUploader.ytForm.value.title = productName
        if (!reelUploader.fbForm.value.title.trim()) reelUploader.fbForm.value.title = productName
      }
    }

    if (!canShowReelTab.value && postMode.value === 'reel') {
      postMode.value = 'post'
    }
  } catch (e: any) {
    postError.value = e?.message ?? 'Không lấy được media của sản phẩm.'
  }
}

const submitReelMulti = async (input: VideoInput) => {
  if (!postProduct.value) return
  if (posting.value) return

  postError.value = null
  posting.value = true
  const t = toast.loading('Đang upload Reel đa nền tảng...')
  try {
    const result = await reelUploader.submit(input)

    if (result.fbFail.length === 0 && result.ytFail.length === 0) {
      t.success(`Thành công. FB: ${result.fbOk}, YT: ${result.ytOk}`)
    } else {
      t.error(`Có lỗi. FB ok ${result.fbOk}, lỗi: ${result.fbFail.join(' | ')}. YT ok ${result.ytOk}, lỗi: ${result.ytFail.join(' | ')}`)
    }

    try {
      const id = postProduct.value.id
      await api.put(`admin/products/${id}/processed`, { isProcessed: true }, { timeout: 120000 })
      const idx = products.value.findIndex((x) => x.id === id)
      if (idx >= 0) products.value[idx].isProcessed = true
      postProduct.value.isProcessed = true
    } catch {
      // ignore
    }

    closePost()
  } catch (e: any) {
    const msg = e?.message ?? 'Upload thất bại.'
    postError.value = msg
    t.error(msg)
  } finally {
    posting.value = false
  }
}

const setPostVideoPreview = (u: string) => {
  postVideoPreviewUrl.value = (u ?? '').trim()
}

const toggleImage = (u: string) => {
  const next = new Set(selectedImageSet.value)
  if (next.has(u)) next.delete(u)
  else next.add(u)
  selectedImageSet.value = next
}

const moveImageBefore = (sourceUrl: string, targetUrl: string) => {
  if (!sourceUrl || !targetUrl || sourceUrl === targetUrl) return

  const list = [...postMedia.value.imageUrls]
  const fromIndex = list.indexOf(sourceUrl)
  const toIndex = list.indexOf(targetUrl)
  if (fromIndex < 0 || toIndex < 0 || fromIndex === toIndex) return

  const [moved] = list.splice(fromIndex, 1)
  const insertIndex = list.indexOf(targetUrl)
  if (!moved || insertIndex < 0) return
  list.splice(insertIndex, 0, moved)

  postMedia.value = {
    ...postMedia.value,
    imageUrls: list,
  }
}

const onImageDragStart = (u: string) => {
  if (posting.value) return
  draggingImageUrl.value = u
}

const onImageDragOver = (u: string) => {
  if (!draggingImageUrl.value || draggingImageUrl.value === u) return
}

const onImageDrop = (u: string) => {
  if (!draggingImageUrl.value) return
  moveImageBefore(draggingImageUrl.value, u)
  draggingImageUrl.value = ''
}

const onImageDragEnd = () => {
  draggingImageUrl.value = ''
}

const toggleVideo = (u: string) => {
  const next = new Set(selectedVideoSet.value)
  if (next.has(u)) next.delete(u)
  else next.add(u)
  selectedVideoSet.value = next

  if (reelForm.value.videoUrl && !next.has(reelForm.value.videoUrl)) {
    reelForm.value.videoUrl = selectedVideoUrls.value[0] ?? ''
  }

  if (postVideoPreviewUrl.value && !next.has(postVideoPreviewUrl.value)) {
    postVideoPreviewUrl.value = selectedVideoUrls.value[0] ?? ''
  }
}

const selectAllImages = () => {
  selectedImageSet.value = new Set(postMedia.value.imageUrls)
}

const clearImages = () => {
  selectedImageSet.value = new Set<string>()
}

const selectAllVideos = () => {
  selectedVideoSet.value = new Set(postMedia.value.videoUrls)
  if (!selectedVideoSet.value.has(reelForm.value.videoUrl)) {
    reelForm.value.videoUrl = selectedVideoUrls.value[0] ?? ''
  }
}

const clearVideos = () => {
  selectedVideoSet.value = new Set<string>()
  reelForm.value.videoUrl = ''
  postVideoPreviewUrl.value = ''
}

const closePost = () => {
  if (posting.value) return
  postOpen.value = false
  postError.value = null
}

const submitPost = async () => {
  if (postMode.value !== 'post') return
  if (!postProduct.value) return
  if (!postTargetPageId.value) return

  postError.value = null
  posting.value = true
  const t = toast.loading('Đang đăng bài...')
  try {
    const msg = postForm.value.message.trim()
    if (!msg) throw new Error('Vui lòng nhập nội dung.')

    const publishAt = toIsoUtc7(postForm.value.publishAt)
    if (publishAt && selectedVideoUrls.value.length > 0) {
      throw new Error('Chưa hỗ trợ lập lịch cho bài post có video. Vui lòng bỏ chọn video hoặc bỏ PublishAt.')
    }

    await api.post(
      'facebook/posts/with-media',
      {
        pageId: postTargetPageId.value,
        message: msg,
        link: postForm.value.link.trim() || null,
        publishAt: publishAt || null,
        imageUrls: selectedImageUrls.value,
        videoUrls: selectedVideoUrls.value,
      },
      { timeout: 120000 },
    )

    try {
      const id = postProduct.value.id
      await api.put(`admin/products/${id}/processed`, { isProcessed: true }, { timeout: 120000 })
      const idx = products.value.findIndex((x) => x.id === id)
      if (idx >= 0) products.value[idx].isProcessed = true
      postProduct.value.isProcessed = true
    } catch {
      // ignore
    }

    t.success('Đăng thành công')

    closePost()
  } catch (e: any) {
    const msg = e?.message ?? 'Đăng bài thất bại.'
    postError.value = msg
    if (typeof msg === 'string' && msg.toLowerCase().includes('timeout')) {
      t.error('Timeout khi đăng bài. Bạn thử lại hoặc giảm số lượng media.')
    } else {
      t.error(msg)
    }
  } finally {
    posting.value = false
  }
}

const editId = ref<number | null>(null)
const editModel = ref<{ name: string; description: string }>({ name: '', description: '' })

const load = async () => {
  loading.value = true
  error.value = null
  try {
    const data = await api.get<ProductItem[]>('admin/products', {
      params: {
        search: search.value || undefined,
        limit: 200,
        filter: filter.value && filter.value !== 'All' ? filter.value : undefined,
      },
    })
    products.value = data
  } catch (e: any) {
    error.value = e?.message ?? 'Không tải được danh sách sản phẩm.'
  } finally {
    loading.value = false
  }
}

const onChangeProcessed = async (id: number, value: string) => {
  const isProcessed = value === 'processed'
  updatingProcessedId.value = id
  try {
    await api.put(`admin/products/${id}/processed`, { isProcessed })
    const idx = products.value.findIndex((x) => x.id === id)
    if (idx >= 0) products.value[idx].isProcessed = isProcessed
  } catch (e: any) {
    error.value = e?.message ?? 'Cập nhật trạng thái thất bại.'
  } finally {
    if (updatingProcessedId.value === id) updatingProcessedId.value = null
  }
}

const remove = async (id: number) => {
  const ok = window.confirm('Bạn có chắc muốn xoá sản phẩm này?')
  if (!ok) return

  try {
    await api.delete(`admin/products/${id}`)
    if (editId.value === id) closeEdit()
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Xoá thất bại.'
  }
}

const openEdit = async (id: number) => {
  editOpen.value = true
  editLoading.value = true
  editError.value = null
  editId.value = id
  try {
    const detail = await api.get<ProductDetail>(`admin/products/${id}`)
    editModel.value = {
      name: detail.name ?? '',
      description: detail.description ?? '',
    }
  } catch (e: any) {
    editError.value = e?.message ?? 'Không tải được chi tiết sản phẩm.'
  } finally {
    editLoading.value = false
  }
}

const closeEdit = () => {
  if (saving.value) return
  editOpen.value = false
  editId.value = null
  editError.value = null
}

const openRenderReels = async (p: ProductItem) => {
  renderReelsProduct.value = p
  renderReelsMedia.value = { imageUrls: [] }
  renderReelsError.value = null
  renderReelsLoading.value = true
  renderReelsOpen.value = true

  try {
    const media = await api.get<any>(`admin/products/${p.id}/media`)
    renderReelsMedia.value = {
      imageUrls: Array.isArray(media?.imageUrls) ? media.imageUrls.filter((x: unknown) => typeof x === 'string' && x.trim()) : [],
    }

    if (renderReelsMedia.value.imageUrls.length === 0) {
      renderReelsError.value = 'Sản phẩm này chưa có ảnh để render reel.'
    }
  } catch (e: any) {
    renderReelsError.value = e?.message ?? 'Không lấy được danh sách ảnh của sản phẩm.'
  } finally {
    renderReelsLoading.value = false
  }
}

const onRenderReelsCompleted = ({ images }: { blob: Blob; url: string; images: string[] }) => {
  const productName = (renderReelsProduct.value?.name ?? '').trim() || 'sản phẩm'
  toast.success(`Render xong video cho ${productName} từ ${images.length} ảnh.`)
}

const save = async () => {
  if (!editId.value) return
  saving.value = true
  editError.value = null
  try {
    await api.put(`admin/products/${editId.value}`, {
      name: editModel.value.name,
      description: editModel.value.description,
    })
    await load()
    closeEdit()
  } catch (e: any) {
    editError.value = e?.message ?? 'Lưu thất bại.'
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  load()
})
</script>

<style scoped>
.page {
  padding: 16px;
  height: 100vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
  flex: 0 0 auto;
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

.input {
  height: 36px;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 0 12px;
  outline: none;
}

.input.input-sm {
  height: 28px;
  border-radius: 8px;
  padding: 0 8px;
  font-size: 11px;
  width: 100px;
}

.status {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  display: inline-block;
  background: #98a2b3;
}

.status-dot.ok {
  background: #12b76a;
}

.status-dot.pending {
  background: #f79009;
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

.btn.secondary.activeToggle {
  background: #111827;
  color: #fff;
}

.import-result {
  margin-top: 10px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
  background: #f9fafb;
  color: #111827;
  font-size: 13px;
}

.grid-wrap {
  flex: 1 1 auto;
  min-height: 0;
  overflow: auto;
  padding-right: 4px;
}

.grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 10px;
}

.card {
  border: 1px solid #eee;
  border-radius: 14px;
  overflow: hidden;
  background: #fff;
  text-align: left;
  padding: 0;
  display: flex;
  align-items: stretch;
}

.card-main {
  width: 100%;
  border: 0;
  padding: 0;
  margin: 0;
  text-align: left;
  background: transparent;
  cursor: pointer;
  display: flex;
  gap: 12px;
  align-items: center;
}

.card-body {
  padding: 10px 0;
  min-width: 0;
  flex: 1;
}

.card-actions {
  display: flex;
  gap: 8px;
  padding: 10px;
  border-left: 1px solid #f2f2f2;
  justify-content: flex-end;
  align-items: center;
  flex: 0 0 auto;
}

.btn-icon {
  width: 32px;
  height: 32px;
  border-radius: 10px;
  border: 1px solid #e5e7eb;
  background: #fff;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0;
}

.btn-icon svg {
  width: 16px;
  height: 16px;
}

.btn-icon:hover {
  background: #f9fafb;
}

.btn-icon:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-icon.danger {
  border-color: #fecdca;
  color: #b42318;
  background: #fff;
}

.btn-icon.danger:hover {
  background: #fef3f2;
}

.btn.tiny {
  height: 28px;
  padding: 0 10px;
  border-radius: 10px;
  font-size: 12px;
}

.media-box {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 10px;
  background: #fff;
}

.media-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 8px;
}

.media-actions {
  display: flex;
  gap: 8px;
}

.media-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
}

.media-item {
  display: grid;
  gap: 6px;
  align-items: center;
  justify-items: center;
  padding: 8px;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #fff;
  cursor: grab;
}

.media-item.selected {
  border-color: #94a3b8;
}

.media-item.dragging {
  opacity: 0.55;
}

.media-order {
  font-size: 12px;
  font-weight: 700;
  color: #667085;
}

.media-thumb {
  width: 100%;
  aspect-ratio: 1 / 1;
  object-fit: cover;
  border-radius: 10px;
  border: 1px solid #eee;
  background: #f5f5f5;
}

.media-list {
  display: grid;
  gap: 8px;
}

.media-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.platform-row {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
}

.platform-item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.thumb-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
}

.thumb-pick {
  border: 1px solid #e5e7eb;
  background: #fff;
  border-radius: 10px;
  padding: 0;
  overflow: hidden;
  cursor: pointer;
}

.thumb-pick img {
  width: 100%;
  aspect-ratio: 1 / 1;
  object-fit: cover;
  display: block;
}

.thumb-pick.active {
  border-color: #111827;
}

.video-preview {
  margin-top: 10px;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  overflow: hidden;
  background: #000;
}

.video-player {
  width: 100%;
  max-height: 320px;
  display: block;
}

@media (max-width: 860px) {
  .media-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

@media (max-width: 560px) {
  .media-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

.btn-action {
  height: 32px;
  padding: 0 10px;
  border-radius: 10px;
  border: 1px solid #e5e7eb;
  background: #fff;
  cursor: pointer;
  font-weight: 600;
  font-size: 12px;
}

.btn-action:hover {
  background: #f9fafb;
}

.btn-action.danger {
  border-color: #fecdca;
  color: #b42318;
  background: #fff;
}

.btn-action.danger:hover {
  background: #fef3f2;
}

.thumb {
  width: 92px;
  height: 92px;
  object-fit: cover;
  display: block;
  background: #f5f5f5;
}

.name {
  font-weight: 700;
  font-size: 13px;
  color: #111;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  min-height: 18px;
}

.desc {
  margin-top: 6px;
  font-size: 12px;
  color: #667085;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.info {
  margin-top: 8px;
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.info-item {
  font-size: 12px;
  color: #667085;
}

.info-item .k {
  color: #98a2b3;
}

.info-item .v {
  color: #344054;
  font-weight: 600;
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

.post-tabs {
  display: inline-flex;
  gap: 6px;
  padding: 4px;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #f9fafb;
  width: fit-content;
}

.post-tab {
  border: 0;
  background: transparent;
  padding: 8px 12px;
  border-radius: 10px;
  cursor: pointer;
  font-weight: 700;
  color: #475467;
}

.post-tab.active {
  background: #fff;
  border: 1px solid #e5e7eb;
  color: #111827;
}

.post-tab:disabled {
  opacity: 0.7;
  cursor: not-allowed;
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

  .page-header {
    flex-direction: column;
    align-items: stretch;
  }

  .toolbar {
    width: 100%;
  }

  .input {
    flex: 1;
  }

  .card-actions {
    padding: 10px;
  }
}

@media (max-width: 1024px) {
  .modal.modal-wide {
    min-width: 0;
  }
}
</style>
