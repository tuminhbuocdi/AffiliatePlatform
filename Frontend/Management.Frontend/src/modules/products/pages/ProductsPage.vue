<template>
  <div class="products-page">
    <div class="page-header">
      <div class="breadcrumb">
        <span>Trang chủ</span>
        <span class="sep">/</span>
        <span>Hoa hồng</span>
        <span class="sep">/</span>
        <span class="current">Hoa hồng Sản phẩm</span>
      </div>

      <div class="header-row">
        <h1 class="page-title">Hoa hồng Sản phẩm</h1>

        <div class="search-box">
          <input
            v-model="searchQuery"
            type="text"
            placeholder="Tìm kiếm sản phẩm..."
            @keyup.enter="fetchProducts"
            class="search-input"
          />
          <button class="search-btn" @click="fetchProducts">Tìm kiếm</button>
        </div>

        <button class="import-btn" @click="showImportModal = true">
          📥 Import dữ liệu
        </button>
      </div>

      <div class="filter-row">
        <div class="filter-group">
          <div class="filter-label">Nguồn</div>
          <div class="muted">Database</div>
        </div>
      </div>
    </div>

    <div class="products-grid" v-if="!loading && products.length > 0">
      <div
        v-for="product in products"
        :key="product.id"
        class="product-card"
        @click="openProductModal(product)"
      >
        <div class="card-image">
          <img
            :src="product.imageUrl || '/placeholder-product.jpg'"
            :alt="product.name"
            class="product-image"
          />
        </div>

        <div class="product-info">
          <div class="product-name">{{ product.name }}</div>

          <div class="product-price">
            <span v-if="product.price" class="current-price">{{ formatPrice(product.price) }}₫</span>
            <span v-if="product.originalPrice && product.originalPrice > (product.price || 0)" class="original-price">
              {{ formatPrice(product.originalPrice) }}₫
            </span>
          </div>

          <div class="card-sub">
            <div class="commission" v-if="product.commissionRate">
              Tỉ lệ hoa hồng {{ (product.commissionRate * 100).toFixed(1) }}%
            </div>
            <button class="link-btn" type="button" @click.stop="openProductModal(product)">Lấy link</button>
          </div>
        </div>
      </div>
    </div>

    <div v-else-if="loading" class="loading">
      <div class="spinner"></div>
      <p>Đang tải sản phẩm...</p>
    </div>

    <div v-else class="no-products">
      <p>Không tìm thấy sản phẩm nào.</p>
    </div>

    <div v-if="isProductModalOpen" class="modal-overlay" @click.self="closeProductModal">
      <div class="modal-content product-detail-modal">
        <div class="modal-header">
          <div class="detail-title" v-if="modalDetail">
            <div class="title-line">{{ modalDetail.name }}</div>
            <div class="sub-line">
              <span class="pill">Sản phẩm từ Shopee</span>
            </div>
          </div>
          <div v-else class="detail-title">
            <div class="title-line">Chi tiết sản phẩm</div>
          </div>

          <button class="modal-close" @click="closeProductModal">×</button>
        </div>

        <div v-if="modalLoading" class="modal-loading">
          <div class="spinner"></div>
          <p>Đang tải thông tin sản phẩm...</p>
        </div>

        <div v-else-if="modalDetail" class="modal-body">
          <div class="detail-grid">
            <div class="gallery">
              <div class="main">
                <div
                  v-if="galleryItems.length > 1"
                  class="nav-hit prev"
                >
                  <button class="nav-btn" type="button" @click="goPrev" aria-label="Previous">‹</button>
                </div>

                <div
                  v-if="galleryItems.length > 1"
                  class="nav-hit next"
                >
                  <button class="nav-btn" type="button" @click="goNext" aria-label="Next">›</button>
                </div>

                <img
                  v-if="selectedGalleryItem?.type === 'image'"
                  :src="selectedGalleryItem?.url || modalDetail.imageUrl || '/placeholder-product.jpg'"
                  :alt="modalDetail.name"
                />

                <video
                  v-else-if="selectedGalleryItem?.type === 'video'"
                  class="main-video"
                  :src="selectedGalleryItem?.url"
                  controls
                  playsinline
                  ref="mainVideoRef"
                  autoplay
                />

                <img
                  v-else
                  :src="modalDetail.imageUrl || '/placeholder-product.jpg'"
                  :alt="modalDetail.name"
                />
              </div>

              <div v-if="galleryItems.length > 0" class="thumbs">
                <button
                  v-for="(it, i) in galleryItems"
                  :key="it.type + ':' + it.url + ':' + i"
                  type="button"
                  class="thumb"
                  :class="{ active: selectedGalleryIndex === i }"
                  @click="selectedGalleryIndex = i"
                >
                  <img v-if="it.type === 'image'" :src="it.url" alt="" />
                  <div v-else class="thumb-video">
                    <img :src="modalDetail.imageUrl || '/placeholder-product.jpg'" alt="" />
                    <div class="play-mask" aria-hidden="true"></div>
                  </div>
                </button>
              </div>
            </div>

            <div class="info">
              <div class="price-row">
                <div class="price">
                  <div v-if="modalDetail.price" class="current">{{ formatPrice(modalDetail.price) }}₫</div>
                  <div v-if="modalDetail.originalPrice && modalDetail.originalPrice > (modalDetail.price || 0)" class="original">
                    {{ formatPrice(modalDetail.originalPrice) }}₫
                  </div>
                  <div class="updated-note">
                    Thông tin được cập nhật gần nhất vào : {{ formatDateTime(modalDetail.updatedAt || modalDetail.createdAt) }}
                  </div>
                </div>
                <div class="kpis">
                  <div class="kpi" v-if="modalDetail.rating != null">
                    <div class="k">Rating</div>
                    <div class="v">{{ Number(modalDetail.rating).toFixed(2) }}</div>
                  </div>
                  <div class="kpi" v-if="modalDetail.sold != null">
                    <div class="k">Đã bán</div>
                    <div class="v">{{ modalDetail.sold }}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div v-else class="modal-error">
          <p>Không tìm thấy sản phẩm.</p>
        </div>

        <div class="modal-footer" v-if="modalDetail">
          <div class="detail-actions">
            <button class="btn-ghost" type="button" :disabled="buying" @click="handleBuyNow">
              {{ buying ? 'Đang xử lý...' : 'Mua ngay' }}
            </button>
            <a v-if="modalDetail.productLink" class="btn-link" :href="modalDetail.productLink" target="_blank" rel="noreferrer">
              Mở product link
            </a>
          </div>
        </div>
      </div>
    </div>

    <!-- Import Modal -->
    <div
      v-if="showImportModal"
      class="modal-overlay"
      @click.self="showImportModal = false"
    >
      <div class="modal-content import-modal">
        <button class="modal-close" @click="showImportModal = false">×</button>
        
        <h2 class="modal-title">Import dữ liệu sản phẩm</h2>
        <p class="import-description">
          Dán JSON chứa danh sách sản phẩm vào textarea bên dưới. 
          Hệ thống sẽ tự động bóc tách và lưu vào database.
        </p>
        
        <div class="import-form">
          <textarea
            v-model="importData"
            class="import-textarea"
            placeholder="Paste JSON data here..."
            rows="10"
          ></textarea>
          
          <div class="import-actions">
            <button 
              class="import-submit-btn" 
              @click="handleImport"
              :disabled="importing || !importData.trim()"
            >
              {{ importing ? 'Đang import...' : 'Import dữ liệu' }}
            </button>
            <button class="import-cancel-btn" @click="showImportModal = false">
              Hủy
            </button>
          </div>
        </div>
        
        <div v-if="importResult" class="import-result">
          <h3>Kết quả import:</h3>
          <p>{{ importResult }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, nextTick, watch } from 'vue'
