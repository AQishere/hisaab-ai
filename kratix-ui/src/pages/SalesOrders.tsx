import React, { useState } from 'react';
import { Alert, Box, Button, MenuItem, TextField, Typography } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import client from '../api/client';
import { DispatchHistory } from '../components/HistoryTables';
import type { Customer, Product, SalesOrder, SOLineItem } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { useToast } from '../context/ToastContext';
import { formatDate, formatMoney, formatNumber, getErrorMessage, todayISO } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import FormDialog from '../components/ui/FormDialog';
import StatusChip from '../components/ui/StatusChip';
import { DetailDialog, InfoGrid, SectionTitle } from '../components/ui/DetailDialog';
import LineItemsEditor, { emptyLine, lineTotal, linesValid, toLinePayload, type LineDraft } from '../components/LineItemsEditor';

const SalesOrders: React.FC = () => {
  const orders = useFetch<SalesOrder[]>('/salesorders');
  const customers = useFetch<Customer[]>('/customers');
  const products = useFetch<Product[]>('/products');
  const toast = useToast();

  const [createOpen, setCreateOpen] = useState(false);
  const [customerID, setCustomerID] = useState('');
  const [soDate, setSoDate] = useState(todayISO());
  const [expected, setExpected] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([emptyLine()]);
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);

  const [selected, setSelected] = useState<SalesOrder | null>(null);
  const [dispatching, setDispatching] = useState<SOLineItem | null>(null);
  const [dispatch, setDispatch] = useState({ dispatchedQuantity: '', truckNumber: '', driverName: '' });

  const activeCustomers = customers.data?.filter((c) => c.isActive) ?? [];
  const sellable = products.data?.filter((p) => p.category === 'Finished Good' || p.category === 'By-product') ?? [];
  const customer = activeCustomers.find((c) => String(c.customerID) === customerID);
  const orderTotal = lines.reduce((s, l) => s + lineTotal(l), 0);
  const overCredit = !!customer && customer.creditLimit > 0 && customer.outstandingBalance + orderTotal > customer.creditLimit;
  const remaining = dispatching ? dispatching.quantity - dispatching.dispatchedQuantity : 0;

  const openCreate = () => {
    setCustomerID('');
    setSoDate(todayISO());
    setExpected('');
    setLines([emptyLine()]);
    setFormError('');
    setCreateOpen(true);
  };

  const create = async () => {
    setSaving(true);
    setFormError('');
    try {
      const res = await client.post<SalesOrder>('/salesorders', {
        customerID: Number(customerID),
        soDate,
        expectedDeliveryDate: expected || null,
        lineItems: toLinePayload(lines),
      });
      toast(`${res.data.soNumber} created`);
      setCreateOpen(false);
      orders.reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const submitDispatch = async () => {
    if (!selected || !dispatching) return;
    setSaving(true);
    setFormError('');
    try {
      await client.put(`/salesorders/${selected.salesOrderID}/dispatch/${dispatching.lineItemID}`, {
        dispatchedQuantity: Number(dispatch.dispatchedQuantity),
        truckNumber: dispatch.truckNumber.trim(),
        driverName: dispatch.driverName.trim(),
      });
      toast(`${formatNumber(Number(dispatch.dispatchedQuantity))} of ${dispatching.productName} dispatched`);
      setDispatching(null);
      const res = await client.get<SalesOrder>(`/salesorders/${selected.salesOrderID}`);
      setSelected(res.data);
      orders.reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const newButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
      New sales order
    </Button>
  );

  return (
    <Box>
      <PageHeader title="Sales Orders" subtitle="Customer orders and their dispatch to trucks" actions={newButton} />

      <DataTable
        rows={orders.data}
        rowKey={(o) => o.salesOrderID}
        loading={orders.loading}
        error={orders.error}
        onRetry={orders.reload}
        onRowClick={setSelected}
        emptyTitle="No sales orders yet"
        emptyHint="Create a sales order when a customer places an order."
        emptyAction={newButton}
        columns={[
          { key: 'no', label: 'SO number', render: (o) => <strong>{o.soNumber}</strong> },
          { key: 'cust', label: 'Customer', render: (o) => o.customerName },
          { key: 'date', label: 'Order date', render: (o) => formatDate(o.soDate) },
          { key: 'exp', label: 'Expected', render: (o) => formatDate(o.expectedDeliveryDate) },
          { key: 'total', label: 'Total', align: 'right', render: (o) => formatMoney(o.totalAmount) },
          { key: 'status', label: 'Status', render: (o) => <StatusChip status={o.status} /> },
        ]}
      />

      <FormDialog
        open={createOpen}
        title="New sales order"
        submitLabel="Create order"
        maxWidth="md"
        submitting={saving}
        submitDisabled={!customerID || !soDate || !linesValid(lines)}
        error={formError}
        onClose={() => setCreateOpen(false)}
        onSubmit={create}
      >
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '2fr 1fr 1fr' }, gap: 2 }}>
          <TextField
            select
            required
            label="Customer"
            value={customerID}
            onChange={(e) => setCustomerID(e.target.value)}
            helperText={activeCustomers.length === 0 ? 'No active customers. Add one on the Customers page first.' : undefined}
          >
            {activeCustomers.map((c) => (
              <MenuItem key={c.customerID} value={String(c.customerID)}>
                {c.customerName} ({c.type})
              </MenuItem>
            ))}
          </TextField>
          <TextField
            required
            type="date"
            label="Order date"
            value={soDate}
            onChange={(e) => setSoDate(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            type="date"
            label="Expected delivery"
            value={expected}
            onChange={(e) => setExpected(e.target.value)}
            slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: soDate } }}
          />
        </Box>
        {customer && (
          <Typography sx={{ fontSize: '13px', color: 'text.secondary', mt: -1 }}>
            Outstanding {formatMoney(customer.outstandingBalance)} of {customer.creditLimit > 0 ? formatMoney(customer.creditLimit) : 'no'} credit limit
          </Typography>
        )}
        <LineItemsEditor lines={lines} products={sellable} quantityLabel="Qty" onChange={setLines} />
        {overCredit && customer && (
          <Alert severity="warning">
            This order takes {customer.customerName} to {formatMoney(customer.outstandingBalance + orderTotal)}, above their credit limit of{' '}
            {formatMoney(customer.creditLimit)}.
          </Alert>
        )}
      </FormDialog>

      {selected && !dispatching && (
        <DetailDialog
          open
          title={
            <>
              {selected.soNumber} <StatusChip status={selected.status} />
            </>
          }
          onClose={() => setSelected(null)}
        >
          <InfoGrid
            items={[
              ['Customer', selected.customerName],
              ['Order date', formatDate(selected.soDate)],
              ['Expected delivery', formatDate(selected.expectedDeliveryDate)],
              ['Total', formatMoney(selected.totalAmount)],
            ]}
          />
          <SectionTitle>Items</SectionTitle>
          <DataTable
            rows={selected.lineItems}
            rowKey={(l) => l.lineItemID}
            emptyTitle="No items"
            columns={[
              { key: 'p', label: 'Product', render: (l) => l.productName },
              { key: 'q', label: 'Ordered', align: 'right', render: (l) => formatNumber(l.quantity) },
              { key: 'r', label: 'Rate', align: 'right', render: (l) => formatMoney(l.ratePerUnit) },
              { key: 'd', label: 'Dispatched', align: 'right', render: (l) => formatNumber(l.dispatchedQuantity) },
              { key: 'truck', label: 'Last truck', render: (l) => (l.truckNumber ? `${l.truckNumber}, ${formatDate(l.dispatchDate)}` : '-') },
              { key: 's', label: 'Status', render: (l) => <StatusChip status={l.dispatchStatus} /> },
              {
                key: 'a',
                label: '',
                align: 'right',
                render: (l) =>
                  l.quantity > l.dispatchedQuantity ? (
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => {
                        setDispatch({ dispatchedQuantity: String(l.quantity - l.dispatchedQuantity), truckNumber: '', driverName: '' });
                        setFormError('');
                        setDispatching(l);
                      }}
                    >
                      Dispatch
                    </Button>
                  ) : null,
              },
            ]}
          />
          {selected.status === 'Dispatched' && (
            <Alert severity="success">Fully dispatched. You can now create the invoice on the Invoices page.</Alert>
          )}
          <DispatchHistory orderID={selected.salesOrderID} />
        </DetailDialog>
      )}

      <FormDialog
        open={!!dispatching}
        title={dispatching ? `Dispatch ${dispatching.productName}` : ''}
        submitLabel="Dispatch"
        submitting={saving}
        submitDisabled={
          !(Number(dispatch.dispatchedQuantity) > 0 && Number(dispatch.dispatchedQuantity) <= remaining) ||
          !dispatch.truckNumber.trim() ||
          !dispatch.driverName.trim()
        }
        error={formError}
        onClose={() => setDispatching(null)}
        onSubmit={submitDispatch}
      >
        <TextField
          required
          type="number"
          label="Quantity"
          value={dispatch.dispatchedQuantity}
          onChange={(e) => setDispatch({ ...dispatch, dispatchedQuantity: e.target.value })}
          error={Number(dispatch.dispatchedQuantity) > remaining}
          helperText={`${formatNumber(remaining)} left to dispatch`}
          slotProps={{ htmlInput: { min: 0, step: 'any' } }}
        />
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
          <TextField required label="Truck number" value={dispatch.truckNumber} onChange={(e) => setDispatch({ ...dispatch, truckNumber: e.target.value })} />
          <TextField required label="Driver name" value={dispatch.driverName} onChange={(e) => setDispatch({ ...dispatch, driverName: e.target.value })} />
        </Box>
      </FormDialog>
    </Box>
  );
};

export default SalesOrders;
