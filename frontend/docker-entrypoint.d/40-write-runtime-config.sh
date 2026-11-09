#!/bin/sh
# Writes the backend URL into config.js when the container starts, so it can be
# changed with the API_BASE_URL environment variable without rebuilding the image.
set -eu
escaped=$(printf '%s' "${API_BASE_URL:-http://localhost:8080}" | sed 's/\\/\\\\/g; s/"/\\"/g')
cat > /usr/share/nginx/html/config.js <<CONFIG
window.__APP_CONFIG__ = { API_BASE_URL: "${escaped}" };
CONFIG
echo "Runtime config written: API_BASE_URL=${API_BASE_URL:-http://localhost:8080}"