import apiClient from '@/infrastructure/apiClient'

interface Product {
  id: number
  platform?: string
  externalItemId?: string
  shopeeItemId?: string
  name: string
  slug?: string
  imageUrl?: string
  price?: number
  originalPrice?: number
  rating?: number
  sold?: number
  commissionRate?: number
}

const products = ref<Product[]>([])
const loading = ref(true)
const searchQuery = ref('')

const isProductModalOpen = ref(false)
const modalLoading = ref(false)
const modalProduct = ref<Product | null>(null)
const buying = ref(false)

type GalleryItem = {
  type: 'image' | 'video'
  url: string
}

type ProductDetail = {
  id: number
  platform?: string
  externalItemId?: string
  shopeeItemId?: string
  shopId?: string
  name: string
  slug?: string
  createdAt?: string
  updatedAt?: string
  imageUrl?: string
  imageUrls?: string[]
  price?: number
  originalPrice?: number
  rating?: number
  sold?: number
  commissionRate?: number
  defaultCommissionRateRaw?: string
  sellerCommissionRateRaw?: string
  maxCommissionRateRaw?: string
  productLink?: string
  longLink?: string
  labelIds?: string[]
  videoUrls?: string[]
  rawJson?: string
}

const modalDetail = ref<ProductDetail | null>(null)
const galleryItems = ref<GalleryItem[]>([])
const selectedGalleryIndex = ref(0)

