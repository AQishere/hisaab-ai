import { useEffect, useState } from 'react';
import type { Paged } from '../api/types';
import { useFetch } from './useFetch';

export const PAGE_SIZE = 25;

// One page of a growing list (orders, receipts, invoices...), newest first, with a search box. The server does the
// paging and searching, so a page never downloads more than PAGE_SIZE rows however long the history gets.
export function usePagedList<T>(path: string, filters: Record<string, string | undefined> = {}) {
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  // Wait for a pause in typing instead of asking the server on every key.
  useEffect(() => {
    const timer = setTimeout(() => setQuery(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  // A new search or filter starts again at page 1.
  const key = JSON.stringify([query, filters]);
  const [paging, setPaging] = useState({ key, page: 1 });
  const page = paging.key === key ? paging.page : 1;
  const setPage = (next: number) => setPaging({ key, page: next });

  const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
  if (query) params.set('search', query);
  for (const [name, value] of Object.entries(filters)) if (value) params.set(name, value);
  const fetched = useFetch<Paged<T>>(`${path}?${params}`);

  // While the next page loads, keep showing the current one instead of flashing an empty table.
  const [shown, setShown] = useState<Paged<T> | null>(null);
  if (fetched.data && fetched.data !== shown) setShown(fetched.data);
  const data = fetched.data ?? shown;

  return {
    rows: data?.items ?? null,
    total: data?.total ?? 0,
    totalAmount: data?.totalAmount ?? null,
    loading: fetched.loading,
    error: fetched.error,
    reload: fetched.reload,
    search,
    setSearch,
    searching: query !== '',
    pagination: { page: data?.page ?? page, pageSize: PAGE_SIZE, total: data?.total ?? 0, onPageChange: setPage },
  };
}
