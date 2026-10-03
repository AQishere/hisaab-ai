import React, { useState } from 'react';
import { Box, Button, GlobalStyles, Paper, Tab, Tabs, TextField, Typography } from '@mui/material';
import { Download as DownloadIcon, Print as PrintIcon } from '@mui/icons-material';
import type { ProductionReport, ReceivablesReport, SalesReport, StockReportRow } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { useAuth } from '../context/AuthContext';
import { canSeeOperationsReports, canSeeSalesReports } from '../auth/permissions';
import { formatDate, formatMoney, formatNumber, todayISO, unitLabel } from '../utils/format';
import { downloadCsv } from '../utils/csv';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';

type ReportKind = 'stock' | 'production' | 'sales' | 'receivables';

const TABS: { kind: ReportKind; label: string; operations: boolean }[] = [
  { kind: 'stock', label: 'Stock', operations: true },
  { kind: 'production', label: 'Production', operations: true },
  { kind: 'sales', label: 'Sales', operations: false },
  { kind: 'receivables', label: 'Receivables ageing', operations: false },
];

// Printing shows only the report: no menu, top bar or buttons, black text on white.
const printStyles = (
  <GlobalStyles styles={{
    '@media print': {
      '.MuiDrawer-root, .MuiAppBar-root, .no-print': { display: 'none !important' },
      'main': { marginTop: '0 !important' },
      'body, body *': { color: '#000 !important', backgroundColor: 'transparent !important', boxShadow: 'none !important' },
    },
  }} />
);

const SectionTitle: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <Typography sx={{ fontSize: '14px', fontWeight: 700, color: 'text.primary', mt: 3, mb: 1 }}>{children}</Typography>
);

const monthStart = () => `${todayISO().slice(0, 8)}01`;
const formLabel = (r: StockReportRow) => (r.stockForm === 'Bulk' ? 'Unpacked bulk' : r.unit === 'bag' ? 'Packed bags' : 'Stock');
const qty = (value: number, unit: string) => `${formatNumber(value, 3)} ${unitLabel(value, unit)}`;

const Tile: React.FC<{ label: string; value: string; tone?: 'error' }> = ({ label, value, tone }) => (
  <Paper variant="outlined" sx={{ p: 2, borderRadius: '8px', minWidth: 0 }}>
    <Typography sx={{ fontSize: '11px', fontWeight: 700, letterSpacing: '0.5px', textTransform: 'uppercase', color: 'text.secondary' }}>{label}</Typography>
    <Typography sx={{ fontSize: '22px', fontWeight: 700, mt: 0.5, color: tone === 'error' ? 'error.main' : 'text.primary', overflowWrap: 'anywhere' }}>{value}</Typography>
  </Paper>
);

const Tiles: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(5, 1fr)' }, gap: 1.5, mb: 3 }}>{children}</Box>
);

