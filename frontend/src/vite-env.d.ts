/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
}

interface Window {
  __APP_CONFIG__?: { API_BASE_URL?: string };
}
