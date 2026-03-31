import { createRouter, createWebHistory } from "vue-router"
import { clearAuthSession, isAdminToken, isTokenExpired } from "@/shared/auth/jwt"

const routes = [
  {
    path: "/login",
    component: () => import("@/modules/auth/pages/LoginPage.vue"),
  },
  {
    path: "/register",
    component: () => import("@/modules/auth/pages/RegisterPage.vue"),
  },
  {
    path: "/",
    component: () => import("@/app/layouts/MainLayout.vue"),
    meta: { requiresAuth: true },
    children: [
      {
        path: "dashboard",
        component: () =>
          import("@/modules/dashboard/pages/DashboardPage.vue"),
      },
      {
        path: "products",
        component: () =>
          import("@/modules/products/pages/ProductsPage.vue"),
      },
      {
        path: "products/:id",
        component: () =>
          import("@/modules/products/pages/ProductDetailPage.vue"),
      },
      {
        path: "settings/user",
        component: () =>
          import("@/modules/settings/pages/UserSettingsPage.vue"),
      },
      {
        path: "payments/topup",
        component: () =>
          import("@/modules/payments/pages/TopUpPage.vue"),
      },
      {
        path: "payments/history",
        component: () =>
          import("@/modules/payments/pages/TransactionHistoryPage.vue"),
      },
      {
        path: "admin/youtube",
        meta: { requiresAdmin: false },
        component: () =>
          import("@/modules/youtube/pages/AdminYoutubePage.vue"),
      },
      {
        path: "platform",
        meta: { requiresAdmin: false },
        component: () =>
          import("@/modules/platform/pages/PlatformDashboardPage.vue"),
      },
      {
        path: "admin/facebook",
        meta: { requiresAdmin: false },
        component: () =>
          import("@/modules/facebook/pages/FacebookPage.vue"),
      },
      {
        path: "admin/manage/products",
        meta: { requiresAdmin: true },
        component: () =>
          import("@/modules/management/pages/ManageProductsPage.vue"),
      },
      {
        path: "admin/manage/users",
        meta: { requiresAdmin: true },
        component: () =>
          import("@/modules/management/pages/ManageUsersPage.vue"),
      },
      {
        path: "admin/manage/affiliate-links",
        meta: { requiresAdmin: true },
        component: () =>
          import("@/modules/management/pages/ManageAffiliateLinksPage.vue"),
      },
      {
        path: "admin/manage/music",
        meta: { requiresAdmin: true },
        component: () =>
          import("@/modules/management/pages/ManageMusicPage.vue"),
      },
      {
        path: "admin/manage/schedules",
        meta: { requiresAdmin: true },
        component: () =>
          import("@/modules/management/pages/ManageSchedulesPage.vue"),
      },
      {
        path: "admin/manage/my-studio",
        meta: { requiresAdmin: true },
        component: () =>
          import("@/modules/management/pages/ManageMyStudioPage.vue"),
      },
      {
        path: "admin/manage/ass-subtitles",
        meta: { requiresAdmin: true },
        component: () =>
          import("@/modules/management/pages/ManageAssSubtitlesPage.vue"),
      },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  if (to.meta.requiresAuth) {
    const token = localStorage.getItem("token")
    if (!token || isTokenExpired(token)) {
      clearAuthSession()
      return "/login"
    }

    if ((to.meta as any)?.requiresAdmin) {
      if (!isAdminToken(token)) {
        return "/dashboard"
      }
    }
  }
})

export default router
