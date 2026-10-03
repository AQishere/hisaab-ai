import { Box } from '@mui/material';
import CrudPage, { type CrudConfig } from '../components/CrudPage';
import StatusChip from '../components/ui/StatusChip';
import type { Customer } from '../api/types';
import { formatMoney } from '../utils/format';

const config: CrudConfig<Customer> = {
  title: 'Customers',
  subtitle: 'Distributors, wholesalers, bakeries and retailers you sell to',
  singular: 'Customer',
  endpoint: '/customers',
  getId: (c) => c.customerID,
  getName: (c) => c.customerName,
  searchText: (c) => `${c.customerName} ${c.type} ${c.contactPerson} ${c.phoneNumber} ${c.address}`,
  columns: [
    { key: 'name', label: 'Customer', render: (c) => <strong>{c.customerName}</strong> },
    { key: 'type', label: 'Type', render: (c) => c.type || '-' },
    { key: 'phone', label: 'Phone', render: (c) => c.phoneNumber || '-' },
    { key: 'limit', label: 'Credit limit', align: 'right', render: (c) => formatMoney(c.creditLimit) },
    {
      key: 'outstanding',
      label: 'Outstanding',
      align: 'right',
      render: (c) => (
        <Box
          component="span"
          sx={{ color: c.creditLimit > 0 && c.outstandingBalance > c.creditLimit ? 'error.main' : 'inherit', fontWeight: 600 }}
        >
          {formatMoney(c.outstandingBalance)}
        </Box>
      ),
    },
    { key: 'status', label: 'Status', render: (c) => <StatusChip status={c.isActive ? 'Active' : 'Inactive'} /> },
  ],
  fields: [
    { name: 'customerName', label: 'Customer name', required: true },
    { name: 'type', label: 'Type', type: 'select', options: ['Distributor', 'Wholesaler', 'Bakery', 'Retailer'], required: true, half: true },
    { name: 'creditLimit', label: 'Credit limit (PKR)', type: 'number', half: true },
    { name: 'contactPerson', label: 'Contact person', half: true },
    { name: 'phoneNumber', label: 'Phone number', half: true },
    { name: 'address', label: 'Address', type: 'textarea' },
    { name: 'isActive', label: 'Active customer', type: 'switch', editOnly: true },
  ],
  toForm: (c) => ({
    customerName: c?.customerName ?? '',
    type: c?.type ?? 'Distributor',
    creditLimit: c ? String(c.creditLimit) : '',
    contactPerson: c?.contactPerson ?? '',
    phoneNumber: c?.phoneNumber ?? '',
    address: c?.address ?? '',
    isActive: c?.isActive ?? true,
  }),
};

const Customers = () => <CrudPage config={config} />;
export default Customers;
