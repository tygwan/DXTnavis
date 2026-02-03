// @ts-check
import { defineConfig } from 'astro/config';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  site: 'https://tygwan.github.io',
  base: '/DXTnavis/',
  vite: {
    plugins: [tailwindcss()]
  },
});
