<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Quản lý link Affiliate</h1>
        <div class="muted">Danh sách link Affiliate, thêm / sửa / xoá. Tìm theo ExternalItemId.</div>
      </div>

      <div class="toolbar">
        <input v-model="search" class="input" placeholder="ExternalItemId..." @keyup.enter="load" />
        <select v-model="filter" class="input" @change="load">
          <option value="All">All</option>
          <option value="Have AffiliateLink">Have AffiliateLink</option>
          <option value="Not Affliliate Link">Not Affliliate Link</option>
          <option value="Have Socials">Have Socials</option>
          <option value="Not Socials">Not Socials</option>
        </select>
        <button class="btn" type="button" @click="openCreate">Thêm</button>
        <button class="btn secondary" type="button" @click="load" :disabled="loading">{{ loading ? 'Đang tải...' : 'Tải' }}</button>
        <button class="btn secondary" type="button" @click="exportAffiliateLinks" :disabled="loading || exporting">
          {{ exporting ? 'Đang xuất...' : 'Xuất khẩu' }}
        </button>
        <button class="btn secondary" type="button" @click="openImportAffiliate" :disabled="loading || importingAffiliate">Import link Affiliate</button>
      </div>
    </div>

    <div v-if="error" class="error">{{ error }}</div>

    <div v-if="loading" class="loading">Đang tải...</div>

    <div v-else class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th>ExternalItemId</th>
            <th>ProductLink</th>
            <th>SubId1</th>
            <th>SubId2</th>
            <th>SubId3</th>
            <th>SubId4</th>
            <th>SubId5</th>
            <th>AffiliateLink</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="x in items" :key="x.externalItemId">
            <td class="mono">{{ x.externalItemId }}</td>
            <td class="truncate"><a v-if="x.productLink" :href="x.productLink" target="_blank" rel="noreferrer">{{ x.productLink }}</a><span v-else>-</span></td>
            <td class="mono">{{ x.subId1 || '-' }}</td>
            <td class="mono">{{ x.subId2 || '-' }}</td>
            <td class="mono">{{ x.subId3 || '-' }}</td>
            <td class="mono">{{ x.subId4 || '-' }}</td>
            <td class="mono">{{ x.subId5 || '-' }}</td>
            <td class="truncate"><a v-if="x.affiliateLink" :href="x.affiliateLink" target="_blank" rel="noreferrer">{{ x.affiliateLink }}</a><span v-else>-</span></td>
            <td class="actions">
              <button class="link" type="button" @click="openEdit(x)">Sửa</button>
              <button class="link danger" type="button" @click="remove(x)" :disabled="removingId === x.externalItemId">Xoá</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="modalOpen" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <div class="modal-header">
          <div class="modal-title">{{ isCreate ? 'Thêm link Affiliate' : 'Sửa link Affiliate' }}</div>
          <button class="icon" type="button" @click="closeModal">×</button>
        </div>

        <div class="modal-body">
          <div class="form">
            <label>
              <div class="lbl">ExternalItemId</div>
              <input v-model="model.externalItemId" class="input" :disabled="!isCreate" />
            </label>

            <label>
              <div class="lbl">ProductLink</div>
              <input v-model="model.productLink" class="input" placeholder="https://..." />
            </label>

            <div class="grid2">
              <label>
                <div class="lbl">SubId1</div>
                <input v-model="model.subId1" class="input" />
              </label>
              <label>
                <div class="lbl">SubId2</div>
                <input v-model="model.subId2" class="input" />
              </label>
              <label>
                <div class="lbl">SubId3</div>
                <input v-model="model.subId3" class="input" />
              </label>
              <label>
                <div class="lbl">SubId4</div>
                <input v-model="model.subId4" class="input" />
              </label>
              <label>
                <div class="lbl">SubId5</div>
                <input v-model="model.subId5" class="input" />
              </label>
            </div>

            <label>
              <div class="lbl">AffiliateLink</div>
              <input v-model="model.affiliateLink" class="input" placeholder="https://..." />
            </label>
          </div>

          <div v-if="modalError" class="error">{{ modalError }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn" type="button" @click="save" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Lưu' }}</button>
          <button class="btn secondary" type="button" @click="closeModal" :disabled="saving">Đóng</button>
        </div>
      </div>
    </div>

    <div v-if="showImportAffiliateModal" class="modal-overlay" @click.self="closeImportAffiliate">
      <div class="modal">
        <div class="modal-header">
          <div class="modal-title">Import link Affiliate</div>
          <button class="icon" type="button" @click="closeImportAffiliate">×</button>
        </div>

        <div class="modal-body">
          <div class="muted">Upload file (.xlsx hoặc .csv) có các cột: "Liên kết gốc", "Sub_id1".."Sub_id5", "Liên kết chuyển đổi"</div>
          <input type="file" class="input" accept=".xlsx,.csv,text/csv" @change="onAffiliateFileChange" />
          <div v-if="affiliateImportResult" class="import-result">{{ affiliateImportResult }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn" type="button" @click="handleImportAffiliate" :disabled="importingAffiliate || !affiliateFile">
            {{ importingAffiliate ? 'Đang import...' : 'Import Affiliate' }}
          </button>
          <button class="btn secondary" type="button" @click="closeImportAffiliate" :disabled="importingAffiliate">Đóng</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import api from '@/infrastructure/http/apiClient'

type AffiliateLinkItem = {
  id: number
  externalItemId: string
  productLink: string
  subId1?: string | null
  subId2?: string | null
  subId3?: string | null
  subId4?: string | null
  subId5?: string | null
  affiliateLink?: string | null
  createdAt: string
  updatedAt?: string | null
}

const items = ref<AffiliateLinkItem[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

const search = ref('')
const filter = ref('All')
const exporting = ref(false)

const showImportAffiliateModal = ref(false)
const affiliateFile = ref<File | null>(null)
const importingAffiliate = ref(false)
const affiliateImportResult = ref('')

const modalOpen = ref(false)
const isCreate = ref(true)
const saving = ref(false)
const modalError = ref<string | null>(null)

const removingId = ref<string | null>(null)

const model = ref({
  externalItemId: '',
  productLink: '',
  subId1: '',
  subId2: '',
  subId3: '',
  subId4: '',
  subId5: '',
  affiliateLink: '',
})

const load = async () => {
  loading.value = true
  error.value = null
  try {
    const q = search.value.trim()
    const data = await api.request<AffiliateLinkItem[]>({
      method: 'GET',
      url: 'admin/affiliate-links',
      params: {
        search: q || undefined,
        filter: filter.value && filter.value !== 'All' ? filter.value : undefined,
      },
    })
    items.value = data
  } catch (e: any) {
    error.value = e?.message ?? 'Không tải được danh sách affiliate links.'
  } finally {
    loading.value = false
  }
}

const openImportAffiliate = () => {
  showImportAffiliateModal.value = true
  affiliateImportResult.value = ''
  affiliateFile.value = null
}

const closeImportAffiliate = () => {
  if (importingAffiliate.value) return
  showImportAffiliateModal.value = false
  affiliateImportResult.value = ''
  affiliateFile.value = null
}

const onAffiliateFileChange = (ev: Event) => {
  const input = ev.target as HTMLInputElement
  const f = input.files?.[0] ?? null
  affiliateFile.value = f
}

const handleImportAffiliate = async () => {
  if (!affiliateFile.value) return
  importingAffiliate.value = true
  affiliateImportResult.value = ''
  try {
    const form = new FormData()
    form.append('file', affiliateFile.value)

    const res = await api.request<any>({
      method: 'POST',
      url: 'admin/products/import-affiliate-links',
      data: form,
      headers: { 'Content-Type': 'multipart/form-data' },
    })

    const imported = res?.imported ?? 0
    const skipped = res?.skipped ?? 0
    const errCount = Array.isArray(res?.errors) ? res.errors.length : 0
    affiliateImportResult.value = `Import xong. Imported=${imported}, Skipped=${skipped}, Errors=${errCount}`
    await load()
  } catch (e: any) {
    affiliateImportResult.value = e?.message ?? 'Import thất bại.'
  } finally {
    importingAffiliate.value = false
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
        search: search.value.trim() || undefined,
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

const openCreate = () => {
  isCreate.value = true
  modalError.value = null
  model.value = {
    externalItemId: '',
    productLink: '',
    subId1: '',
    subId2: '',
    subId3: '',
    subId4: '',
    subId5: '',
    affiliateLink: '',
  }
  modalOpen.value = true
}

const openEdit = (x: AffiliateLinkItem) => {
  void (async () => {
    isCreate.value = false
    modalError.value = null
    try {
      const detail = await api.get<AffiliateLinkItem>(`admin/affiliate-links/${encodeURIComponent(x.externalItemId)}`)
      model.value = {
        externalItemId: detail.externalItemId,
        productLink: detail.productLink ?? '',
        subId1: detail.subId1 ?? '',
        subId2: detail.subId2 ?? '',
        subId3: detail.subId3 ?? '',
        subId4: detail.subId4 ?? '',
        subId5: detail.subId5 ?? '',
        affiliateLink: detail.affiliateLink ?? '',
      }
      modalOpen.value = true
    } catch (e: any) {
      modalError.value = e?.message ?? 'Không tải được chi tiết affiliate link.'
      modalOpen.value = true
    }
  })()
}

const closeModal = () => {
  if (saving.value) return
  modalOpen.value = false
}

const save = async () => {
  saving.value = true
  modalError.value = null
  try {
    if (isCreate.value) {
      await api.post('admin/affiliate-links', {
        externalItemId: model.value.externalItemId,
        productLink: model.value.productLink,
        subId1: model.value.subId1,
        subId2: model.value.subId2,
        subId3: model.value.subId3,
        subId4: model.value.subId4,
        subId5: model.value.subId5,
        affiliateLink: model.value.affiliateLink,
      })
    } else {
      await api.put(`admin/affiliate-links/${encodeURIComponent(model.value.externalItemId)}`, {
        productLink: model.value.productLink,
        subId1: model.value.subId1,
        subId2: model.value.subId2,
        subId3: model.value.subId3,
        subId4: model.value.subId4,
        subId5: model.value.subId5,
        affiliateLink: model.value.affiliateLink,
      })
    }

    await load()
    closeModal()
  } catch (e: any) {
    modalError.value = e?.message ?? 'Lưu thất bại.'
  } finally {
    saving.value = false
  }
}

const remove = async (x: AffiliateLinkItem) => {
  if (!confirm(`Xoá link Affiliate cho ExternalItemId=${x.externalItemId}?`)) return
  removingId.value = x.externalItemId
  try {
    await api.delete(`admin/affiliate-links/${encodeURIComponent(x.externalItemId)}`)
    await load()
  } catch (e: any) {
    alert(e?.message ?? 'Xoá thất bại')
  } finally {
    removingId.value = null
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

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
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
  align-items: center;
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

.input {
  height: 36px;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 0 12px;
  outline: none;
  width: 220px;
}

.table-wrap {
  border: 1px solid #eee;
  border-radius: 14px;
  overflow: auto;
  background: #fff;
}

.table {
  width: 100%;
  border-collapse: collapse;
  min-width: 1200px;
}

th,
 td {
  padding: 12px 12px;
  border-bottom: 1px solid #f1f1f1;
  font-size: 13px;
  vertical-align: top;
}

th {
  text-align: left;
  color: #667085;
  font-weight: 700;
  background: #fafafa;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
}

.actions {
  white-space: nowrap;
}

.link {
  border: none;
  background: transparent;
  color: #111827;
  cursor: pointer;
  font-weight: 700;
  padding: 0 6px;
}

.link.danger {
  color: #b42318;
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
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 14px;
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

.textarea {
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 10px 12px;
  outline: none;
  width: 100%;
  resize: vertical;
}

.grid2 {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.truncate {
  max-width: 320px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.modal-footer {
  padding: 12px 14px;
  border-top: 1px solid #eee;
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}

@media (max-width: 640px) {
  .page-header {
    flex-direction: column;
    align-items: stretch;
  }
  .toolbar {
    justify-content: flex-end;
    flex-wrap: wrap;
  }
  .input {
    width: 100%;
  }
  .grid2 {
    grid-template-columns: 1fr;
  }
  .table {
    min-width: 980px;
  }
}
</style>
