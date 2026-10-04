import { useCallback, useEffect, useMemo, useState } from 'react';
import client from '../api/client';
import { getErrorMessage } from '../utils/format';

export function useFetch<T>(url: string | null) {
  const [revision, setRevision] = useState(0);
  const request = useMemo(() => ({ url, revision }), [url, revision]);
  const [result, setResult] = useState<{ request: typeof request; data: T | null; error: string } | null>(null);
  const reload = useCallback(() => setRevision((value) => value + 1), []);

  useEffect(() => {
    if (!url) return;
    const controller = new AbortController();
    client.get<T>(url, { signal: controller.signal }).then(
      (response) => {
        if (!controller.signal.aborted) setResult({ request, data: response.data, error: '' });
      },
      (err: unknown) => {
        if (!controller.signal.aborted) setResult({ request, data: null, error: getErrorMessage(err, 'Could not load data.') });
      },
    );
    return () => controller.abort();
  }, [url, request]);

  // Keep existing rows during a reload, but never show another URL's response.
  const current = url && result?.request.url === url ? result : null;
  const loading = !!url && current?.request !== request;
  return { data: current?.data ?? null, loading, error: loading ? '' : current?.error ?? '', reload };
}
