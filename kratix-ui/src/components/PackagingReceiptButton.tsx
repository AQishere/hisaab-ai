import { useState } from 'react';
import { Button, MenuItem, TextField } from '@mui/material';
import client from '../api/client';
import type { Product, PurchaseOrder, Warehouse } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { useToast } from '../hooks/useToast';
import { getErrorMessage, formatNumber } from '../utils/format';
import FormDialog from './ui/FormDialog';

export default function PackagingReceiptButton({ onReceived }: { onReceived: () => void }) {
  // Only open (sent) orders can be received against.
  const orders = useFetch<PurchaseOrder[]>('/purchaseorders?status=Sent');
  const products = useFetch<Product[]>('/products');
  const warehouses = useFetch<Warehouse[]>('/warehouses');
  const toast = useToast();
  const [open, setOpen] = useState(false);
  const [lineID, setLineID] = useState('');
  const [quantity, setQuantity] = useState('');
  const [warehouseID, setWarehouseID] = useState('');
  const [lotNumber, setLotNumber] = useState('');
  const [binCode, setBinCode] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const lines = (orders.data ?? []).filter(o => o.status === 'Sent').flatMap(o => o.lineItems.filter(l =>
    l.receivedQuantity < l.quantity && products.data?.some(p => p.productID === l.productID && p.category === 'Packaging' && p.unit === 'piece'))
    .map(l => ({ ...l, poid: o.poid, poNumber: o.poNumber })));
  const line = lines.find(l => String(l.lineItemID) === lineID);
  return <>
    <Button variant="outlined" onClick={() => {
      orders.reload(); products.reload(); warehouses.reload(); setLineID(''); setQuantity(''); setWarehouseID(''); setLotNumber(''); setBinCode(''); setError(''); setOpen(true);
    }}>Receive packaging</Button>
    <FormDialog open={open} title="Receive packaging" submitLabel="Receive into stock" submitting={saving} error={error}
      submitDisabled={!line || !warehouseID || !lotNumber.trim() || !Number.isInteger(Number(quantity)) || Number(quantity) <= 0 || Number(quantity) > line.quantity - line.receivedQuantity}
      onClose={() => setOpen(false)} onSubmit={async () => {
        if (!line) return;
        setSaving(true); setError('');
        try {
          await client.post('/inboundreceipts/count-receipt', { poid: line.poid, poLineItemID: line.lineItemID, quantity: Number(quantity), warehouseID: Number(warehouseID), lotNumber: lotNumber.trim(), binCode: binCode.trim() });
          toast('Packaging received into stock'); setOpen(false); onReceived();
        } catch (err) { setError(getErrorMessage(err)); } finally { setSaving(false); }
      }}>
      <TextField select required label="Purchase order line" value={lineID} onChange={e => setLineID(e.target.value)} helperText={lines.length ? 'Check the delivery and count accepted pieces before receiving.' : 'Create and send a purchase order for packaging in pieces first.'}>
        {lines.map(l => <MenuItem key={l.lineItemID} value={String(l.lineItemID)}>{l.poNumber}: {l.productName}, {formatNumber(l.quantity - l.receivedQuantity)} pieces remaining</MenuItem>)}
      </TextField>
      <TextField required type="number" label="Accepted pieces" value={quantity} onChange={e => setQuantity(e.target.value)} slotProps={{ htmlInput: { min: 1, step: 1 } }} />
      <TextField select required label="Warehouse" value={warehouseID} onChange={e => setWarehouseID(e.target.value)}>
        {warehouses.data?.filter(w => w.isActive).map(w => <MenuItem key={w.warehouseID} value={String(w.warehouseID)}>{w.warehouseName}</MenuItem>)}
      </TextField>
      <TextField required label="Lot number" value={lotNumber} onChange={e => setLotNumber(e.target.value)} slotProps={{ htmlInput: { maxLength: 100 } }} />
      <TextField label="Bin" value={binCode} onChange={e => setBinCode(e.target.value)} slotProps={{ htmlInput: { maxLength: 64 } }} />
    </FormDialog>
  </>;
}
