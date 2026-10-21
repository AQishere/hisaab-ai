import { isAxiosError } from 'axios';

export const formatNumber = (value: number | null | undefined, maxDecimals = 2) =>
  value == null ? '-' : value.toLocaleString(undefined, { maximumFractionDigits: maxDecimals });

// Short axis labels: 1.2k, 3.4M.
export const formatCompact = (value: number) =>
  Math.abs(value) >= 1e6 ? `${+(value / 1e6).toFixed(1)}M` : Math.abs(value) >= 1e3 ? `${+(value / 1e3).toFixed(1)}k` : `${+value.toFixed(1)}`;

export const formatMoney = (value: number | null | undefined) =>
  value == null ? '-' : `PKR ${value.toLocaleString(undefined, { maximumFractionDigits: 2 })}`;

// A bare YYYY-MM-DD is a calendar date: read it as local midnight, not UTC, so it never shifts a day.
export const formatDate = (value: string | null | undefined) =>
  value
    ? new Date(/^\d{4}-\d{2}-\d{2}$/.test(value) ? `${value}T00:00:00` : value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
    : '-';

// "1 bag", "5 bags", "5 kg" (kg never takes an s).
export const unitLabel = (quantity: number, unit: string) => (unit === 'kg' || Math.abs(quantity) === 1 ? unit : `${unit}s`);

export const formatDateTime = (value: string | null | undefined) =>
  value
    ? new Date(value).toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
    : '-';

export const todayISO = () => {
  const d = new Date();
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
};

export function getErrorMessage(err: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (!isAxiosError(err)) return fallback;
  if (!err.response) return 'Cannot reach the server. Check that the backend is running.';
  if (err.response.status === 403) return 'Your account does not have permission to do this.';
  if (err.response.status === 429) return 'Too many sign-in attempts. Wait a minute and try again.';
  if (err.response.status >= 500) return fallback;

  const data = err.response.data as unknown;
  if (typeof data === 'string' && data.trim()) return data;
  if (data && typeof data === 'object') {
    const obj = data as { message?: string; title?: string; errors?: Record<string, string[]> };
    if (obj.errors) return Object.values(obj.errors).flat().join(' ');
    if (obj.message) return obj.message;
    if (obj.title) return obj.title;
  }
  return fallback;
}
