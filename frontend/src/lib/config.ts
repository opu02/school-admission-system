/**
 * Backend URL, resolved in this order:
 * 1. window.__APP_CONFIG__.API_BASE_URL - written by the Docker container at start-up,
 *    so the URL can be changed with an environment variable without rebuilding.
 * 2. VITE_API_BASE_URL - for `npm run dev`.
 * 3. http://localhost:8080
 */
export const API_BASE_URL: string = (
  window.__APP_CONFIG__?.API_BASE_URL ||
  import.meta.env.VITE_API_BASE_URL ||
  'http://localhost:8080'
).replace(/\/+$/, '');

/** How often seat counts and applications are refreshed. */
export const POLL_INTERVAL_MS = 3000;
