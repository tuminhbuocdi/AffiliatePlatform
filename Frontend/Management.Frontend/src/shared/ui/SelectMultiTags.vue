<template>
  <div ref="root" class="smt" :class="{ disabled }">
    <button type="button" class="box" :disabled="disabled" @click="toggle">
      <template v-if="selectedItems.length > 0">
        <span v-for="item in selectedItems" :key="item.value" class="tag">
          <span class="tag-text">{{ item.label }}</span>
          <span
            class="tag-x"
            role="button"
            tabindex="0"
            @click.stop="remove(item.value)"
            @keydown.enter.prevent.stop="remove(item.value)"
          >×</span>
        </span>
      </template>
      <span v-else class="placeholder">{{ placeholderText }}</span>
    </button>
  </div>

  <teleport to="body">
    <div v-if="open" class="dropdown" :style="dropdownStyle" @mousedown.stop>
      <div v-if="searchable" class="search">
        <input v-model="query" class="input" type="text" :placeholder="searchPlaceholderText" :disabled="disabled" />
      </div>

      <div class="options">
        <button
          v-for="opt in filteredOptions"
          :key="opt.value"
          type="button"
          class="opt"
          :class="{ selected: isSelected(opt.value) }"
          :disabled="disabled"
          @click="toggleValue(opt.value)"
        >
          <span class="tick">✓</span>
          <span class="label">{{ opt.label }}</span>
        </button>

        <div v-if="filteredOptions.length === 0" class="empty">{{ emptyTextValue }}</div>
      </div>
    </div>
  </teleport>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

export type SelectMultiTagsOption = {
  value: string | number
  label: string
}

const props = defineProps<{
  modelValue: string[]
  options: SelectMultiTagsOption[]
  placeholder?: string
  disabled?: boolean
  searchable?: boolean
  searchPlaceholder?: string
  emptyText?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', v: string[]): void
}>()

const root = ref<HTMLElement | null>(null)
const open = ref(false)
const query = ref('')
const selectedValues = ref<string[]>([])

const dropdownStyle = ref<Record<string, string>>({})

const disabled = computed(() => Boolean(props.disabled))
const searchable = computed(() => props.searchable !== false)
const placeholderText = computed(() => props.placeholder ?? 'Chọn...')
const searchPlaceholderText = computed(() => props.searchPlaceholder ?? 'Tìm...')
const emptyTextValue = computed(() => props.emptyText ?? 'Không có dữ liệu.')

const norm = (value: string | number) => String(value)

const selectedItems = computed(() => {
  const map = new Map((props.options ?? []).map((opt) => [norm(opt.value), opt.label]))
  return selectedValues.value.map((value) => ({ value, label: map.get(value) ?? value }))
})

const filteredOptions = computed(() => {
  const q = query.value.trim().toLowerCase()
  const opts = props.options ?? []
  if (!q) return opts
  return opts.filter((opt) => {
    const label = (opt.label ?? '').toLowerCase()
    const value = norm(opt.value).toLowerCase()
    return label.includes(q) || value.includes(q)
  })
})

const isSelected = (value: string | number) => selectedValues.value.includes(norm(value))

const syncFromModel = (value: string[] | undefined) => {
  selectedValues.value = Array.isArray(value) ? Array.from(new Set(value.map((x) => String(x)).filter(Boolean))) : []
}

const emitChange = (next: string[]) => {
  selectedValues.value = next
  emit('update:modelValue', next)
}

const updateDropdownPosition = () => {
  const el = root.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  dropdownStyle.value = {
    position: 'fixed',
    top: `${Math.round(rect.bottom + 6)}px`,
    left: `${Math.round(rect.left)}px`,
    width: `${Math.round(rect.width)}px`,
    zIndex: '2147483647',
  }
}

const toggle = () => {
  if (disabled.value) return
  open.value = !open.value
  if (open.value) {
    void nextTick(() => {
      updateDropdownPosition()
    })
  }
}

const close = () => {
  open.value = false
}

const remove = (value: string | number) => {
  if (disabled.value) return
  const v = norm(value)
  emitChange(selectedValues.value.filter((x) => x !== v))
}

const toggleValue = (value: string | number) => {
  if (disabled.value) return
  const v = norm(value)
  if (selectedValues.value.includes(v)) {
    emitChange(selectedValues.value.filter((x) => x !== v))
    return
  }
  emitChange([...selectedValues.value, v])
}

const onDocMouseDown = (e: MouseEvent) => {
  if (!open.value) return
  const el = root.value
  if (!el) return
  if (e.target instanceof Node && el.contains(e.target)) return
  close()
}

const onViewportChange = () => {
  if (!open.value) return
  updateDropdownPosition()
}

watch(
  () => props.modelValue,
  (value) => {
    syncFromModel(value)
  },
  { deep: true, immediate: true },
)

onMounted(() => {
  document.addEventListener('mousedown', onDocMouseDown)
  window.addEventListener('scroll', onViewportChange, true)
  window.addEventListener('resize', onViewportChange)
})

onBeforeUnmount(() => {
  document.removeEventListener('mousedown', onDocMouseDown)
  window.removeEventListener('scroll', onViewportChange, true)
  window.removeEventListener('resize', onViewportChange)
})
</script>

<style scoped>
.smt {
  position: relative;
  isolation: isolate;
}

.smt.disabled {
  opacity: 0.7;
  pointer-events: none;
}

.box {
  min-height: 42px;
  width: 100%;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 8px 10px;
  background: #fff;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  cursor: pointer;
  text-align: left;
}

.placeholder {
  color: #9ca3af;
  font-size: 13px;
}

.tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: #f3f4f6;
  border: 1px solid #e5e7eb;
  color: #111827;
  border-radius: 999px;
  padding: 4px 8px;
  max-width: 100%;
}

.tag-text {
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.tag-x {
  cursor: pointer;
  font-size: 16px;
  line-height: 1;
  color: #6b7280;
}

.dropdown {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 10px 25px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

.search {
  padding: 10px;
  border-bottom: 1px solid #f3f4f6;
}

.input {
  width: 100%;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 8px 10px;
  outline: none;
}

.options {
  max-height: 240px;
  overflow: auto;
}

.opt {
  width: 100%;
  text-align: left;
  padding: 10px 12px;
  background: #fff;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 10px;
  border: none;
}

.opt:hover {
  background: #f9fafb;
}

.opt.selected .label {
  color: #2563eb;
  font-weight: 600;
}

.opt.selected .tick {
  opacity: 1;
  color: #2563eb;
}

.tick {
  width: 16px;
  opacity: 0;
}

.label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty {
  padding: 10px 12px;
  color: #6b7280;
  font-size: 13px;
}
</style>
