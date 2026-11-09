import { useCallback, useEffect, useRef, useState } from 'react';

interface PollingState<T> {
  data: T | undefined;
  error: unknown;
  loading: boolean;
  refresh: () => Promise<void>;
}

/**
 * Calls `load` now and then every `intervalMs`, plus when the tab becomes visible.
 * Keeps the last good data if a request fails (the error is reported separately).
 * Pass `enabled = false` to stop polling (e.g. no student id yet).
 */
export function usePolling<T>(load: () => Promise<T>, intervalMs: number, enabled = true): PollingState<T> {
  const [data, setData] = useState<T>();
  const [error, setError] = useState<unknown>();
  const [loading, setLoading] = useState(enabled);
  const loadRef = useRef(load);
  loadRef.current = load;
  const generation = useRef(0);

  const refresh = useCallback(async () => {
    const current = generation.current;
    try {
      const result = await loadRef.current();
      if (current !== generation.current) return; // inputs changed meanwhile; ignore stale result
      setData(result);
      setError(undefined);
    } catch (err) {
      if (current !== generation.current) return;
      setError(err);
    } finally {
      if (current === generation.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    generation.current += 1;
    if (!enabled) {
      setData(undefined);
      setError(undefined);
      setLoading(false);
      return;
    }

    setLoading(true);
    void refresh();
    const timer = window.setInterval(() => void refresh(), intervalMs);
    const onVisible = () => {
      if (document.visibilityState === 'visible') void refresh();
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', onVisible);
    };
    // `load` identity changes are handled through loadRef; restart only on these inputs.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, intervalMs, refresh, load]);

  return { data, error, loading, refresh };
}
