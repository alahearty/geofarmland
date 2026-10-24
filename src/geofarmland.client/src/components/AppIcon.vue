<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{ name: string; size?: number; strokeWidth?: number }>(), {
  size: 20,
  strokeWidth: 1.8
})

const icons: Record<string, string[]> = {
  dashboard: ['M3 3h7v7H3z', 'M14 3h7v4h-7z', 'M14 11h7v10h-7z', 'M3 14h7v7H3z'],
  map: ['M9 18l-6 3V6l6-3 6 3 6-3v15l-6 3-6-3Z', 'M9 3v15', 'M15 6v15'],
  farm: ['M3 21h18', 'M5 21V10l7-5 7 5v11', 'M9 21v-6h6v6', 'M9 9h.01', 'M15 9h.01'],
  crop: ['M11 20A7 7 0 0 1 9.8 6.1C15.5 5 17 4.5 19 2c1 2 2 4.2 2 8 0 5.5-4.8 10-10 10Z', 'M2 21c0-3 1.8-5.4 5.1-6C9.5 14.5 12 13 13 12'],
  livestock: ['M4 10c0-3 2-5 5-5h6c3 0 5 2 5 5v3c0 4-3 7-7 7h-2c-4 0-7-3-7-7Z', 'M8 8h.01', 'M16 8h.01', 'M9 13c1.5 1 4.5 1 6 0'],
  aquaculture: ['M6.5 12c2.5-4 6.5-5 10-2 2 1.5 3.5 2 5.5 2-2 0-3.5.5-5.5 2-3.5 3-7.5 2-10-2Z', 'M6.5 12 3 8v8l3.5-4Z', 'M16 9h.01'],
  greenhouse: ['M3 20h18', 'M5 20V9l7-5 7 5v11', 'M8 20v-8h8v8', 'M8 16h8'],
  soil: ['m12 2 9 5-9 5-9-5 9-5Z', 'm3 12 9 5 9-5', 'm3 17 9 5 9-5'],
  weather: ['M12 2v2', 'm4.93 4.93 1.41 1.41', 'M20 12h2', 'm19.07 4.93-1.41 1.41', 'M15.95 12.65A4 4 0 0 0 10.02 8.52', 'M13 22H7a5 5 0 1 1 4.9-6H13a3 3 0 0 1 0 6Z'],
  device: ['M9 9h6v6H9z', 'M4 9V6a2 2 0 0 1 2-2h3', 'M15 4h3a2 2 0 0 1 2 2v3', 'M4 15v3a2 2 0 0 0 2 2h3', 'M15 20h3a2 2 0 0 0 2-2v-3', 'M9 2v2', 'M15 2v2', 'M9 20v2', 'M15 20v2', 'M2 9h2', 'M2 15h2', 'M20 9h2', 'M20 15h2'],
  satellite: ['M13 7 9 3 3 9l4 4', 'm8 8 8 8', 'm16 8 3-3', 'm19 11 3 3-6 6-3-3', 'm12 16-4-4'],
  analytics: ['M3 3v18h18', 'M7 16v-5', 'M12 16V7', 'M17 16v-8'],
  tasks: ['M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2', 'M9 3h6v4H9z', 'm9 14 2 2 4-4'],
  inventory: ['M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z', 'm3.3 7 8.7 5 8.7-5', 'M12 22V12'],
  finance: ['M12 2v20', 'M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'],
  admin: ['M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z', 'm9 12 2 2 4-4'],
  search: ['M21 21l-4.35-4.35', 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14Z'],
  menu: ['M4 6h16', 'M4 12h16', 'M4 18h16'],
  close: ['M6 6l12 12', 'M18 6 6 18'],
  chevron: ['m9 18 6-6-6-6'],
  logout: ['M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4', 'm16 17 5-5-5-5', 'M21 12H9'],
  user: ['M20 21a8 8 0 0 0-16 0', 'M12 3a5 5 0 1 0 0 10 5 5 0 0 0 0-10Z'],
  plus: ['M12 5v14', 'M5 12h14'],
  arrow: ['M5 12h14', 'm13 6 6 6-6 6'],
  refresh: ['M21 12a9 9 0 0 0-15-6.7L3 8', 'M3 3v5h5', 'M3 12a9 9 0 0 0 15 6.7l3-2.7', 'M21 21v-5h-5'],
  layers: ['m12 2 9 5-9 5-9-5 9-5Z', 'm3 12 9 5 9-5', 'm3 17 9 5 9-5'],
  leaf: ['M11 20A7 7 0 0 1 9.8 6.1C15.5 5 17 4.5 19 2c1 2 2 4.2 2 8 0 5.5-4.8 10-10 10Z', 'M2 21c0-3 1.8-5.4 5.1-6C9.5 14.5 12 13 13 12'],
  calendar: ['M8 2v4', 'M16 2v4', 'M3 10h18', 'M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z'],
  warning: ['M10.3 2.9 1.8 17a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 2.9a2 2 0 0 0-3.4 0Z', 'M12 9v4', 'M12 17h.01'],
  pin: ['M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0Z', 'M12 10a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z']
}

const paths = computed(() => icons[props.name] ?? icons.dashboard)
</script>

<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    :stroke-width="strokeWidth"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path v-for="(path, index) in paths" :key="index" :d="path" />
  </svg>
</template>
