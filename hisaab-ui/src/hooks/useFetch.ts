import { useCallback, useEffect, useMemo, useState } from 'react';
import client from '../api/client';
import { responseCache } from '../api/responseCache';
import { getErrorMessage } from '../utils/format';

// Pages that need the same list at the same time (Inbound Receipts asks for purchase orders and warehouses twice)
// share one request. A reload after a change always sends a new one, so it never gets data from before the change.
const inFlight = new Map<string, Promise<unknown>>();

function get<T>(url: string, fresh: boolean): Promise<T> {
  const pending = inFlight.get(url);
  if (pending && !fresh) return pending as Promise<T>;
  const request = client.get<T>(url).then((response) => {
    responseCache.set(url, response.data);
    return response.data;
  });
  inFlight.set(url, request);
  const done = () => {
    if (inFlight.get(url) === request) inFlight.delete(url);
  };
  request.then(done, done);
  return request;
}

export function useFetch<T>(url: string | null) {
  const [revision, setRevision] = useState(0);
  const request = useMemo(() => ({ url, revision }), [url, revision]);
  const [result, setResult] = useState<{ request: typeof request; data: T | null; error: string } | null>(null);
  const reload = useCallback(() => setRevision((value) => value + 1), []);

  useEffect(() => {
    if (!url) return;
    let live = true;
    get<T>(url, request.revision > 0).then(
      (data) => {
        if (live) setResult({ request, data, error: '' });
      },
      (err: unknown) => {
        if (live) setResult({ request, data: null, error: getErrorMessage(err, 'Could not load data.') });
      },
    );
    return () => {
      live = false;
    };
  }, [url, request]);

  // Keep existing rows during a reload, but never show another URL's response. A page opened again starts with the
  // rows from its last visit (marked loading) instead of an empty table.
  const current = url && result?.request.url === url ? result : null;
  const loading = !!url && current?.request !== request;
  const remembered = url && !current && responseCache.has(url) ? (responseCache.get(url) as T) : null;
  return { data: current?.data ?? remembered, loading, error: loading ? '' : current?.error ?? '', reload };
}
