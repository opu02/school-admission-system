import { useEffect, useState } from 'react';
import { serverNow } from '../lib/serverClock';

/** Server-clock "now", re-rendering every `intervalMs` (for countdowns). */
export function useNow(intervalMs = 500): number {
  const [now, setNow] = useState(serverNow);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(serverNow()), intervalMs);
    return () => window.clearInterval(timer);
  }, [intervalMs]);
  return now;
}
