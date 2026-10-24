<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AppIcon from '@/components/AppIcon.vue'
import { findModule } from '@/utils/navigation'

const route = useRoute()
const module = computed(() => findModule(route.params.module))
</script>

<template>
  <div class="module-page">
    <template v-if="module">
      <section class="module-hero">
        <div class="hero-icon"><AppIcon :name="module.icon" :size="30" /></div>
        <div class="hero-copy">
          <p class="eyebrow">Enterprise delivery plan</p>
          <h2>{{ module.title }}</h2>
          <p>{{ module.description }}</p>
          <div class="hero-tags">
            <span class="phase-tag">{{ module.phase }}</span>
            <span class="state-tag"><span class="dot"></span> Foundation planned</span>
          </div>
        </div>
      </section>

      <section class="module-grid">
        <article class="module-card">
          <p class="panel-eyebrow">Planned scope</p>
          <h3>Capabilities in this module</h3>
          <ul>
            <li v-for="capability in module.capabilities" :key="capability">
              <span class="check"><AppIcon name="arrow" :size="13" /></span>
              {{ capability }}
            </li>
          </ul>
        </article>

        <aside class="module-aside">
          <article class="aside-card">
            <p class="panel-eyebrow">Available now</p>
            <h3>Start with the live foundation</h3>
            <p>The enterprise land register, spatial explorer and operational dashboard are already backed by the working API.</p>
            <div class="aside-links">
              <router-link to="/farms"><AppIcon name="farm" :size="16" /> Farms &amp; land parcels</router-link>
              <router-link to="/gis"><AppIcon name="map" :size="16" /> GIS explorer</router-link>
              <router-link to="/"><AppIcon name="dashboard" :size="16" /> Operational dashboard</router-link>
            </div>
          </article>

          <article class="aside-card delivery-card">
            <p class="panel-eyebrow">Delivery approach</p>
            <h3>No placeholder operational data</h3>
            <p>
              This module will be released with its database model, API contracts, role checks and tested
              screens. It is intentionally shown as a delivery-plan page until those pieces are live.
            </p>
          </article>
        </aside>
      </section>
    </template>

    <section v-else class="not-found">
      <span class="hero-icon"><AppIcon name="warning" :size="26" /></span>
      <h2>Module not found</h2>
      <p>The requested module is not part of the current GeoFarmland delivery plan.</p>
      <router-link to="/" class="back-link">Return to dashboard</router-link>
    </section>
  </div>
</template>

<style scoped>
.module-page {
  max-width: 1080px;
  color: #1d3525;
}

.module-hero {
  display: flex;
  gap: 1rem;
  align-items: flex-start;
  padding: 1.35rem;
  border: 1px solid #e1e9e1;
  border-radius: 15px;
  background:
    radial-gradient(circle at 95% 10%, rgba(87, 151, 103, 0.13), transparent 25%),
    linear-gradient(120deg, #ffffff, #f7fbf7);
  box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04);
  margin-bottom: 0.8rem;
}

.hero-icon {
  display: grid;
  place-items: center;
  width: 60px;
  height: 60px;
  flex: 0 0 auto;
  border: 1px solid #d4e5d8;
  border-radius: 17px;
  background: #e9f4ec;
  color: #2c7450;
}

.hero-copy {
  min-width: 0;
}

.eyebrow {
  margin: 0 0 0.25rem;
  color: #75867a;
  font-size: 0.62rem;
  font-weight: 800;
  letter-spacing: 0.11em;
  text-transform: uppercase;
}

.hero-copy h2 {
  margin: 0;
  color: #16351f;
  font-size: clamp(1.45rem, 2vw, 1.9rem);
  letter-spacing: -0.035em;
}

.hero-copy > p {
  max-width: 760px;
  margin: 0.38rem 0 0;
  color: #697c70;
  font-size: 0.82rem;
  line-height: 1.55;
}

.hero-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin-top: 0.7rem;
}

.phase-tag,
.state-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.22rem 0.5rem;
  border-radius: 999px;
  font-size: 0.6rem;
  font-weight: 800;
}

.phase-tag {
  background: #edf5ef;
  color: #33734e;
}

.state-tag {
  border: 1px solid #e3e9e2;
  background: #fff;
  color: #748279;
}

.state-tag .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #d39b39;
}

.module-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(280px, 0.8fr);
  gap: 0.8rem;
}

.module-card,
.aside-card {
  border: 1px solid #e1e9e1;
  border-radius: 13px;
  background: #fff;
  box-shadow: 0 4px 14px rgba(23, 57, 34, 0.04);
}

.module-card {
  padding: 1.15rem 1.2rem;
}

.panel-eyebrow {
  margin: 0 0 0.2rem;
  color: #829286;
  font-size: 0.58rem;
  font-weight: 800;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.module-card h3,
.aside-card h3 {
  margin: 0;
  color: #1d3d27;
  font-size: 0.9rem;
}

.module-card ul {
  list-style: none;
  margin: 0.8rem 0 0;
  padding: 0;
}

.module-card li {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.55rem 0;
  border-top: 1px solid #eef2ee;
  color: #566d5c;
  font-size: 0.74rem;
  line-height: 1.45;
}

.check {
  display: grid;
  place-items: center;
  width: 20px;
  height: 20px;
  flex: 0 0 auto;
  margin-top: 0.02rem;
  border-radius: 6px;
  background: #eef7f0;
  color: #2f754d;
}

.module-aside {
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
}

.aside-card {
  padding: 1rem;
}

.aside-card > p {
  margin: 0.38rem 0 0;
  color: #738377;
  font-size: 0.7rem;
  line-height: 1.55;
}

.aside-links {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  margin-top: 0.7rem;
}

.aside-links a {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.45rem 0.5rem;
  border-radius: 7px;
  color: #2f6e4b;
  font-size: 0.7rem;
  font-weight: 700;
  text-decoration: none;
}

.aside-links a:hover {
  background: #f2f8f3;
}

.delivery-card {
  background: #fbfcfb;
}

.not-found {
  display: grid;
  place-items: center;
  gap: 0.4rem;
  padding: 4rem 1rem;
  text-align: center;
}

.not-found h2 {
  margin: 0;
  color: #1e3c27;
}

.not-found p {
  max-width: 380px;
  margin: 0;
  color: #7b8b7e;
  font-size: 0.8rem;
}

.back-link {
  margin-top: 0.5rem;
  color: #2d734b;
  font-size: 0.75rem;
  font-weight: 700;
  text-decoration: none;
}

@media (max-width: 760px) {
  .module-grid {
    grid-template-columns: 1fr;
  }

  .module-hero {
    flex-direction: column;
  }
}
</style>
