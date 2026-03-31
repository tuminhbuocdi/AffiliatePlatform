import { createApp } from "vue"
import { createPinia } from "pinia"
import App from "./App.vue"

import "@/styles/global.css"

import { i18n } from "@/app/i18n"

import "@/infrastructure/http/interceptor"

import router from "@/app/router"   // <-- SỬA DÒNG NÀY

const app = createApp(App)

app.use(createPinia())
app.use(i18n)
app.use(router)

app.mount("#app")
