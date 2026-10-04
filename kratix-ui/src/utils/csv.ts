// Spreadsheet-friendly CSV download (UTF-8 with BOM so Excel shows names correctly).
const cell = (value: unknown) => {
  let text = value === null || value === undefined ? '' : String(value);
  // Treat names/codes as text in Excel, even if they look like a formula.
  // Numeric values (including negative stock movements) stay numeric.
  if (typeof value === 'string' && /^[\s]*[=+@-]/.test(text)) text = `'${text}`;
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
};

export function downloadCsv(filename: string, headers: string[], rows: unknown[][]) {
  const text = [headers, ...rows].map((r) => r.map(cell).join(',')).join('\r\n');
  const url = URL.createObjectURL(new Blob(['\uFEFF' + text], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