const selectedGalleryItem = computed(() => {
  return galleryItems.value[selectedGalleryIndex.value] || null
})

const mainVideoRef = ref<HTMLVideoElement | null>(null)

const rebuildGallery = (detail: ProductDetail | null) => {
  const items: GalleryItem[] = []
  const seen = new Set<string>()

  const push = (type: 'image' | 'video', url?: string) => {
    const u = (url || '').trim()
    if (!u) return
    const k = `${type}:${u}`
    if (seen.has(k)) return
    seen.add(k)
    items.push({ type, url: u })
  }

  // Ưu tiên video lên đầu
  if (detail?.videoUrls && detail.videoUrls.length > 0) {
    for (const u of detail.videoUrls) push('video', u)
  }

  if (detail?.imageUrls && detail.imageUrls.length > 0) {
    for (const u of detail.imageUrls) push('image', u)
  } else {
    push('image', detail?.imageUrl)
  }

  galleryItems.value = items
  selectedGalleryIndex.value = 0
}

const goPrev = () => {
  const n = galleryItems.value.length
  if (n <= 1) return
  selectedGalleryIndex.value = (selectedGalleryIndex.value - 1 + n) % n
}

const goNext = () => {
  const n = galleryItems.value.length
  if (n <= 1) return
  selectedGalleryIndex.value = (selectedGalleryIndex.value + 1) % n
}

// Import modal state
const showImportModal = ref(false)
const importData = ref('')
const importing = ref(false)
const importResult = ref('')

const fetchProducts = async () => {
  try {
    loading.value = true

    const params: any = { limit: 50 }
    if (searchQuery.value.trim()) params.search = searchQuery.value.trim()
    const response = await apiClient.get('/api/products/db', { params })
    products.value = response.data
  } catch (error) {
    console.error('Error fetching products:', error)
    products.value = []
  } finally {
    loading.value = false
  }
}

const openProductModal = async (product: Product) => {
  isProductModalOpen.value = true
  modalProduct.value = product

  try {
    modalLoading.value = true
    galleryItems.value = []
    selectedGalleryIndex.value = 0
    const res = await apiClient.get(`/api/products/db/${product.id}`)
    modalDetail.value = res.data
    rebuildGallery(modalDetail.value)
  } catch (error) {
    console.error('Error fetching product detail:', error)
  } finally {
    modalLoading.value = false
  }
}

const closeProductModal = () => {
  isProductModalOpen.value = false
  modalProduct.value = null
  modalDetail.value = null
  galleryItems.value = []
  selectedGalleryIndex.value = 0
  modalLoading.value = false
  buying.value = false
}

const getClientIP = async (): Promise<string> => {
  try {
    const response = await fetch('https://api.ipify.org?format=json')
    const data = await response.json()
    return data.ip
  } catch {
    return ''
  }
}

