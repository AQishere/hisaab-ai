import { isAxiosError } from 'axios';

export const formatNumber = (value: number | null | undefined, maxDecimals = 2) =>
  value == null ? '-' : value.toLocaleString(undefined, { maximumFractionDigits: maxDecimals });

export const formatMoney = (value: number | null | undefined) =>
  value == null ? '-' : `PKR ${value.toLocaleString(undefined, { maximumFractionDigits: 2 })}`;

export const formatDate = (value: string | null | undefined) =>
  value ? new Date(value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }) : '-';

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
