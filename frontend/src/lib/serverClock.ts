/**
 * Keeps the difference between the server clock and this computer's clock, so
 * countdowns are correct even if the user's clock is wrong.
 */
let offsetMs = 0;

export function syncServerTime(serverTime: string | undefined, receivedAt = Date.now()): void {
  if (!serverTime) return;
  const parsed = Date.parse(serverTime);
  if (!Number.isNaN(parsed)) offsetMs = parsed - receivedAt;
}

/** Current time on the server's clock (ms since epoch). */
export function serverNow(): number {
  return Date.now() + offsetMs;
}

/** Milliseconds left until `expiresAt` (never negative). */
export function msUntil(expiresAt: string, now = serverNow()): number {
  return Math.max(0, Date.parse(expiresAt) - now);
}

export function formatCountdown(ms: number): string {
  const totalSeconds = Math.ceil(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}