const Reports: React.FC = () => {
  const { user } = useAuth();
  const tabs = TABS.filter((t) => (t.operations ? canSeeOperationsReports(user?.role) : canSeeSalesReports(user?.role)));
  const [kind, setKind] = useState<ReportKind>(tabs[0]?.kind ?? 'stock');
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState(todayISO());

  const period = `from=${from}&to=${to}`;
  const validPeriod = !!from && !!to && from <= to;
  const stock = useFetch<{ rows: StockReportRow[] }>(kind === 'stock' && validPeriod ? `/reports/stock?${period}` : null);
  const production = useFetch<ProductionReport>(kind === 'production' && validPeriod ? `/reports/production?${period}` : null);
  const sales = useFetch<SalesReport>(kind === 'sales' && validPeriod ? `/reports/sales?${period}` : null);
  const receivables = useFetch<ReceivablesReport>(kind === 'receivables' ? '/reports/receivables' : null);

  const suffix = kind === 'receivables' ? todayISO() : `${from}-to-${to}`;
  const exportCsv = () => {
    if (kind === 'stock' && stock.data)
      downloadCsv(`stock-${suffix}.csv`, ['Product', 'Code', 'Category', 'Form', 'Unit', 'Opening', 'Received', 'Produced/packed', 'Used', 'Dispatched', 'Closing'],
        stock.data.rows.map((r) => [r.productName, r.productCode, r.category, formLabel(r), r.unit, r.opening, r.received, r.produced, r.used, r.dispatched, r.closing]));
    if (kind === 'production' && production.data)
      downloadCsv(`production-${suffix}.csv`, ['Batch', 'Date', 'Shift', 'Operator', 'Input kg', 'Output kg', 'Waste kg', 'Yield %'],
        production.data.batches.map((b) => [b.batchNumber, b.productionDate.slice(0, 10), b.shiftNumber, b.operatorName ?? '', b.input, b.output, b.waste, b.yield]));
    if (kind === 'sales' && sales.data)
      downloadCsv(`sales-${suffix}.csv`, ['Invoice', 'Date', 'Customer', 'Status', 'Net amount', 'Paid', 'Balance'],
        sales.data.invoices.map((i) => [i.invoiceNumber, i.invoiceDate.slice(0, 10), i.customerName, i.status, i.netAmount, i.paid, i.balance]));
    if (kind === 'receivables' && receivables.data)
      downloadCsv(`receivables-${suffix}.csv`, ['Customer', 'Invoices', 'Not yet due', '1-30 days', '31-60 days', '61+ days', 'Total'],
        receivables.data.byCustomer.map((c) => [c.customerName, c.invoices, c.aging.notDue, c.aging.days1To30, c.aging.days31To60, c.aging.days61Plus, c.aging.total]));
  };

  if (tabs.length === 0) return <Typography>No reports are available for your role.</Typography>;

  return (
    <Box>
      {printStyles}
      <PageHeader title="Reports" subtitle="Stock, production, sales and money owed, for any period"
        actions={
          <Box className="no-print" sx={{ display: 'flex', gap: 1 }}>
            <Button variant="outlined" startIcon={<DownloadIcon />} onClick={exportCsv}>CSV</Button>
            <Button variant="outlined" startIcon={<PrintIcon />} onClick={() => window.print()}>Print</Button>
          </Box>
        } />

      <Tabs className="no-print" value={kind} onChange={(_, v) => setKind(v)} variant="scrollable" sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        {tabs.map((t) => <Tab key={t.kind} value={t.kind} label={t.label} />)}
      </Tabs>

      <Typography sx={{ display: 'none', '@media print': { display: 'block' }, fontSize: '20px', fontWeight: 700, mb: 1 }}>
        {TABS.find((t) => t.kind === kind)?.label} report, {kind === 'receivables' ? `as of ${formatDate(todayISO())}` : `${formatDate(from)} to ${formatDate(to)}`}
      </Typography>

      {kind !== 'receivables' && (
        <Box className="no-print" sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
          <TextField size="small" type="date" label="From" value={from} onChange={(e) => setFrom(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField size="small" type="date" label="To" value={to} onChange={(e) => setTo(e.target.value)} slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: from } }}
            error={!validPeriod} helperText={!validPeriod ? 'End date must be on or after the start date' : ' '} />
        </Box>
      )}

      {kind === 'stock' && (
        <>
          <Typography sx={{ fontSize: '13px', color: 'text.secondary', mb: 2 }}>
            Opening + received + produced + used + dispatched = closing. Used and dispatched are negative. Stock that existed before the 2026-10-04 ledger upgrade counts as opening stock.
          </Typography>
          <DataTable
            rows={stock.data?.rows ?? null}
            rowKey={(r) => `${r.productCode}-${r.stockForm}-${r.unit}`}
            loading={stock.loading}
            error={stock.error}
            onRetry={stock.reload}
            emptyTitle="No stock movements"
            columns={[
              { key: 'p', label: 'Product', render: (r) => <><strong>{r.productName}</strong> <Box component="span" sx={{ color: 'text.secondary' }}>{r.productCode}</Box></> },
              { key: 'f', label: 'Form', render: formLabel },
              { key: 'o', label: 'Opening', align: 'right', render: (r) => qty(r.opening, r.unit) },
              { key: 'r', label: 'Received', align: 'right', render: (r) => qty(r.received, r.unit) },
              { key: 'm', label: 'Produced / packed', align: 'right', render: (r) => qty(r.produced, r.unit) },
              { key: 'u', label: 'Used', align: 'right', render: (r) => qty(r.used, r.unit) },
              { key: 'd', label: 'Dispatched', align: 'right', render: (r) => qty(r.dispatched, r.unit) },
              { key: 'c', label: 'Closing', align: 'right', render: (r) => <strong>{qty(r.closing, r.unit)}</strong> },
            ]}
          />
        </>
      )}

      {kind === 'production' && production.data && (
        <>
          <Tiles>
            <Tile label="Batches completed" value={String(production.data.totals.batches)} />
            <Tile label="Wheat milled" value={qty(production.data.totals.input, 'kg')} />
            <Tile label="Output" value={qty(production.data.totals.output, 'kg')} />
            <Tile label="Waste" value={qty(production.data.totals.waste, 'kg')} />
            <Tile label="Yield" value={`${formatNumber(production.data.totals.yield, 2)}%`} />
          </Tiles>
          <SectionTitle>By day</SectionTitle>
          <DataTable rows={production.data.daily} rowKey={(d) => d.date} emptyTitle="No batches completed in this period" columns={[
            { key: 'd', label: 'Date', render: (d) => formatDate(d.date) },
            { key: 'b', label: 'Batches', align: 'right', render: (d) => d.batches },
            { key: 'i', label: 'Input', align: 'right', render: (d) => qty(d.input, 'kg') },
            { key: 'o', label: 'Output', align: 'right', render: (d) => qty(d.output, 'kg') },
            { key: 'w', label: 'Waste', align: 'right', render: (d) => qty(d.waste, 'kg') },
            { key: 'y', label: 'Yield', align: 'right', render: (d) => `${formatNumber(d.yield, 2)}%` },
          ]} />
          <SectionTitle>Output by product</SectionTitle>
          <DataTable rows={production.data.outputs} rowKey={(o) => `${o.productName}-${o.materialType}`} emptyTitle="No output" columns={[
            { key: 'p', label: 'Product', render: (o) => o.productName },
            { key: 't', label: 'Type', render: (o) => o.materialType },
            { key: 'q', label: 'Quantity', align: 'right', render: (o) => qty(o.quantityKg, 'kg') },
          ]} />
          <SectionTitle>Batches</SectionTitle>
          <DataTable rows={production.data.batches} rowKey={(b) => b.batchNumber} emptyTitle="No batches" columns={[
            { key: 'n', label: 'Batch', render: (b) => <strong>{b.batchNumber}</strong> },
            { key: 'd', label: 'Date', render: (b) => `${formatDate(b.productionDate)}, shift ${b.shiftNumber}` },
            { key: 'op', label: 'Operator', render: (b) => b.operatorName || '-' },
            { key: 'i', label: 'Input', align: 'right', render: (b) => qty(b.input, 'kg') },
            { key: 'o', label: 'Output', align: 'right', render: (b) => qty(b.output, 'kg') },
            { key: 'w', label: 'Waste', align: 'right', render: (b) => qty(b.waste, 'kg') },
            { key: 'y', label: 'Yield', align: 'right', render: (b) => `${formatNumber(b.yield, 2)}%` },
          ]} />
        </>
      )}

      {kind === 'sales' && sales.data && (
        <>
          <Tiles>
            <Tile label="Invoices" value={String(sales.data.totals.invoices)} />
            <Tile label="Billed (incl. tax)" value={formatMoney(sales.data.totals.net)} />
            <Tile label="Tax" value={formatMoney(sales.data.totals.tax)} />
            <Tile label="Received" value={formatMoney(sales.data.totals.paid)} />
            <Tile label="Still owed" value={formatMoney(sales.data.totals.balance)} tone={sales.data.totals.balance > 0 ? 'error' : undefined} />
          </Tiles>
          <SectionTitle>By customer</SectionTitle>
          <DataTable rows={sales.data.byCustomer} rowKey={(c) => c.customerName} emptyTitle="No invoices in this period" columns={[
            { key: 'c', label: 'Customer', render: (c) => <strong>{c.customerName}</strong> },
            { key: 'i', label: 'Invoices', align: 'right', render: (c) => c.invoices },
            { key: 'n', label: 'Billed', align: 'right', render: (c) => formatMoney(c.net) },
            { key: 'p', label: 'Received', align: 'right', render: (c) => formatMoney(c.paid) },
            { key: 'b', label: 'Still owed', align: 'right', render: (c) => formatMoney(c.balance) },
          ]} />
          <SectionTitle>By product</SectionTitle>
          <DataTable rows={sales.data.byProduct} rowKey={(p) => `${p.productName}-${p.unit}`} emptyTitle="No items billed" columns={[
            { key: 'p', label: 'Product', render: (p) => p.productName },
            { key: 'q', label: 'Quantity', align: 'right', render: (p) => qty(p.quantity, p.unit) },
            { key: 'a', label: 'Amount', align: 'right', render: (p) => formatMoney(p.amount) },
          ]} />
          <SectionTitle>Invoices</SectionTitle>
          <DataTable rows={sales.data.invoices} rowKey={(i) => i.invoiceNumber} emptyTitle="No invoices" columns={[
            { key: 'n', label: 'Invoice', render: (i) => <strong>{i.invoiceNumber}</strong> },
            { key: 'd', label: 'Date', render: (i) => formatDate(i.invoiceDate) },
            { key: 'c', label: 'Customer', render: (i) => i.customerName },
            { key: 'a', label: 'Amount', align: 'right', render: (i) => formatMoney(i.netAmount) },
            { key: 'b', label: 'Balance', align: 'right', render: (i) => formatMoney(i.balance) },
          ]} />
        </>
      )}

      {kind === 'receivables' && receivables.data && (
        <>
          <Tiles>
            <Tile label="Not yet due" value={formatMoney(receivables.data.totals.notDue)} />
            <Tile label="1-30 days late" value={formatMoney(receivables.data.totals.days1To30)} />
            <Tile label="31-60 days late" value={formatMoney(receivables.data.totals.days31To60)} />
            <Tile label="61+ days late" value={formatMoney(receivables.data.totals.days61Plus)} tone={receivables.data.totals.days61Plus > 0 ? 'error' : undefined} />
            <Tile label="Total owed" value={formatMoney(receivables.data.totals.total)} />
          </Tiles>
          <SectionTitle>By customer</SectionTitle>
          <DataTable rows={receivables.data.byCustomer} rowKey={(c) => c.customerName} emptyTitle="Nothing is owed" emptyHint="Every invoice is fully paid." columns={[
            { key: 'c', label: 'Customer', render: (c) => <strong>{c.customerName}</strong> },
            { key: 'n', label: 'Not yet due', align: 'right', render: (c) => formatMoney(c.aging.notDue) },
            { key: '1', label: '1-30 days', align: 'right', render: (c) => formatMoney(c.aging.days1To30) },
            { key: '3', label: '31-60 days', align: 'right', render: (c) => formatMoney(c.aging.days31To60) },
            { key: '6', label: '61+ days', align: 'right', render: (c) => formatMoney(c.aging.days61Plus) },
            { key: 't', label: 'Total', align: 'right', render: (c) => <strong>{formatMoney(c.aging.total)}</strong> },
          ]} />
          <SectionTitle>Unpaid invoices</SectionTitle>
          <DataTable rows={receivables.data.invoices} rowKey={(i) => i.invoiceNumber} emptyTitle="No unpaid invoices" columns={[
            { key: 'n', label: 'Invoice', render: (i) => <strong>{i.invoiceNumber}</strong> },
            { key: 'c', label: 'Customer', render: (i) => i.customerName },
            { key: 'd', label: 'Due', render: (i) => formatDate(i.dueDate) },
            { key: 'o', label: 'Days late', align: 'right', render: (i) => (i.daysOverdue > 0 ? i.daysOverdue : '-') },
            { key: 'b', label: 'Balance', align: 'right', render: (i) => formatMoney(i.balance) },
          ]} />
        </>
      )}

      {[production, sales, receivables].map((r, idx) => r.error && <Typography key={idx} color="error" sx={{ mt: 2 }}>{r.error}</Typography>)}
    </Box>
  );
};

export default Reports;
