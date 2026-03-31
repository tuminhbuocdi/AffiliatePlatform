<template>
  <section class="base-grid" :class="{ 'is-full': fullHeight }">
    <header v-if="$slots.header" class="base-grid__header">
      <slot name="header" />
    </header>

    <div class="base-grid__body">
      <div v-if="loading" class="base-grid__loading">
        <slot name="loading">Đang tải...</slot>
      </div>

      <div v-else-if="isEmpty" class="base-grid__empty">
        <div class="empty-icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" width="44" height="44" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path
              d="M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z"
              stroke="currentColor"
              stroke-width="1.5"
            />
            <path d="M8 8h8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
            <path d="M8 12h8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
            <path d="M8 16h6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
          </svg>
        </div>
        <div class="empty-title">{{ emptyTitle }}</div>
        <div v-if="emptyDescription" class="empty-desc">{{ emptyDescription }}</div>
        <div v-if="$slots.empty" class="empty-extra">
          <slot name="empty" />
        </div>
      </div>

      <div v-else class="base-grid__content">
        <slot />
      </div>
    </div>

    <footer v-if="$slots.footer" class="base-grid__footer">
      <slot name="footer" />
    </footer>
  </section>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    fullHeight?: boolean
    loading?: boolean
    isEmpty?: boolean
    emptyTitle?: string
    emptyDescription?: string
  }>(),
  {
    fullHeight: true,
    loading: false,
    isEmpty: false,
    emptyTitle: 'Dữ liệu trống',
    emptyDescription: 'Chưa có dữ liệu để hiển thị.',
  },
)
</script>

<style scoped>
.base-grid {
  background: #fff;
  border: 1px solid #eee;
  border-radius: 4px;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.base-grid.is-full {
  height: 100%;
  min-height: 100%;
}

.base-grid__header {
  padding: 14px 16px;
  border-bottom: 1px solid #f0f0f0;
  min-width: 0;
}

.base-grid__body {
  flex: 1;
  min-height: 0;
  display: flex;
}

.base-grid__loading {
  padding: 16px;
  font-size: 12px;
  color: #999;
}

.base-grid__content {
  flex: 1;
  min-width: 0;
  min-height: 0;
}

.base-grid__empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 28px 16px;
  color: #888;
  text-align: center;
}

.empty-icon {
  color: #c7c7c7;
  margin-bottom: 10px;
}

.empty-title {
  font-size: 14px;
  font-weight: 600;
  color: #666;
  margin-bottom: 4px;
}

.empty-desc {
  font-size: 12px;
  color: #999;
}

.empty-extra {
  margin-top: 12px;
}

.base-grid__footer {
  padding: 12px 16px;
  border-top: 1px solid #f0f0f0;
}
</style>