const handleBuyNow = async () => {
  if ((!modalProduct.value && !modalDetail.value) || buying.value) return

  try {
    buying.value = true
    let affiliateLink = ''

    const p = modalDetail.value ?? modalProduct.value
    if (!p) return

    const clickResponse = await apiClient.post(`/api/products/buy-now`, {
      platform: p.platform || 'Shopee',
      externalItemId: p.externalItemId || p.shopeeItemId || '',
      shopeeItemId: p.shopeeItemId || '',
      name: p.name,
      slug: p.slug,
      imageUrl: p.imageUrl,
      price: p.price,
      originalPrice: p.originalPrice,
      rating: p.rating,
      sold: p.sold,
      commissionRate: p.commissionRate,
      ipAddress: await getClientIP(),
      userAgent: navigator.userAgent,
    })
    affiliateLink = clickResponse.data.affiliateLink

    if (affiliateLink) {
      window.open(affiliateLink, '_blank')
      alert('Link affiliate đã được mở. Bạn sẽ nhận được hoa hồng khi mua hàng thành công!')
    } else {
      alert('Không thể tạo link affiliate.')
    }
  } catch (error) {
    console.error('Error buy-now:', error)
    alert('Có lỗi xảy ra. Vui lòng thử lại.')
  } finally {
    buying.value = false
  }
}

const formatPrice = (price: number) => {
  if (price == null || Number.isNaN(price)) return '-'
  return new Intl.NumberFormat('vi-VN').format(Math.round(price))
}

const formatDateTime = (value?: string) => {
  if (!value) return '-'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d)
}

const handleImport = async () => {
  if (!importData.value.trim()) return
  
  try {
    importing.value = true
    importResult.value = ''
    
    const response = await apiClient.post('/api/products/import', {
      data: importData.value.trim()
    })
    
    importResult.value = response.data.message
    // Refresh products list after successful import
    fetchProducts()
  } catch (error: any) {
    console.error('Import error:', error)
    importResult.value = error.response?.data?.message || 'Import thất bại. Vui lòng kiểm tra lại dữ liệu.'
  } finally {
    importing.value = false
  }
}

// Auto-play video with volume 45% when video becomes selected
watch(selectedGalleryItem, async (item) => {
  if (item?.type === 'video') {
    await nextTick()
    if (mainVideoRef.value) {
      mainVideoRef.value.volume = 0.45
      // Try to play (some browsers require user interaction)
      try {
        await mainVideoRef.value.play()
      } catch (e) {
        // Autoplay without user interaction may be blocked
        console.debug('Autoplay blocked, user needs to click play:', e)
      }
    }
  }
})

onMounted(() => {
  fetchProducts()
})
</script>

<style scoped>
.products-page {
  max-width: 100%;
  margin: 0;
  background: #f6f6f6;
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
  margin-bottom: 12px;
}

.page-title {
  font-size: 16px;
  font-weight: 600;
  color: #222;
  margin: 0;
  min-width: 180px;
}

.search-box {
  display: flex;
  align-items: center;
  flex: 1;
  border: 1px solid #f26f3b;
  border-radius: 2px;
  overflow: hidden;
  background: #fff;
}

.search-input {
  flex: 1;
  min-width: 280px;
  padding: 10px 12px;
  border: none;
  outline: none;
  font-size: 13px;
  background: transparent;
}

.search-btn {
  background: #f26f3b;
  color: #fff;
  border: none;
  padding: 10px 16px;
  font-size: 13px;
  cursor: pointer;
}

.search-btn:hover {
  background: #ea5f28;
}

.import-btn {
  background: #52c41a;
  color: white;
  border: none;
  padding: 10px 16px;
  font-size: 13px;
  cursor: pointer;
  border-radius: 2px;
  white-space: nowrap;
}

.import-btn:hover {
  background: #389e0d;
}

.filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 18px;
}

