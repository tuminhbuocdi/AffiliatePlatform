<template>
  <div class="product-detail-page" v-if="!loading && product">
    <div class="product-container">
      <div class="product-image-section">
        <img
          :src="product.imageUrl || '/placeholder-product.jpg'"
          :alt="product.name"
          class="main-product-image"
        />
      </div>
      
      <div class="product-info-section">
        <h1 class="product-title">{{ product.name }}</h1>
        
        <div class="price-section">
          <span v-if="product.price" class="current-price">
            {{ formatPrice(product.price) }}₫
          </span>
          <span v-if="product.originalPrice && product.originalPrice > (product.price || 0)" class="original-price">
            {{ formatPrice(product.originalPrice) }}₫
          </span>
          <span v-if="product.originalPrice && product.originalPrice > (product.price || 0)" class="discount">
            -{{ Math.round((1 - (product.price || 0) / product.originalPrice) * 100) }}%
          </span>
        </div>

        <div class="stats-section">
          <span v-if="product.rating" class="rating">
            ⭐ {{ product.rating }} ({{ Math.floor(Math.random() * 1000) + 50 }} đánh giá)
          </span>
          <span v-if="product.sold" class="sold">
            Đã bán {{ product.sold.toLocaleString() }} sản phẩm
          </span>
        </div>

        <div class="commission-section" v-if="product.commissionRate">
          <div class="commission-badge">
            🎯 Hoa hồng: {{ (product.commissionRate * 100).toFixed(1) }}%
          </div>
          <div class="commission-estimate">
            Ước tính nhận: {{ formatPrice((product.price || 0) * product.commissionRate) }}₫
          </div>
        </div>

        <div class="action-section">
          <button @click="handleBuyNow" class="buy-now-btn" :disabled="trackingClick">
            {{ trackingClick ? 'Đang xử lý...' : 'Mua Ngay' }}
          </button>
          <p class="affiliate-note">
            * Bạn sẽ được chuyển đến trang sàn TMĐT với link affiliate
          </p>
        </div>

        <div class="product-description">
          <h3>Thông tin sản phẩm</h3>
          <p>Sản phẩm này được đồng bộ từ sàn TMĐT. Khi bạn mua hàng qua link affiliate, 
          bạn sẽ nhận được hoa hồng dựa trên giá trị đơn hàng.</p>
        </div>
      </div>
    </div>
  </div>

  <div v-else-if="loading" class="loading">
    <div class="spinner"></div>
    <p>Đang tải thông tin sản phẩm...</p>
  </div>

  <div v-else class="error">
    <p>Không tìm thấy sản phẩm.</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import apiClient from '@/infrastructure/apiClient'

interface Product {
  id: number
  name: string
  slug?: string
  imageUrl?: string
  price?: number
  originalPrice?: number
  rating?: number
  sold?: number
  commissionRate?: number
  shopeeItemId?: number
}

const route = useRoute()
const router = useRouter()
const product = ref<Product | null>(null)
const loading = ref(true)
const trackingClick = ref(false)

const fetchProduct = async () => {
  try {
    loading.value = true
    const shopeeItemId = route.params.id as string
    const response = await apiClient.get(`/api/products/external/${shopeeItemId}`)
    product.value = response.data
  } catch (error) {
    console.error('Error fetching product:', error)
    product.value = null
  } finally {
    loading.value = false
  }
}

const handleBuyNow = async () => {
  if (!product.value || trackingClick.value) return

  try {
    trackingClick.value = true

    // Persist product + track click (server returns affiliate link)
    const clickResponse = await apiClient.post(`/api/products/buy-now`, {
      shopeeItemId: product.value.shopeeItemId,
      name: product.value.name,
      slug: product.value.slug,
      imageUrl: product.value.imageUrl,
      price: product.value.price,
      originalPrice: product.value.originalPrice,
      rating: product.value.rating,
      sold: product.value.sold,
      commissionRate: product.value.commissionRate,
      ipAddress: await getClientIP(),
      userAgent: navigator.userAgent
    })

    const { affiliateLink } = clickResponse.data

    // Open affiliate link in new tab
    window.open(affiliateLink, '_blank')

    // Show success message
    alert('Link affiliate đã được mở. Bạn sẽ nhận được hoa hồng khi mua hàng thành công!')
    
  } catch (error) {
    console.error('Error tracking click:', error)
    alert('Có lỗi xảy ra. Vui lòng thử lại.')
  } finally {
    trackingClick.value = false
  }
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

const formatPrice = (price: number) => {
  if (price == null || Number.isNaN(price)) return '-'
  return new Intl.NumberFormat('vi-VN').format(Math.round(price))
}

onMounted(() => {
  fetchProduct()
})
</script>

<style scoped>
.product-detail-page {
  padding: 20px;
  max-width: 1200px;
  margin: 0 auto;
}

.product-container {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 40px;
  background: white;
  border-radius: 12px;
  padding: 30px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.product-image-section {
  display: flex;
  align-items: center;
  justify-content: center;
}

.main-product-image {
  max-width: 100%;
  max-height: 400px;
  border-radius: 8px;
  object-fit: cover;
}

.product-info-section {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.product-title {
  font-size: 24px;
  font-weight: 700;
  color: #333;
  line-height: 1.3;
  margin: 0;
}

.price-section {
  display: flex;
  align-items: center;
  gap: 15px;
}

.current-price {
  font-size: 28px;
  font-weight: 700;
  color: #ff4d4f;
}

.original-price {
  font-size: 18px;
  color: #999;
  text-decoration: line-through;
}

.discount {
  background: #ff4d4f;
  color: white;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 600;
}

.stats-section {
  display: flex;
  gap: 20px;
  font-size: 16px;
  color: #666;
}

.rating {
  color: #faad14;
}

.commission-section {
  background: #f0f8ff;
  padding: 15px;
  border-radius: 8px;
  border-left: 4px solid #1890ff;
}

.commission-badge {
  font-size: 16px;
  font-weight: 600;
  color: #1890ff;
  margin-bottom: 5px;
}

.commission-estimate {
  font-size: 14px;
  color: #666;
}

.action-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.buy-now-btn {
  background: linear-gradient(135deg, #ff4d4f, #ff7875);
  color: white;
  border: none;
  padding: 15px 30px;
  font-size: 18px;
  font-weight: 600;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s ease;
}

.buy-now-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, #ff7875, #ff4d4f);
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(255, 77, 79, 0.3);
}

.buy-now-btn:disabled {
  background: #ccc;
  cursor: not-allowed;
  transform: none;
  box-shadow: none;
}

.affiliate-note {
  font-size: 12px;
  color: #999;
  margin: 0;
}

.product-description {
  border-top: 1px solid #eee;
  padding-top: 20px;
}

.product-description h3 {
  font-size: 18px;
  font-weight: 600;
  margin-bottom: 10px;
  color: #333;
}

.product-description p {
  color: #666;
  line-height: 1.6;
  margin: 0;
}

.loading {
  text-align: center;
  padding: 60px;
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

.error {
  text-align: center;
  padding: 60px;
  color: #666;
}

@media (max-width: 768px) {
  .product-detail-page {
    padding: 15px;
  }
  
  .product-container {
    grid-template-columns: 1fr;
    gap: 20px;
    padding: 20px;
  }
  
  .product-title {
    font-size: 20px;
  }
  
  .current-price {
    font-size: 24px;
  }
  
  .stats-section {
    flex-direction: column;
    gap: 10px;
  }
  
  .buy-now-btn {
    padding: 12px 24px;
    font-size: 16px;
  }
}
</style>
