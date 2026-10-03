import React, { useState } from 'react';
import { Box, Button } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import type { Invoice, Payment } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { formatDate, formatMoney } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import PaymentDialog from '../components/PaymentDialog';

const Payments: React.FC = () => {
  const payments = useFetch<Payment[]>('/payments');
  const invoices = useFetch<Invoice[]>('/invoices');
  const [open, setOpen] = useState(false);

  const customerOf = (p: Payment) => invoices.data?.find((i) => i.invoiceID === p.invoiceID)?.customerName ?? '-';
  const totalReceived = payments.data?.reduce((s, p) => s + p.paymentAmount, 0) ?? 0;

  const newButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
      Record payment
    </Button>
  );

  return (
    <Box>
      <PageHeader
        title="Payments"
        subtitle={payments.data?.length ? `${formatMoney(totalReceived)} received across ${payments.data.length} payments` : 'Money received from customers against invoices'}
        actions={newButton}
      />

      <DataTable
        rows={payments.data}
        rowKey={(p) => p.paymentID}
        loading={payments.loading}
        error={payments.error}
        onRetry={payments.reload}
        emptyTitle="No payments yet"
        emptyHint="Record a payment when a customer pays an invoice."
        emptyAction={newButton}
        columns={[
          { key: 'date', label: 'Date', render: (p) => formatDate(p.paymentDate) },
          { key: 'inv', label: 'Invoice', render: (p) => <strong>{p.invoiceNumber}</strong> },
          { key: 'cust', label: 'Customer', render: customerOf },
          { key: 'mode', label: 'Mode', render: (p) => p.paymentMode },
          { key: 'ref', label: 'Reference', render: (p) => p.reference || '-' },
          { key: 'amt', label: 'Amount', align: 'right', render: (p) => <strong>{formatMoney(p.paymentAmount)}</strong> },
        ]}
      />

      {open && (
        <PaymentDialog
          invoices={invoices.data ?? []}
          onClose={() => setOpen(false)}
          onSaved={() => {
            payments.reload();
            invoices.reload();
          }}
        />
      )}
    </Box>
  );
};

export default Payments;
