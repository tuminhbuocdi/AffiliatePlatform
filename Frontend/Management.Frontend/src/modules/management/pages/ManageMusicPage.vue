<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Quản lý music</h1>
        <div class="muted">Danh sách music, filter theo topic/style, thêm / sửa / xoá.</div>
      </div>

      <div class="toolbar">
        <input v-model="search" class="input" placeholder="Tìm theo tên/author..." @keyup.enter="load" />

        <select v-model.number="topicId" class="input" @change="load">
          <option :value="0">Topic: All</option>
          <option v-for="t in topics" :key="t.id" :value="t.id">{{ t.name }}</option>
        </select>

        <select v-model.number="styleId" class="input" @change="load">
          <option :value="0">Style: All</option>
          <option v-for="s in styles" :key="s.id" :value="s.id">{{ s.name }}</option>
        </select>

        <button class="btn" type="button" @click="openCreate">Thêm</button>
        <button class="btn secondary" type="button" @click="openImport">Import JSON</button>
        <button class="btn secondary" type="button" @click="load" :disabled="loading">{{ loading ? 'Đang tải...' : 'Tải' }}</button>
      </div>
    </div>

    <div v-if="error" class="error">{{ error }}</div>

    <div v-if="loading" class="loading">Đang tải...</div>

    <div v-else class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th>Id</th>
            <th>Name</th>
            <th>Author</th>
            <th>Duration</th>
            <th>Topics</th>
            <th>Styles</th>
            <th>Active</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="x in items" :key="x.id">
            <td class="mono">{{ x.id }}</td>
            <td>{{ x.name }}</td>
            <td>{{ x.author || '-' }}</td>
            <td class="mono">{{ formatDuration(x.durationSeconds) }}</td>
            <td class="truncate">{{ renderNames(x.topicIds, topicsById) }}</td>
            <td class="truncate">{{ renderNames(x.styleIds, stylesById) }}</td>
            <td><span :class="['pill', x.isActive ? 'ok' : 'bad']">{{ x.isActive ? 'Yes' : 'No' }}</span></td>
            <td class="actions">
              <button class="link" type="button" @click="openEdit(x)">Sửa</button>
              <button class="link danger" type="button" @click="remove(x)" :disabled="removingId === x.id">Xoá</button>
            </td>
          </tr>
        </tbody>
      </table>

      <div class="pager">
        <button class="btn tiny secondary" type="button" @click="prevPage" :disabled="page <= 1 || loading">Trước</button>
        <div class="muted">Trang {{ page }} / {{ totalPages }}</div>
        <button class="btn tiny secondary" type="button" @click="nextPage" :disabled="page >= totalPages || loading">Sau</button>
      </div>
    </div>

    <div v-if="modalOpen" class="modal-overlay" @click.self="closeModal">
      <div class="modal modal-wide">
        <div class="modal-header">
          <div class="modal-title">{{ isCreate ? 'Thêm music' : 'Sửa music' }}</div>
          <button class="icon" type="button" @click="closeModal">×</button>
        </div>

        <div class="modal-body">
          <div class="form">
            <label>
              <div class="lbl">Id</div>
              <input v-model="model.id" class="input" :disabled="!isCreate" placeholder="6696367276884495000" />
            </label>

            <label>
              <div class="lbl">IdStr</div>
              <input v-model="model.idStr" class="input" placeholder="6696367276884495105" />
            </label>

            <label>
              <div class="lbl">Name</div>
              <input v-model="model.name" class="input" placeholder="AH" />
            </label>

            <div class="grid2">
              <label>
                <div class="lbl">Author</div>
                <input v-model="model.author" class="input" />
              </label>
              <label>
                <div class="lbl">Album</div>
                <input v-model="model.album" class="input" />
              </label>
            </div>

            <div class="grid2">
              <label>
                <div class="lbl">Language</div>
                <input v-model="model.language" class="input" placeholder="non_vocal" />
              </label>
              <label>
                <div class="lbl">Category</div>
                <input v-model="model.category" class="input" placeholder="non_vocal" />
              </label>
            </div>

            <div class="grid2">
              <label>
                <div class="lbl">Duration (seconds)</div>
                <input v-model.number="model.durationSeconds" class="input" type="number" min="0" step="0.001" />
              </label>
              <label class="row">
                <input v-model="model.isActive" type="checkbox" />
                <span>Active</span>
              </label>
            </div>

            <label>
              <div class="lbl">AudioUrl</div>
              <input v-model="model.audioUrl" class="input" placeholder="https://..." />
            </label>

            <div class="grid2">
              <label>
                <div class="lbl">Topics</div>
                <select v-model="model.topicIds" class="input" multiple size="8">
                  <option v-for="t in topics" :key="t.id" :value="t.id">{{ t.name }}</option>
                </select>
              </label>
              <label>
                <div class="lbl">Styles</div>
                <select v-model="model.styleIds" class="input" multiple size="8">
                  <option v-for="s in styles" :key="s.id" :value="s.id">{{ s.name }}</option>
                </select>
              </label>
            </div>
          </div>

          <div v-if="modalError" class="error">{{ modalError }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn" type="button" @click="save" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Lưu' }}</button>
          <button class="btn secondary" type="button" @click="closeModal" :disabled="saving">Đóng</button>
        </div>
      </div>
    </div>

    <div v-if="importOpen" class="modal-overlay" @click.self="closeImport">
      <div class="modal modal-wide">
        <div class="modal-header">
          <div class="modal-title">Import JSON</div>
          <button class="icon" type="button" @click="closeImport">×</button>
        </div>

        <div class="modal-body">
          <div class="muted" style="margin-bottom: 8px">
            Dán JSON theo format: {"topic": [...], "style": [...], "music": [...]}
          </div>

          <textarea v-model="importJson" class="input textarea" placeholder="{\n  &quot;topic&quot;: [...],\n  &quot;style&quot;: [...],\n  &quot;music&quot;: [...]\n}"></textarea>

          <div v-if="importResult" class="result">
            <div><b>TopicsUpserted:</b> {{ importResult.topicsUpserted }}</div>
            <div><b>StylesUpserted:</b> {{ importResult.stylesUpserted }}</div>
            <div><b>MusicsUpserted:</b> {{ importResult.musicsUpserted }}</div>
            <div><b>TopicMapRows:</b> {{ importResult.topicMapRows }}</div>
            <div><b>StyleMapRows:</b> {{ importResult.styleMapRows }}</div>
          </div>

          <div v-if="importError" class="error">{{ importError }}</div>
        </div>

        <div class="modal-footer">
          <button class="btn" type="button" @click="doImport" :disabled="importing">{{ importing ? 'Đang import...' : 'Import' }}</button>
          <button class="btn secondary" type="button" @click="closeImport" :disabled="importing">Đóng</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import api from '@/infrastructure/http/apiClient'

type TopicItem = { id: number; name: string }
type StyleItem = { id: number; name: string }

type MusicItem = {
  id: string
  idStr?: string | null
  name: string
  author?: string | null
  album?: string | null
  language?: string | null
  category?: string | null
  durationSeconds?: number | null
  audioUrl: string
  isActive: boolean
  topicIds: number[]
  styleIds: number[]
}

type PageResponse = { total: number; items: MusicItem[] }

type ImportResponse = {
  topicsUpserted: number
  stylesUpserted: number
  musicsUpserted: number
  topicMapRows: number
  styleMapRows: number
}

const items = ref<MusicItem[]>([])
const error = ref<string | null>(null)
const loading = ref(false)

const topics = ref<TopicItem[]>([])
const styles = ref<StyleItem[]>([])

const search = ref('')
const topicId = ref(0)
const styleId = ref(0)

const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

const totalPages = computed(() => {
  const n = Math.ceil((Number(total.value) || 0) / (Number(pageSize.value) || 20))
  return Math.max(1, n || 1)
})

const topicsById = computed(() => {
  const m: Record<number, string> = {}
  for (const t of topics.value) m[t.id] = t.name
  return m
})

const stylesById = computed(() => {
  const m: Record<number, string> = {}
  for (const s of styles.value) m[s.id] = s.name
  return m
})

const formatDuration = (v: any) => {
  const n = Number(v)
  if (!Number.isFinite(n) || n <= 0) return '-'
  const totalSec = Math.round(n)
  const mm = Math.floor(totalSec / 60)
  const ss = totalSec % 60
  return `${mm}:${ss.toString().padStart(2, '0')}`
}

const renderNames = (ids: number[], dict: Record<number, string>) => {
  const list = Array.isArray(ids) ? ids : []
  return list.map((x) => dict[Number(x)] ?? String(x)).join(', ')
}

const loadMeta = async () => {
  try {
    const [t, s] = await Promise.all([
      api.get<TopicItem[]>('admin/music/topics'),
      api.get<StyleItem[]>('admin/music/styles'),
    ])
    topics.value = Array.isArray(t) ? t : []
    styles.value = Array.isArray(s) ? s : []
  } catch {
    topics.value = []
    styles.value = []
  }
}

const load = async () => {
  if (loading.value) return
  error.value = null
  loading.value = true

  try {
    const data = await api.get<PageResponse>('admin/music', {
      params: {
        q: (search.value ?? '').trim() || undefined,
        topicId: topicId.value > 0 ? topicId.value : undefined,
        styleId: styleId.value > 0 ? styleId.value : undefined,
        page: page.value,
        pageSize: pageSize.value,
      },
    })

    items.value = Array.isArray((data as any)?.items) ? (data as any).items : []
    total.value = Number((data as any)?.total) || 0
  } catch (e: any) {
    error.value = e?.message ?? 'Không tải được danh sách music.'
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

const prevPage = async () => {
  if (page.value <= 1) return
  page.value = page.value - 1
  await load()
}

const nextPage = async () => {
  if (page.value >= totalPages.value) return
  page.value = page.value + 1
  await load()
}

const modalOpen = ref(false)
const modalError = ref<string | null>(null)
const saving = ref(false)
const removingId = ref<string | null>(null)
const isCreate = ref(true)

const blankModel = (): MusicItem => ({
  id: '',
  idStr: '',
  name: '',
  author: '',
  album: '',
  language: '',
  category: '',
  durationSeconds: null,
  audioUrl: '',
  isActive: true,
  topicIds: [],
  styleIds: [],
})

const model = ref<MusicItem>(blankModel())

const openCreate = () => {
  isCreate.value = true
  modalError.value = null
  model.value = blankModel()
  modalOpen.value = true
}

const openEdit = (x: MusicItem) => {
  isCreate.value = false
  modalError.value = null
  model.value = {
    id: x.id,
    idStr: x.idStr ?? '',
    name: x.name,
    author: x.author ?? '',
    album: x.album ?? '',
    language: x.language ?? '',
    category: x.category ?? '',
    durationSeconds: x.durationSeconds ?? null,
    audioUrl: x.audioUrl,
    isActive: !!x.isActive,
    topicIds: Array.isArray(x.topicIds) ? [...x.topicIds] : [],
    styleIds: Array.isArray(x.styleIds) ? [...x.styleIds] : [],
  }
  modalOpen.value = true
}

const closeModal = () => {
  if (saving.value) return
  modalOpen.value = false
}

const importOpen = ref(false)
const importJson = ref('')
const importError = ref<string | null>(null)
const importing = ref(false)
const importResult = ref<ImportResponse | null>(null)

const openImport = () => {
  importOpen.value = true
  importError.value = null
  importResult.value = null
}

const closeImport = () => {
  if (importing.value) return
  importOpen.value = false
}

const doImport = async () => {
  if (importing.value) return
  importError.value = null
  importResult.value = null

  const raw = (importJson.value ?? '').trim()
  if (!raw) {
    importError.value = 'JSON rỗng.'
    return
  }

  let parsed: any
  try {
    parsed = JSON.parse(raw)
  } catch {
    importError.value = 'JSON không hợp lệ.'
    return
  }

  if (!parsed || typeof parsed !== 'object') {
    importError.value = 'JSON không hợp lệ.'
    return
  }

  importing.value = true
  try {
    const res = await api.post<ImportResponse>('admin/music/import', {
      topic: Array.isArray(parsed.topic) ? parsed.topic : [],
      style: Array.isArray(parsed.style) ? parsed.style : [],
      music: Array.isArray(parsed.music) ? parsed.music : [],
    })
    importResult.value = res
    await loadMeta()
    await load()
  } catch (e: any) {
    importError.value = e?.message ?? 'Import thất bại.'
  } finally {
    importing.value = false
  }
}

const save = async () => {
  if (saving.value) return
  modalError.value = null
  saving.value = true

  try {
    const payload = {
      id: (model.value.id ?? '').trim(),
      idStr: (model.value.idStr ?? '').toString().trim() || null,
      name: (model.value.name ?? '').trim(),
      author: (model.value.author ?? '').trim() || null,
      album: (model.value.album ?? '').trim() || null,
      language: (model.value.language ?? '').trim() || null,
      category: (model.value.category ?? '').trim() || null,
      durationSeconds: model.value.durationSeconds,
      audioUrl: (model.value.audioUrl ?? '').trim(),
      isActive: !!model.value.isActive,
      topicIds: Array.isArray(model.value.topicIds) ? model.value.topicIds.map((x) => Number(x)).filter((x) => Number.isFinite(x)) : [],
      styleIds: Array.isArray(model.value.styleIds) ? model.value.styleIds.map((x) => Number(x)).filter((x) => Number.isFinite(x)) : [],
    }

    await api.post<MusicItem>('admin/music', payload)
    modalOpen.value = false
    await load()
  } catch (e: any) {
    modalError.value = e?.message ?? 'Lưu thất bại.'
  } finally {
    saving.value = false
  }
}

const remove = async (x: MusicItem) => {
  const id = (x?.id ?? '').trim()
  if (!id) return
  if (removingId.value) return

  const ok = window.confirm(`Xoá music ${id}?`)
  if (!ok) return

  removingId.value = id
  try {
    await api.delete<any>(`admin/music/${encodeURIComponent(id)}`)
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Xoá thất bại.'
  } finally {
    removingId.value = null
  }
}

onMounted(async () => {
  await loadMeta()
  await load()
})
</script>

<style scoped>
.page {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
}

.toolbar {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  align-items: center;
}

.table-wrap {
  overflow: auto;
  background: #fff;
  border: 1px solid #eee;
  border-radius: 8px;
}

.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.table th,
.table td {
  padding: 10px 10px;
  border-bottom: 1px solid #f1f1f1;
  text-align: left;
  vertical-align: top;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
}

.truncate {
  max-width: 320px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.actions {
  white-space: nowrap;
}

.link {
  background: transparent;
  border: none;
  color: #2563eb;
  cursor: pointer;
  padding: 0 6px;
}

.link.danger {
  color: #d1242f;
}

.muted {
  color: #777;
  font-size: 12px;
}

.error {
  color: #d1242f;
  background: #fff1f2;
  border: 1px solid #fecdd3;
  padding: 8px 10px;
  border-radius: 8px;
}

.loading {
  color: #333;
}

.input {
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 13px;
  background: #fff;
}

.textarea {
  width: 100%;
  min-height: 260px;
  resize: vertical;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
}

.result {
  margin-top: 10px;
  border: 1px solid #eee;
  background: #fafafa;
  border-radius: 8px;
  padding: 10px;
  font-size: 13px;
}

.btn {
  border: 1px solid #eee;
  background: #f26f3b;
  color: #fff;
  border-radius: 6px;
  padding: 8px 12px;
  font-size: 13px;
  cursor: pointer;
}

.btn.secondary {
  background: #fff;
  color: #333;
}

.btn.tiny {
  padding: 6px 10px;
  font-size: 12px;
}

.pill {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 12px;
  border: 1px solid #eee;
}

.pill.ok {
  background: #ecfdf5;
  border-color: #a7f3d0;
  color: #047857;
}

.pill.bad {
  background: #fff1f2;
  border-color: #fecdd3;
  color: #d1242f;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px;
  z-index: 100;
}

.modal {
  width: 560px;
  max-width: 100%;
  background: #fff;
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid #eee;
}

.modal.modal-wide {
  width: 760px;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 14px;
  border-bottom: 1px solid #f0f0f0;
}

.modal-title {
  font-weight: 700;
}

.icon {
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 20px;
}

.modal-body {
  padding: 14px;
}

.modal-footer {
  padding: 12px 14px;
  border-top: 1px solid #f0f0f0;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.lbl {
  font-size: 12px;
  color: #777;
  margin-bottom: 4px;
}

.grid2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 12px;
}

@media (max-width: 768px) {
  .page-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .grid2 {
    grid-template-columns: 1fr;
  }
}
</style>
