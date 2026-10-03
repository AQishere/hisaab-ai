import { useCallback, useEffect, useState } from 'react';
import client from '../api/client';
import { getErrorMessage } from '../utils/format';

export function useFetch<T>(url: string | null) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(!!url);
  const [error, setError] = useState('');

  const reload = useCallback(async () => {
    if (!url) return;
    setLoading(true);
    setError('');
    try {
      const res = await client.get<T>(url);
      setData(res.data);
    } catch (err) {
      setError(getErrorMessage(err, 'Could not load data.'));
    } finally {
      setLoading(false);
    }
  }, [url]);

  useEffect(() => {
    reload();
  }, [reload]);

  return { data, loading, error, reload, setData };
}