.filter-group {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.filter-label {
  font-size: 12px;
  color: #777;
  min-width: 80px;
}

.sort-tabs {
  display: flex;
  gap: 8px;
}

.sort-tab {
  border: 1px solid #f26f3b;
  background: #fff;
  color: #f26f3b;
  padding: 6px 10px;
  font-size: 12px;
  border-radius: 2px;
  cursor: pointer;
}

.sort-tab.active {
  background: #f26f3b;
  color: #fff;
}

.filter-select {
  border: 1px solid #ddd;
  border-radius: 2px;
  padding: 6px 10px;
  font-size: 12px;
  background: #fff;
  min-width: 140px;
}

.products-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 14px;
}

.product-card {
  border: 1px solid #eee;
  border-radius: 2px;
  overflow: hidden;
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
  background: white;
}

.product-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.08);
}

.card-image {
  background: #fafafa;
}

.product-image {
  width: 100%;
  height: 170px;
  object-fit: cover;
}

.product-info {
  padding: 10px;
}

.product-name {
  font-size: 12px;
  font-weight: 500;
  margin-bottom: 8px;
  line-height: 1.3;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.product-price {
  margin-bottom: 8px;
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.current-price {
  font-size: 14px;
  font-weight: 700;
  color: #ff4d4f;
}

.original-price {
  font-size: 11px;
  color: #999;
  text-decoration: line-through;
}

.card-sub {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
  color: #666;
}

.commission {
  font-size: 11px;
  color: #999;
}

.link-btn {
  border: 1px solid #f26f3b;
  color: #f26f3b;
  background: #fff;
  border-radius: 2px;
  font-size: 11px;
  padding: 4px 8px;
  cursor: pointer;
  white-space: nowrap;
}

.link-btn:hover {
  background: #fff3ee;
}

.commission-info {
  background: #f0f8ff;
  color: #1890ff;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
}

.loading {
  text-align: center;
  padding: 40px;
}

.spinner {
  border: 4px solid #f3f3f3;
  border-top: 4px solid #3498db;
  border-radius: 50%;
  width: 40px;
  height: 40px;
  animation: spin 1s linear infinite;
  margin: 0 auto 20px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.no-products {
  text-align: center;
  padding: 40px;
  color: #666;
}

@media (max-width: 768px) {
  .products-page {
    padding: 15px;
  }

  .header-row {
    flex-direction: column;
    align-items: stretch;
  }

  .page-title {
    min-width: 0;
  }

  .search-box {
    width: 100%;
  }

  .filter-row {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;
  }

  .filter-group {
    width: 100%;
  }

  .filter-label {
    min-width: 0;
  }

  .filter-select {
    min-width: 0;
    width: 100%;
  }
  
  .products-grid {
    grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
    gap: 15px;
  }
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  padding: 20px;
}

.modal-content {
  width: 100%;
  max-width: 900px;
  background: white;
  border-radius: 12px;
  padding: 24px;
  position: relative;
  max-height: 90vh;
  overflow: hidden; /* prevent outer scroll */
  display: flex;
  flex-direction: column;
}

.modal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding: 0 0 12px;
  border-bottom: 1px solid #eee;
  flex: 0 0 auto;
}

.modal-footer {
  flex: 0 0 auto;
  padding: 12px 0 0;
  border-top: 1px solid #eee;
  background: #fff;
}

.modal-close {
  position: static;
  width: 36px;
  height: 36px;
  border: none;
  background: #f0f0f0;
  border-radius: 8px;
  cursor: pointer;
  font-size: 20px;
  line-height: 1;
  z-index: 20;
}

.modal-loading,
.modal-error {
  text-align: center;
  padding: 24px;
}

.modal-body {
  overflow-y: auto;
  flex: 1;
  padding: 16px 0;
  padding-right: 4px; /* space for scrollbar */
}

/* Product Detail Modal */
.product-detail-modal {
  max-width: 1080px;
  padding: 18px;
  display: flex;
  flex-direction: column;
  max-height: 90vh;
}

.product-detail-modal .modal-header {
  padding: 0 0 10px;
}

.product-detail-modal .modal-footer {
  padding: 10px 0 0;
}

.detail-title .title-line {
  font-size: 16px;
  font-weight: 700;
  color: #222;
  line-height: 1.35;
}

.detail-title .sub-line {
  margin-top: 8px;
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
}

.pill {
  display: inline-flex;
  padding: 4px 10px;
  border-radius: 999px;
  border: 1px solid #f26f3b;
  background: #fff3ee;
  color: #f26f3b;
  font-size: 12px;
  font-weight: 600;
}

.muted {
  color: #777;
  font-size: 12px;
}

.detail-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.btn-ghost {
  border: 1px solid #e5e7eb;
  background: #fff;
  color: #111;
  padding: 10px 14px;
  border-radius: 10px;
  cursor: pointer;
  font-weight: 600;
}

.btn-ghost:hover {
  background: #f9fafb;
}

.btn-ghost.small {
  padding: 8px 12px;
  border-radius: 8px;
  font-size: 12px;
}

.btn-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 10px 14px;
  border-radius: 10px;
  border: 1px solid #ffe2d6;
  background: #fff;
  color: #f26f3b;
  text-decoration: none;
  font-weight: 600;
}

.btn-link:hover {
  background: #fff3ee;
}

.detail-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.gallery {
  border: 1px solid #eee;
  border-radius: 14px;
  overflow: hidden;
  background: #fff;
}

.gallery .main {
  background: #fafafa;
  position: relative;
  overflow: hidden;
}

.gallery .main img {
  width: 100%;
  height: 420px;
  object-fit: contain;
  display: block;
}

.main-video {
  width: 100%;
  height: 420px;
  display: block;
  background: #000;
}

.nav-hit {
  position: absolute;
  top: 64px;
  bottom: 64px;
  z-index: 10;
  width: 72px;
  display: flex;
  align-items: center;
}

.nav-hit.prev {
  left: 0;
  justify-content: flex-start;
}

.nav-hit.next {
  right: 0;
  justify-content: flex-end;
}

.nav-btn {
  width: 40px;
  height: 40px;
  margin: 0 10px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.6);
  background: rgba(0, 0, 0, 0.35);
  color: #fff;
  font-size: 24px;
  line-height: 1;
  cursor: pointer;
}

