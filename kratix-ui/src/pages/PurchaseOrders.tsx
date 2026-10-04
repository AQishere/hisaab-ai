import React, { useMemo, useState } from 'react';
import { Alert, Box, Button, MenuItem, TextField } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import client from '../api/client';
import type { Product, PurchaseOrder, Supplier } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { useToast } from '../hooks/useToast';
import { formatDate, formatMoney, formatNumber, getErrorMessage, todayISO } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import FormDialog from '../components/ui/FormDialog';
import ConfirmDialog from '../components/ui/ConfirmDialog';
import StatusChip from '../components/ui/StatusChip';
import { DetailDialog, InfoGrid, SectionTitle } from '../components/ui/DetailDialog';
import ReasonDialog from '../components/ReasonDialog';
import LineItemsEditor from '../components/LineItemsEditor';
import { emptyLine, linesValid, toLinePayload, type LineDraft } from '../utils/lineItems';

const STATUSES = ['All', 'Draft', 'Sent', 'Received', 'Closed', 'Cancelled'];

const PurchaseOrders: React.FC = () => {
  const orders = useFetch<PurchaseOrder[]>('/purchaseorders');
  const suppliers = useFetch<Supplier[]>('/suppliers');
  const products = useFetch<Product[]>('/products');
  const toast = useToast();

  const [statusFilter, setStatusFilter] = useState('All');
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<PurchaseOrder | null>(null);
  const [supplierID, setSupplierID] = useState('');
  const [poDate, setPoDate] = useState(todayISO());
  const [expected, setExpected] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([emptyLine()]);
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);

  const [selected, setSelected] = useState<PurchaseOrder | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const rows = useMemo(
    () => orders.data?.filter((o) => statusFilter === 'All' || o.status === statusFilter) ?? null,
    [orders.data, statusFilter],
  );

  const openCreate = () => {
    setEditing(null);
    setSupplierID('');
    setPoDate(todayISO());
    setExpected('');
    setLines([emptyLine()]);
    setFormError('');
    setCreateOpen(true);
  };

  const openEdit = (po: PurchaseOrder) => {
    setEditing(po);
    setSupplierID(String(po.supplierID));
    setPoDate(po.poDate.slice(0, 10));
    setExpected(po.expectedDeliveryDate?.slice(0, 10) ?? '');
    setLines(po.lineItems.map((l) => ({ productID: String(l.productID), quantity: String(l.quantity), ratePerUnit: String(l.ratePerUnit) })));
    setFormError('');
    setSelected(null);
    setCreateOpen(true);
  };

  const create = async () => {
    setSaving(true);
    setFormError('');
    const body = { supplierID: Number(supplierID), poDate, expectedDeliveryDate: expected || null, lineItems: toLinePayload(lines) };
    try {
      if (editing) {
        const res = await client.put<PurchaseOrder>(`/purchaseorders/${editing.poid}/details`, body);
        toast(`${res.data.poNumber} updated`);
      } else {
        const res = await client.post<PurchaseOrder>('/purchaseorders', body);
        toast(`${res.data.poNumber} created as draft`);
      }
      setCreateOpen(false);
      orders.reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const [closing, setClosing] = useState(false);

  const setStatus = async (po: PurchaseOrder, status: string) => {
    setBusy(true);
    try {
      await client.put(`/purchaseorders/${po.poid}`, { expectedDeliveryDate: po.expectedDeliveryDate, status });
      toast(`${po.poNumber} marked as ${status.toLowerCase()}`);
      setSelected({ ...po, status });
      orders.reload();
    } catch (err) {
      toast(getErrorMessage(err), 'error');
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!selected) return;
    setBusy(true);
    try {
      await client.delete(`/purchaseorders/${selected.poid}`);
      toast(`${selected.poNumber} deleted`);
      setConfirmDelete(false);
      setSelected(null);
      orders.reload();
    } catch (err) {
      toast(getErrorMessage(err, 'Could not delete this purchase order.'), 'error');
      setConfirmDelete(false);
    } finally {
      setBusy(false);
    }
  };

  const activeSuppliers = suppliers.data?.filter((s) => s.isActive) ?? [];
  const newButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
      New purchase order
    </Button>
  );

  return (
    <Box>
      <PageHeader title="Purchase Orders" subtitle="Orders placed with suppliers for wheat and packaging" actions={newButton} />

      <TextField
        select
        size="small"
        label="Status"
        value={statusFilter}
        onChange={(e) => setStatusFilter(e.target.value)}
        sx={{ mb: 2, width: 180 }}
      >
        {STATUSES.map((s) => (
          <MenuItem key={s} value={s}>
            {s}
          </MenuItem>
        ))}
      </TextField>

      <DataTable
        rows={rows}
        rowKey={(o) => o.poid}
        loading={orders.loading}
        error={orders.error}
        onRetry={orders.reload}
        onRowClick={setSelected}
        emptyTitle={statusFilter === 'All' ? 'No purchase orders yet' : `No ${statusFilter.toLowerCase()} purchase orders`}
        emptyHint={statusFilter === 'All' ? 'Create a purchase order when you need to buy wheat or packaging.' : undefined}
        emptyAction={statusFilter === 'All' ? newButton : undefined}
        columns={[
          { key: 'no', label: 'PO number', render: (o) => <strong>{o.poNumber}</strong> },
          { key: 'supplier', label: 'Supplier', render: (o) => o.supplierName },
          { key: 'date', label: 'PO date', render: (o) => formatDate(o.poDate) },
          { key: 'expected', label: 'Expected', render: (o) => formatDate(o.expectedDeliveryDate) },
          { key: 'total', label: 'Total', align: 'right', render: (o) => formatMoney(o.totalAmount) },
          { key: 'status', label: 'Status', render: (o) => <StatusChip status={o.status} /> },
        ]}
      />

      <FormDialog
        open={createOpen}
        title={editing ? `Edit ${editing.poNumber}` : 'New purchase order'}
        submitLabel={editing ? 'Save changes' : 'Create draft'}
        maxWidth="md"
        submitting={saving}
        submitDisabled={!supplierID || !poDate || !linesValid(lines)}
        error={formError}
        onClose={() => setCreateOpen(false)}
        onSubmit={create}
      >
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '2fr 1fr 1fr' }, gap: 2 }}>
          <TextField
            select
            required
            label="Supplier"
            value={supplierID}
            onChange={(e) => setSupplierID(e.target.value)}
            helperText={activeSuppliers.length === 0 ? 'No active suppliers. Add one on the Suppliers page first.' : undefined}
          >
            {activeSuppliers.map((s) => (
              <MenuItem key={s.supplierID} value={String(s.supplierID)}>
                {s.supplierName}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            required
            type="date"
            label="PO date"
            value={poDate}
            onChange={(e) => setPoDate(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            type="date"
            label="Expected delivery"
            value={expected}
            onChange={(e) => setExpected(e.target.value)}
            slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: poDate } }}
          />
        </Box>
        <LineItemsEditor lines={lines} products={products.data ?? []} quantityLabel="Qty" onChange={setLines} />
      </FormDialog>

      {selected && (
        <DetailDialog
          open
          title={
            <>
              {selected.poNumber} <StatusChip status={selected.status} />
            </>
          }
          onClose={() => setSelected(null)}
          actions={
            <>
              {selected.status === 'Draft' && (
                <Button color="error" disabled={busy} onClick={() => setConfirmDelete(true)} sx={{ mr: 'auto' }}>
                  Delete draft
                </Button>
              )}
              {selected.status === 'Sent' && selected.lineItems.some((l) => l.receivedQuantity > 0) && (
                <Button color="inherit" disabled={busy} onClick={() => setClosing(true)}>
                  Close short
                </Button>
              )}
              {(selected.status === 'Draft' || selected.status === 'Sent') && (
                <Button color="inherit" disabled={busy} onClick={() => setStatus(selected, 'Cancelled')}>
                  Cancel order
                </Button>
              )}
              {selected.status === 'Draft' && (
                <Button color="inherit" disabled={busy} onClick={() => openEdit(selected)}>
                  Edit
                </Button>
              )}
              {selected.status === 'Draft' && (
                <Button variant="contained" disabled={busy} onClick={() => setStatus(selected, 'Sent')}>
                  Mark as sent
                </Button>
              )}
            </>
          }
        >
          <InfoGrid
            items={[
              ['Supplier', selected.supplierName],
              ['PO date', formatDate(selected.poDate)],
              ['Expected delivery', formatDate(selected.expectedDeliveryDate)],
              ['Total', formatMoney(selected.totalAmount)],
            ]}
          />
          {selected.closedReason && <Alert severity="info">Closed short: {selected.closedReason}</Alert>}
          <SectionTitle>Items</SectionTitle>
          <DataTable
            rows={selected.lineItems}
            rowKey={(l) => l.lineItemID}
            emptyTitle="No items"
            columns={[
              { key: 'p', label: 'Product', render: (l) => l.productName },
              { key: 'q', label: 'Ordered', align: 'right', render: (l) => `${formatNumber(l.quantity)} ${l.unit}` },
              { key: 'r', label: 'Rate', align: 'right', render: (l) => formatMoney(l.ratePerUnit) },
              { key: 't', label: 'Amount', align: 'right', render: (l) => formatMoney(l.totalAmount) },
              { key: 'rec', label: 'Received', align: 'right', render: (l) => `${formatNumber(l.receivedQuantity)} ${l.unit}` },
            ]}
          />
        </DetailDialog>
      )}

      <ConfirmDialog
        open={confirmDelete}
        title="Delete draft?"
        message={selected ? `${selected.poNumber} will be permanently deleted.` : ''}
        confirmLabel="Delete"
        busy={busy}
        onConfirm={remove}
        onClose={() => setConfirmDelete(false)}
      />
      {selected && (
        <ReasonDialog open={closing} title={`Close ${selected.poNumber} short`} submitLabel="Close order"
          description="Stops further deliveries on this order. Everything already received stays in stock."
          url={`/purchaseorders/${selected.poid}/close`} success={`${selected.poNumber} closed short`}
          onClose={() => setClosing(false)} onDone={() => { setClosing(false); setSelected(null); orders.reload(); }} />
      )}
    </Box>
  );
};

export default PurchaseOrders;
