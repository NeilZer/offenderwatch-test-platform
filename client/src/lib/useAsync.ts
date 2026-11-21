import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Loads data with explicit loading / error states and a reload() function.
 * Stale responses (from an older call) are ignored, so fast navigation never shows wrong data.
 */
export function useAsync<T>(loader: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const callId = useRef(0);

  const reload = useCallback(async () => {
    const id = ++callId.current;
    setLoading(true);
    try {
      const result = await loader();
      if (id === callId.current) {
        setData(result);
        setError(null);
      }
    } catch (e) {
      if (id === callId.current) setError((e as Error).message);
    } finally {
      if (id === callId.current) setLoading(false);
    }
  }, deps); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    void reload();
  }, [reload]);

  return { data, setData, error, loading, reload };
}
