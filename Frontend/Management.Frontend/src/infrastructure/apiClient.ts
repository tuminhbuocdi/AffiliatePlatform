import axios from 'axios'

const rawBaseUrl = import.meta.env?.VITE_API_BASE_URL ?? 'https://localhost:7179'
const apiBaseUrl = rawBaseUrl.replace(/\/+$/, '').replace(/\/api\/?$/, '')

const apiClient = axios.create({
  baseURL: apiBaseUrl,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor to handle errors
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token expired or invalid
      localStorage.removeItem('token')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

// AccessTrade datafeeds
export const getDataFeeds = async (domain: string, cat?: string, page = 1, limit = 50) => {
  const params: any = { domain, page, limit }
  if (cat) params.cat = cat
  const response = await apiClient.get('/api/accesstrade/datafeeds', { params })
  return response.data
}

// Shopee Open API
export const searchShopeeOpen = async (keyword: string, page = 1, pageSize = 20, categoryId?: number, sort?: string) => {
  const params: any = { keyword, page, page_size: pageSize }
  if (categoryId) params.category_id = categoryId
  if (sort) params.sort = sort
  const response = await apiClient.get('/api/shopeeopenapi/search', { params })
  return response.data
}

export default apiClient