.nav-btn:hover {
  background: rgba(0, 0, 0, 0.5);
}

.thumbs {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
  padding: 10px;
  background: #fff;
}

.thumb {
  border: 1px solid #eee;
  background: #fff;
  border-radius: 10px;
  overflow: hidden;
  cursor: pointer;
  padding: 0;
}

.thumb.active {
  border-color: #f26f3b;
  box-shadow: 0 0 0 2px rgba(242, 111, 59, 0.15);
}

.thumb img {
  width: 100%;
  height: 64px;
  object-fit: cover;
  display: block;
}

.thumb-video {
  position: relative;
  height: 64px;
  background: #111;
}

.thumb-video img {
  width: 100%;
  height: 64px;
  object-fit: cover;
  display: block;
}

.play-mask {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(180deg, rgba(0, 0, 0, 0.08), rgba(0, 0, 0, 0.35));
}

.play-mask::before {
  content: '';
  width: 22px;
  height: 22px;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.55);
  box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.55);
}

.play-mask::after {
  content: '';
  position: absolute;
  width: 0;
  height: 0;
  border-top: 6px solid transparent;
  border-bottom: 6px solid transparent;
  border-left: 10px solid rgba(255, 255, 255, 0.95);
  transform: translateX(2px);
}

.info {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.price-row {
  border: 1px solid #eee;
  border-radius: 14px;
  padding: 12px;
  background: #fff;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
}

.price .current {
  font-size: 20px;
  font-weight: 800;
  color: #ff4d4f;
}

.price .original {
  margin-top: 6px;
  font-size: 13px;
  color: #999;
  text-decoration: line-through;
}

.updated-note {
  margin-top: 6px;
  font-size: 12px;
  color: #777;
}

.kpis {
  display: flex;
  gap: 10px;
}

.kpi {
  border: 1px solid #f2f2f2;
  border-radius: 12px;
  padding: 10px;
  min-width: 110px;
  background: #fff;
}

.kpi .k {
  font-size: 12px;
  color: #777;
}

.kpi .v {
  margin-top: 4px;
  font-size: 16px;
  font-weight: 800;
  color: #111;
}

@media (max-width: 980px) {
  .detail-grid {
    grid-template-columns: 1fr;
  }
  .gallery .main img,
  .main-video {
    height: 320px;
  }
  .thumbs {
    grid-template-columns: repeat(5, 1fr);
  }
}

@media (max-width: 640px) {
  .modal-content {
    padding: 12px;
    margin: 8px;
    max-height: 95vh;
    padding-bottom: 0; /* bottom handled by sticky actions */
  }

  .modal-body {
    padding-right: 2px;
  }

  .detail-title .title-line {
    font-size: 14px;
  }

  .pill {
    font-size: 11px;
    padding: 3px 8px;
  }

  .detail-grid {
    gap: 12px;
  }

  .gallery .main img,
  .main-video {
    height: 240px;
  }

  .thumbs {
    grid-template-columns: repeat(4, 1fr);
    gap: 6px;
    padding: 8px;
  }

  .thumb {
    border-radius: 8px;
  }

  .thumb img,
  .thumb-video {
    height: 56px;
  }

  .nav-hit {
    width: 56px;
  }

  .nav-btn {
    width: 36px;
    height: 36px;
    font-size: 20px;
    margin: 0 6px;
  }

  .price-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .kpis {
    width: 100%;
    justify-content: flex-start;
  }

  .kpi {
    min-width: 0;
    flex: 1;
  }

  .detail-actions {
    flex-direction: column;
    gap: 10px;
  }

  .btn-ghost,
  .btn-link {
    width: 100%;
    justify-content: center;
    padding: 12px;
  }
}

.modal-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px;
}

