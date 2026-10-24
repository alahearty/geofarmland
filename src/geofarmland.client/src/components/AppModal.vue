<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import AppIcon from './AppIcon.vue'

withDefaults(defineProps<{ title: string; subtitle?: string; size?: 'default' | 'wide' }>(), {
  subtitle: '',
  size: 'default'
})

const emit = defineEmits<{ close: [] }>()

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') emit('close')
}

onMounted(() => {
  document.addEventListener('keydown', onKeydown)
  document.body.style.overflow = 'hidden'
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown)
  document.body.style.overflow = ''
})
</script>

<template>
  <Teleport to="body">
    <div class="modal-backdrop" @click.self="$emit('close')">
      <section class="modal-card" :class="{ 'modal-wide': size === 'wide' }" role="dialog" aria-modal="true">
        <header class="modal-head">
          <div>
            <p class="modal-eyebrow">GeoFarmland workspace</p>
            <h2>{{ title }}</h2>
            <p v-if="subtitle" class="modal-subtitle">{{ subtitle }}</p>
          </div>
          <button type="button" class="modal-close" aria-label="Close dialog" @click="$emit('close')">
            <AppIcon name="close" :size="18" />
          </button>
        </header>
        <div class="modal-body">
          <slot />
        </div>
        <footer v-if="$slots.footer" class="modal-footer">
          <slot name="footer" />
        </footer>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1200;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 4vh 1rem;
  overflow-y: auto;
  background: rgba(12, 28, 20, 0.55);
  backdrop-filter: blur(3px);
}

.modal-card {
  width: min(620px, 100%);
  background: #fff;
  border: 1px solid rgba(21, 61, 42, 0.12);
  border-radius: 18px;
  box-shadow: 0 24px 70px rgba(10, 30, 20, 0.24);
  overflow: hidden;
}

.modal-wide {
  width: min(980px, 100%);
}

.modal-head {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 1.25rem 1.4rem 1rem;
  border-bottom: 1px solid #e9efe9;
}

.modal-eyebrow {
  margin: 0 0 0.25rem;
  color: #6b7f70;
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.modal-head h2 {
  margin: 0;
  color: #14311f;
  font-size: 1.3rem;
  letter-spacing: -0.02em;
}

.modal-subtitle {
  margin: 0.35rem 0 0;
  color: #6b7f70;
  font-size: 0.84rem;
  line-height: 1.5;
}

.modal-close {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  border: 1px solid #dde7de;
  border-radius: 10px;
  background: #f7faf7;
  color: #3c5847;
  cursor: pointer;
}

.modal-close:hover {
  background: #edf4ed;
}

.modal-body {
  padding: 1.25rem 1.4rem;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.6rem;
  padding: 1rem 1.4rem;
  border-top: 1px solid #e9efe9;
  background: #fbfcfb;
}
</style>
