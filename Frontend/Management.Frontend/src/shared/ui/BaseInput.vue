<template>
  <div class="base-input" :class="{ 'has-error': !!error }">
    <input
      class="input"
      :class="{ small: size === 'sm' }"
      :type="type"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      v-bind="$attrs"
      @input="onInput"
      @blur="$emit('blur')"
      @focus="$emit('focus')"
    />
    <div v-if="error" class="base-input__error">{{ error }}</div>
  </div>
</template>

<script setup lang="ts">
defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    modelValue?: string
    type?: string
    placeholder?: string
    disabled?: boolean
    error?: string
    size?: 'md' | 'sm'
  }>(),
  {
    modelValue: '',
    type: 'text',
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
  const el = e.target as HTMLInputElement
  emit('update:modelValue', el.value)
}
</script>

<style scoped>
.base-input__error {
  margin-top: 6px;
  font-size: 12px;
  color: #b42318;
}
</style>
