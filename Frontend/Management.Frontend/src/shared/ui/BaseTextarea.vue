<template>
  <div class="base-textarea" :class="{ 'has-error': !!error }">
    <textarea
      class="textarea"
      :class="{ small: size === 'sm' }"
      :value="modelValue"
      :rows="rows"
      :placeholder="placeholder"
      :disabled="disabled"
      v-bind="$attrs"
      @input="onInput"
      @blur="$emit('blur')"
      @focus="$emit('focus')"
    />
    <div v-if="error" class="base-textarea__error">{{ error }}</div>
  </div>
</template>

<script setup lang="ts">
defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    modelValue?: string
    rows?: number
    placeholder?: string
    disabled?: boolean
    error?: string
    size?: 'md' | 'sm'
  }>(),
  {
    modelValue: '',
    rows: 3,
    placeholder: '',
    disabled: false,
    error: '',
    size: 'md',
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', v: string): void
  (e: 'blur'): void
  (e: 'focus'): void
}>()

const onInput = (e: Event) => {
  const el = e.target as HTMLTextAreaElement
  emit('update:modelValue', el.value)
}
</script>

<style scoped>
.base-textarea__error {
  margin-top: 6px;
  font-size: 12px;
  color: #b42318;
}
</style>
