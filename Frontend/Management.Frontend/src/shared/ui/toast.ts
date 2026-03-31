import { ref } from 'vue'

export type ToastType = 'info' | 'success' | 'error' | 'loading'

export type ToastItem = {
  id: string
  type: ToastType
  message: string
  createdAt: number
  timeoutMs?: number
}

const toasts = ref<ToastItem[]>([])

const remove = (id: string) => {
  toasts.value = toasts.value.filter((t) => t.id !== id)
}

const push = (item: ToastItem) => {
  toasts.value = [item, ...toasts.value].slice(0, 5)
  if (item.timeoutMs && item.timeoutMs > 0) {
    window.setTimeout(() => remove(item.id), item.timeoutMs)
  }
}

const createId = () => `${Date.now()}_${Math.random().toString(16).slice(2)}`

export const useToasts = () => ({
  toasts,
  remove,
})

export const toast = {
  info(message: string, timeoutMs = 3500) {
    push({ id: createId(), type: 'info', message, createdAt: Date.now(), timeoutMs })
  },
  success(message: string, timeoutMs = 3500) {
    push({ id: createId(), type: 'success', message, createdAt: Date.now(), timeoutMs })
  },
  error(message: string, timeoutMs = 6000) {
    push({ id: createId(), type: 'error', message, createdAt: Date.now(), timeoutMs })
  },
  loading(message: string) {
    const id = createId()
    push({ id, type: 'loading', message, createdAt: Date.now() })
    return {
      id,
      update(nextMessage: string) {
        const idx = toasts.value.findIndex((t) => t.id === id)
        if (idx >= 0) toasts.value[idx] = { ...toasts.value[idx], message: nextMessage }
      },
      success(nextMessage: string, timeoutMs = 3000) {
        const idx = toasts.value.findIndex((t) => t.id === id)
        if (idx >= 0) toasts.value[idx] = { ...toasts.value[idx], type: 'success', message: nextMessage, timeoutMs }
        window.setTimeout(() => remove(id), timeoutMs)
      },
      error(nextMessage: string, timeoutMs = 6000) {
        const idx = toasts.value.findIndex((t) => t.id === id)
        if (idx >= 0) toasts.value[idx] = { ...toasts.value[idx], type: 'error', message: nextMessage, timeoutMs }
        window.setTimeout(() => remove(id), timeoutMs)
      },
      dismiss() {
        remove(id)
      },
    }
  },
}
