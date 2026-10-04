import React, { useState } from 'react';
import { Box, MenuItem, TextField } from '@mui/material';
import client from '../api/client';
import type { Invoice } from '../api/types';
import { useToast } from '../hooks/useToast';
import { formatMoney, formatNumber, getErrorMessage, todayISO } from '../utils/format';
import FormDialog from './ui/FormDialog';

// Mount only while open: the form is initialised once from props.
interface PaymentDialogProps {
  invoices: Invoice[];
  fixedInvoiceID?: number;
  onClose: () => void;
  onSaved: () => void;
}

const PaymentDialog: React.FC<PaymentDialogProps> = ({ invoices, fixedInvoiceID, onClose, onSaved }) => {
  const toast = useToast();
  const [form, setForm] = useState(() => {
    const fixed = invoices.find((i) => i.invoiceID === fixedInvoiceID);
    return {
      invoiceID: fixed ? String(fixed.invoiceID) : '',
      paymentDate: todayISO(),
      paymentMode: 'Cash',
      paymentAmount: fixed ? String(fixed.balanceRemaining) : '',
      reference: '',
    };
  });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const payable = invoices.filter((i) => i.balanceRemaining > 0);
  const invoice = invoices.find((i) => String(i.invoiceID) === form.invoiceID);
  const amount = Number(form.paymentAmount);

  const save = async () => {
    if (!invoice) return;
    setSaving(true);
    setError('');
    try {
      await client.post('/payments', {
        invoiceID: invoice.invoiceID,
        paymentDate: form.paymentDate,
        paymentMode: form.paymentMode,
        paymentAmount: amount,
        reference: form.reference.trim(),
      });
      toast(
        amount >= invoice.balanceRemaining
          ? `${invoice.invoiceNumber} is now fully paid`
          : `${formatMoney(amount)} received. ${formatMoney(invoice.balanceRemaining - amount)} still due on ${invoice.invoiceNumber}`,
      );
      onSaved();
      onClose();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormDialog
      open
      title="Record payment"
      submitLabel="Record payment"
      submitting={saving}
      submitDisabled={!invoice || !(amount > 0) || amount > (invoice?.balanceRemaining ?? 0) || !form.paymentDate}
      error={error}
      onClose={onClose}
      onSubmit={save}
    >
      <TextField
        select
        required
        label="Invoice"
        value={form.invoiceID}
        disabled={!!fixedInvoiceID}
        onChange={(e) => {
          const inv = invoices.find((i) => String(i.invoiceID) === e.target.value);
          setForm({ ...form, invoiceID: e.target.value, paymentAmount: inv ? String(inv.balanceRemaining) : '' });
        }}
        helperText={payable.length === 0 && !fixedInvoiceID ? 'There are no unpaid invoices.' : undefined}
      >
        {(fixedInvoiceID ? invoices.filter((i) => i.invoiceID === fixedInvoiceID) : payable).map((i) => (
          <MenuItem key={i.invoiceID} value={String(i.invoiceID)}>
            {i.invoiceNumber} : {i.customerName}, {formatMoney(i.balanceRemaining)} due
          </MenuItem>
        ))}
      </TextField>
      <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
        <TextField
          required
          type="number"
          label="Amount (PKR)"
          value={form.paymentAmount}
          onChange={(e) => setForm({ ...form, paymentAmount: e.target.value })}
          error={!!invoice && amount > invoice.balanceRemaining}
          helperText={invoice ? `Balance ${formatNumber(invoice.balanceRemaining)}` : undefined}
          slotProps={{ htmlInput: { min: 0, step: 'any' } }}
        />
        <TextField
          required
          type="date"
          label="Payment date"
          value={form.paymentDate}
          onChange={(e) => setForm({ ...form, paymentDate: e.target.value })}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField select label="Mode" value={form.paymentMode} onChange={(e) => setForm({ ...form, paymentMode: e.target.value })}>
          {['Cash', 'Bank Transfer', 'Cheque'].map((m) => (
            <MenuItem key={m} value={m}>
              {m}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          label="Reference"
          placeholder={form.paymentMode === 'Cheque' ? 'Cheque number' : form.paymentMode === 'Bank Transfer' ? 'Transaction ID' : 'Optional'}
          value={form.reference}
          onChange={(e) => setForm({ ...form, reference: e.target.value })}
        />
      </Box>
    </FormDialog>
  );
};

export default PaymentDialog;
