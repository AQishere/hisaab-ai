import React, { useState } from 'react';
import { Box, Button, MenuItem, TextField, Typography } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import client from '../api/client';
import type { Invoice, Payment, SalesOrder } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { usePagedList } from '../hooks/usePagedList';
import { useToast } from '../hooks/useToast';
import { formatDate, formatMoney, getErrorMessage, todayISO, formatNumber, unitLabel } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import ListToolbar from '../components/ui/ListToolbar';
import FormDialog from '../components/ui/FormDialog';
import StatusChip from '../components/ui/StatusChip';
import { DetailDialog, InfoGrid, SectionTitle } from '../components/ui/DetailDialog';
import PaymentDialog from '../components/PaymentDialog';

const isOverdue = (i: Invoice) => i.balanceRemaining > 0 && !!i.dueDate && i.dueDate.slice(0, 10) < todayISO();

const addDays = (iso: string, days: number) => {
  const d = new Date(iso);
  d.setDate(d.getDate() + days);
  return d.toISOString();
};

const Invoices: React.FC = () => {
  const invoices = usePagedList<Invoice>('/invoices');
  // Fully dispatched or closed orders without an invoice yet, worked out by the server.
  const orders = useFetch<SalesOrder[]>('/salesorders?toInvoice=true');
  const toast = useToast();

  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState({ salesOrderID: '', invoiceDate: todayISO(), taxAmount: '', dueDays: '15' });
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);
  const [selected, setSelected] = useState<Invoice | null>(null);
  const payments = useFetch<Payment[]>(selected ? `/payments/invoice/${selected.invoiceID}` : null);
  const [payOpen, setPayOpen] = useState(false);

  const invoiceable = orders.data ?? [];

  const order = invoiceable.find((o) => String(o.salesOrderID) === form.salesOrderID);
  const tax = Number(form.taxAmount) || 0;
  // Empty means the default 15 days; 0 is valid (due on the invoice date).
  const dueDays = form.dueDays.trim() === '' ? 15 : Number(form.dueDays);
  // Invoices bill the dispatched quantity, which is less than the order total when an order was closed short.
  const subtotal = order?.lineItems.reduce((sum, l) => sum + Math.round(l.dispatchedQuantity * l.ratePerUnit * 100) / 100, 0) ?? 0;

  const create = async () => {
    setSaving(true);
    setFormError('');
    try {
      const res = await client.post<Invoice>('/invoices', {
        salesOrderID: Number(form.salesOrderID),
        invoiceDate: form.invoiceDate,
        taxAmount: tax,
        dueDays,
      });
      toast(`${res.data.invoiceNumber} created for ${formatMoney(res.data.netAmount)}`);
      setCreateOpen(false);
      invoices.reload();
      orders.reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const afterPayment = async () => {
    invoices.reload();
    payments.reload();
    if (selected) {
      const res = await client.get<Invoice>(`/invoices/${selected.invoiceID}`);
      setSelected(res.data);
    }
  };

  const newButton = (
    <Button
      variant="contained"
      startIcon={<AddIcon />}
      onClick={() => {
        setForm({ salesOrderID: '', invoiceDate: todayISO(), taxAmount: '', dueDays: '15' });
        setFormError('');
        setCreateOpen(true);
      }}
    >
      New invoice
    </Button>
  );

  return (
    <Box>
      <PageHeader title="Invoices" subtitle="Bills for dispatched sales orders and what is still owed" actions={newButton} />

      <ListToolbar search={invoices.search} onSearch={invoices.setSearch} placeholder="Search invoice, customer, SO number or status" />

      <DataTable
        rows={invoices.rows}
        pagination={invoices.pagination}
        rowKey={(i) => i.invoiceID}
        loading={invoices.loading}
        error={invoices.error}
        onRetry={invoices.reload}
        onRowClick={setSelected}
        emptyTitle={invoices.searching ? 'No invoices match the search' : 'No invoices yet'}
        emptyHint={invoices.searching ? undefined : 'Invoices are created from sales orders once they are fully dispatched.'}
        emptyAction={invoices.searching ? undefined : newButton}
        columns={[
          { key: 'no', label: 'Invoice', render: (i) => <strong>{i.invoiceNumber}</strong> },
          { key: 'cust', label: 'Customer', render: (i) => i.customerName },
          { key: 'so', label: 'Sales order', render: (i) => i.soNumber },
          { key: 'date', label: 'Date', render: (i) => formatDate(i.invoiceDate) },
          {
            key: 'due',
            label: 'Due',
            render: (i) => (
              <Box component="span" sx={{ color: isOverdue(i) ? 'error.main' : 'inherit', fontWeight: isOverdue(i) ? 600 : 400 }}>
                {formatDate(i.dueDate)}
                {isOverdue(i) && ' (overdue)'}
              </Box>
            ),
          },
          { key: 'net', label: 'Amount', align: 'right', render: (i) => formatMoney(i.netAmount) },
          { key: 'bal', label: 'Balance', align: 'right', render: (i) => <strong>{formatMoney(i.balanceRemaining)}</strong> },
          { key: 'status', label: 'Status', render: (i) => <StatusChip status={i.status} /> },
        ]}
      />

      <FormDialog
        open={createOpen}
        title="New invoice"
        submitLabel="Create invoice"
        submitting={saving}
        submitDisabled={!order || !form.invoiceDate || tax < 0 || !Number.isInteger(dueDays) || dueDays < 0}
        error={formError}
        onClose={() => setCreateOpen(false)}
        onSubmit={create}
      >
        <TextField
          select
          required
          label="Sales order"
          value={form.salesOrderID}
          onChange={(e) => setForm({ ...form, salesOrderID: e.target.value })}
          helperText={invoiceable.length === 0 ? 'No fully dispatched sales orders are waiting for an invoice.' : undefined}
        >
          {invoiceable.map((o) => (
            <MenuItem key={o.salesOrderID} value={String(o.salesOrderID)}>
              {o.soNumber} : {o.customerName}, {formatMoney(o.lineItems.reduce((sum, l) => sum + Math.round(l.dispatchedQuantity * l.ratePerUnit * 100) / 100, 0))}
            </MenuItem>
          ))}
        </TextField>
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr 1fr' }, gap: 2 }}>
          <TextField
            required
            type="date"
            label="Invoice date"
            value={form.invoiceDate}
            onChange={(e) => setForm({ ...form, invoiceDate: e.target.value })}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            type="number"
            label="Tax (PKR)"
            value={form.taxAmount}
            onChange={(e) => setForm({ ...form, taxAmount: e.target.value })}
            slotProps={{ htmlInput: { min: 0, step: 'any' } }}
          />
          <TextField
            type="number"
            label="Due in (days)"
            value={form.dueDays}
            onChange={(e) => setForm({ ...form, dueDays: e.target.value })}
            slotProps={{ htmlInput: { min: 0, step: 1 } }}
          />
        </Box>
        {order && (
          <Box sx={{ bgcolor: 'action.hover', borderRadius: '8px', p: 2 }}>
            <InfoGrid
              items={[
                ['Subtotal', formatMoney(subtotal)],
                ['Tax', formatMoney(tax)],
                ['Net amount', <strong key="n">{formatMoney(subtotal + tax)}</strong>],
                ['Due date', formatDate(addDays(form.invoiceDate, dueDays))],
              ]}
            />
          </Box>
        )}
      </FormDialog>

      {selected && !payOpen && (
        <DetailDialog
          open
          title={
            <>
              {selected.invoiceNumber} <StatusChip status={selected.status} />
            </>
          }
          onClose={() => setSelected(null)}
          actions={
            <>
              <Button color="inherit" onClick={() => window.open(`/invoices/${selected.invoiceID}/print`, '_blank')}>
                Print / PDF
              </Button>
              {selected.balanceRemaining > 0 && (
                <Button variant="contained" onClick={() => setPayOpen(true)}>
                  Record payment
                </Button>
              )}
            </>
          }
        >
          <InfoGrid
            items={[
              ['Customer', selected.customerName],
              ['Sales order', selected.soNumber],
              ['Invoice date', formatDate(selected.invoiceDate)],
              ['Due date', formatDate(selected.dueDate)],
              ['Subtotal', formatMoney(selected.totalAmount)],
              ['Tax', formatMoney(selected.taxAmount)],
              ['Net amount', formatMoney(selected.netAmount)],
              ['Balance', <Typography key="b" component="span" sx={{ fontWeight: 700, color: selected.balanceRemaining > 0 ? 'error.main' : 'success.main' }}>{formatMoney(selected.balanceRemaining)}</Typography>],
            ]}
          />
          <SectionTitle>Payments</SectionTitle>
          <DataTable
            rows={payments.data}
            rowKey={(p) => p.paymentID}
            loading={payments.loading}
            error={payments.error}
            emptyTitle="No payments yet"
            columns={[
              { key: 'date', label: 'Date', render: (p) => formatDate(p.paymentDate) },
              { key: 'mode', label: 'Mode', render: (p) => p.paymentMode },
              { key: 'ref', label: 'Reference', render: (p) => p.reference || '-' },
              { key: 'amt', label: 'Amount', align: 'right', render: (p) => formatMoney(p.paymentAmount) },
            ]}
          />
          <SectionTitle>Invoice items</SectionTitle>
          <DataTable rows={selected.lines} rowKey={(l) => l.invoiceLineID} emptyTitle="No item snapshots available" columns={[
            { key: 'name', label: 'Product', render: l => l.productName },
            { key: 'qty', label: 'Quantity', render: l => `${formatNumber(l.quantity, 3)} ${unitLabel(l.quantity, l.unit)}` },
            { key: 'rate', label: 'Rate', render: l => formatMoney(l.ratePerUnit) },
            { key: 'amount', label: 'Amount', render: l => formatMoney(l.totalAmount) },
            { key: 'legacy', label: 'Source', render: l => l.isLegacySnapshot ? 'Reconstructed from legacy order' : 'Recorded at invoicing' },
          ]} />
        </DetailDialog>
      )}

      {payOpen && (
        <PaymentDialog
          invoices={invoices.rows ?? []}
          fixedInvoiceID={selected?.invoiceID}
          onClose={() => setPayOpen(false)}
          onSaved={afterPayment}
        />
      )}
    </Box>
  );
};

export default Invoices;
