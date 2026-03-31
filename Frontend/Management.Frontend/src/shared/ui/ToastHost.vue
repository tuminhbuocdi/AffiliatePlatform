<template>
  <div class="toast-host" aria-live="polite" aria-relevant="additions removals">
    <div v-for="t in toasts" :key="t.id" class="toast" :class="t.type">
      <div class="msg">{{ t.message }}</div>
      <button class="x" type="button" @click="remove(t.id)">×</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useToasts } from './toast'

const { toasts, remove } = useToasts()
</script>

<style scoped>
.toast-host {
  position: fixed;
  top: 14px;
  right: 14px;
  z-index: 9999;
  display: grid;
  gap: 10px;
  width: min(420px, calc(100vw - 28px));
}

.toast {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
  background: #fff;
  box-shadow: 0 10px 30px rgba(16, 24, 40, 0.12);
}

.toast .msg {
  font-size: 13px;
  line-height: 1.35;
  color: #111827;
  word-break: break-word;
}

.toast .x {
  width: 28px;
  height: 28px;
  border-radius: 10px;
  border: 1px solid #e5e7eb;
  background: #fff;
  cursor: pointer;
  font-size: 16px;
}

.toast.info {
  border-color: #d0d5dd;
}

.toast.success {
  border-color: #abefc6;
  background: #ecfdf3;
}

.toast.error {
  border-color: #fecdca;
  background: #fef3f2;
}

.toast.loading {
  border-color: #b2ddff;
  background: #eff8ff;
}
</style>