.modal-image img {
  width: 100%;
  height: 360px;
  object-fit: cover;
  border-radius: 10px;
}

.modal-title {
  margin: 0 0 12px;
}

.modal-actions {
  margin-top: 16px;
}

.buy-now-btn {
  background: linear-gradient(135deg, #ff4d4f, #ff7875);
  color: white;
  border: none;
  padding: 12px 18px;
  font-size: 16px;
  font-weight: 600;
  border-radius: 8px;
  cursor: pointer;
}

.buy-now-btn:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.affiliate-note {
  font-size: 12px;
  color: #999;
  margin: 8px 0 0;
}

/* Import Modal Styles */
.import-modal {
  max-width: 700px;
}

.import-description {
  color: #666;
  margin-bottom: 20px;
  line-height: 1.5;
}

.import-form {
  margin-bottom: 20px;
}

.import-textarea {
  width: 100%;
  min-height: 300px;
  padding: 12px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-family: 'Courier New', monospace;
  font-size: 12px;
  resize: vertical;
}

.import-textarea:focus {
  outline: none;
  border-color: #52c41a;
}

.import-actions {
  display: flex;
  gap: 12px;
  margin-top: 16px;
}

.import-submit-btn {
  background: #52c41a;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
}

.import-submit-btn:hover:not(:disabled) {
  background: #389e0d;
}

.import-submit-btn:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.import-cancel-btn {
  background: #f5f5f5;
  color: #666;
  border: 1px solid #ddd;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
}

.import-cancel-btn:hover {
  background: #e8e8e8;
}

.import-result {
  background: #f6ffed;
  border: 1px solid #b7eb8f;
  border-radius: 4px;
  padding: 12px;
}

.import-result h3 {
  margin: 0 0 8px;
  color: #52c41a;
  font-size: 14px;
}

.import-result p {
  margin: 0;
  color: #389e0d;
}

@media (max-width: 768px) {
  .modal-grid {
    grid-template-columns: 1fr;
  }

  .modal-image img {
    height: 280px;
  }
}
</style>
