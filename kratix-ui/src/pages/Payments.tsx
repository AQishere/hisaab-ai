import React, { useState } from 'react';
import { Box, Button } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import type { Invoice, Payment } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { usePagedList } from '../hooks/usePagedList';
import { formatDate, formatMoney } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import ListToolbar from '../components/ui/ListToolbar';
import PaymentDialog from '../components/PaymentDialog';

const Payments: React.FC = () => {
  const payments = usePagedList<Payment>('/payments');
  // Only invoices with money still owed can take a payment.
  const invoices = useFetch<Invoice[]>('/invoices?open=true');
  const [open, setOpen] = useState(false);

  const newButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
      Record payment
    </Button>
  );

  return (
    <Box>
      <PageHeader
        title="Payments"
        subtitle={payments.total && payments.totalAmount !== null
          ? `${formatMoney(payments.totalAmount)} received across ${payments.total} payments${payments.searching ? ' matching the search' : ''}`
          : 'Money received from customers against invoices'}
        actions={newButton}
      />

      <ListToolbar search={payments.search} onSearch={payments.setSearch} placeholder="Search invoice, customer, mode or reference" />

      <DataTable
        rows={payments.rows}
        pagination={payments.pagination}
        rowKey={(p) => p.paymentID}
        loading={payments.loading}
        error={payments.error}
        onRetry={payments.reload}
        emptyTitle={payments.searching ? 'No payments match the search' : 'No payments yet'}
        emptyHint={payments.searching ? undefined : 'Record a payment when a customer pays an invoice.'}
        emptyAction={payments.searching ? undefined : newButton}
        columns={[
          { key: 'date', label: 'Date', render: (p) => formatDate(p.paymentDate) },
          { key: 'inv', label: 'Invoice', render: (p) => <strong>{p.invoiceNumber}</strong> },
          { key: 'cust', label: 'Customer', render: (p) => p.customerName ?? '-' },
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
