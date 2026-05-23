import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory('/'),
  routes: [
    {
      path: '/',
      name: 'library',
      component: () => import('@/views/LibraryView.vue'),
      meta: { title: '書架' },
    },
    {
      path: '/books/new',
      name: 'book-new',
      component: () => import('@/views/BookFormView.vue'),
      meta: { title: '新增實體書' },
    },
    {
      path: '/books/:id',
      name: 'book-detail',
      component: () => import('@/views/BookDetailView.vue'),
      meta: { title: '書籍詳情' },
    },
    {
      path: '/books/:id/edit',
      name: 'book-edit',
      component: () => import('@/views/BookFormView.vue'),
      meta: { title: '編輯書籍' },
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('@/views/SettingsView.vue'),
      meta: { title: '設定' },
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
  scrollBehavior() {
    return { top: 0 }
  },
})

router.afterEach((to) => {
  const title = (to.meta.title as string | undefined) ?? ''
  document.title = title ? `${title} · Knovault` : 'Knovault 芝士庫'
})

export default router
