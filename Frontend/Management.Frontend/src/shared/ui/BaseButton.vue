<template>
  <button
    :type="type"
    class="btn"
    :class="[variantClass, sizeClass, { block, loading }]"
    :disabled="disabled || loading"
    v-bind="$attrs"
  >
    <span v-if="loading" class="btn__spinner" aria-hidden="true" />
    <span class="btn__content">
      <slot />
    </span>
  </button>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    type?: 'button' | 'submit' | 'reset'
    variant?: 'primary' | 'secondary' | 'danger'
    size?: 'md' | 'tiny'
    block?: boolean
    disabled?: boolean
    loading?: boolean
  }>(),
  {
    type: 'button',
    variant: 'primary',
    size: 'md',
    block: false,
    disabled: false,
    loading: false,
  },
)

defineOptions({ inheritAttrs: false })

const variantClass = computed(() => {
  if (props.variant === 'secondary') return 'secondary'
  if (props.variant === 'danger') return 'danger'
  return ''
})

const sizeClass = computed(() => {
  if (props.size === 'tiny') return 'tiny'
  return ''
})
</script>

<style scoped>
.btn.block {
  width: 100%;
}

.btn__content {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.btn__spinner {
  width: 14px;
  height: 14px;
  border-radius: 999px;
  border: 2px solid rgba(255, 255, 255, 0.5);
  border-top-color: rgba(255, 255, 255, 1);
  animation: spin 0.9s linear infinite;
}

.btn.secondary .btn__spinner,
.btn.danger .btn__spinner {
  border-color: rgba(0, 0, 0, 0.25);
  border-top-color: rgba(0, 0, 0, 0.55);
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
