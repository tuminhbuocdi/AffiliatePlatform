<template>
  <div class="auth-page">
    <div class="auth-card">
      <div class="brand">
        <div class="logo">AP</div>
        <div class="title">{{ t('auth.registerTitle') }}</div>
      </div>

      <div class="form">
        <div class="field">
          <label>{{ t('auth.usernameLabel') }}</label>
          <input v-model="username" :placeholder="t('auth.usernamePlaceholder')" />
        </div>

        <div class="field">
          <label>{{ t('auth.emailLabel') }}</label>
          <input v-model="email" :placeholder="t('auth.emailPlaceholder')" />
        </div>

        <div class="field">
          <label>{{ t('auth.phoneLabel') }}</label>
          <input v-model="phone" :placeholder="t('auth.phonePlaceholder')" />
        </div>

        <div class="field">
          <label>{{ t('auth.fullNameLabel') }}</label>
          <input v-model="fullName" :placeholder="t('auth.fullNamePlaceholder')" />
        </div>

        <div class="field">
          <label>{{ t('auth.passwordLabel') }}</label>
          <input v-model="password" type="password" :placeholder="t('auth.passwordPlaceholder')" />
        </div>

        <button class="btn" @click="onRegister" :disabled="loading">
          {{ loading ? t('auth.creating') : t('auth.createButton') }}
        </button>

        <div v-if="error" class="error">{{ error }}</div>

        <div class="footer">
          <span>{{ t('auth.haveAccount') }}</span>
          <a class="link" href="/login">{{ t('auth.loginLink') }}</a>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue"
import { useI18n } from "vue-i18n"
import { useRouter } from "vue-router"
import api, { type ApiError } from "@/infrastructure/http/apiClient"

const router = useRouter()
const { t } = useI18n()

const username = ref("")
const email = ref("")
const phone = ref("")
const fullName = ref("")
const password = ref("")
const loading = ref(false)
const error = ref<string | null>(null)

const onRegister = async () => {
  error.value = null
  loading.value = true
  try {
    const res = await api.post<{ token: string }>("/auth/register", {
      username: username.value,
      email: email.value || null,
      phone: phone.value || null,
      fullName: fullName.value || null,
      password: password.value,
    })

    localStorage.setItem("token", res.token)
    await router.push("/dashboard")
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Register failed"
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f6f6f6;
  padding: 16px;
}

.auth-card {
  width: 100%;
  max-width: 480px;
  background: #fff;
  border: 1px solid #eee;
  border-radius: 6px;
  padding: 18px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
}

.logo {
  width: 34px;
  height: 34px;
  border-radius: 8px;
  background: #f26f3b;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  font-size: 12px;
}

.title {
  font-size: 16px;
  font-weight: 700;
  color: #222;
}

.form {
  display: grid;
  gap: 12px;
}

.field {
  display: grid;
  gap: 6px;
}

label {
  font-size: 12px;
  color: #777;
}

input {
  border: 1px solid #ddd;
  border-radius: 4px;
  padding: 10px 12px;
  font-size: 13px;
  outline: none;
}

input:focus {
  border-color: #f26f3b;
}

.btn {
  border: none;
  border-radius: 4px;
  padding: 10px 14px;
  background: #f26f3b;
  color: #fff;
  font-size: 13px;
  cursor: pointer;
}

.btn:disabled {
  opacity: 0.75;
  cursor: not-allowed;
}

.error {
  font-size: 12px;
  color: #d1242f;
  background: #fff1f0;
  border: 1px solid #ffccc7;
  padding: 8px 10px;
  border-radius: 4px;
}

.footer {
  font-size: 12px;
  color: #666;
  display: flex;
  gap: 6px;
  justify-content: center;
}

.link {
  color: #f26f3b;
  text-decoration: none;
  font-weight: 600;
}

.link:hover {
  text-decoration: underline;
}
</style>
