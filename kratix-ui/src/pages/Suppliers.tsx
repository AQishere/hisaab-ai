import CrudPage, { type CrudConfig } from '../components/CrudPage';
import StatusChip from '../components/ui/StatusChip';
import type { Supplier } from '../api/types';

const config: CrudConfig<Supplier> = {
  title: 'Suppliers',
  subtitle: 'Wheat and packaging suppliers you buy from',
  singular: 'Supplier',
  endpoint: '/suppliers',
  getId: (s) => s.supplierID,
  getName: (s) => s.supplierName,
  searchText: (s) => `${s.supplierName} ${s.contactPerson} ${s.phoneNumber} ${s.address}`,
  columns: [
    { key: 'name', label: 'Supplier', render: (s) => <strong>{s.supplierName}</strong> },
    { key: 'contact', label: 'Contact', render: (s) => s.contactPerson || '-' },
    { key: 'phone', label: 'Phone', render: (s) => s.phoneNumber || '-' },
    { key: 'terms', label: 'Payment terms', render: (s) => s.paymentTerms || '-' },
    { key: 'status', label: 'Status', render: (s) => <StatusChip status={s.isActive ? 'Active' : 'Inactive'} /> },
  ],
  fields: [
    { name: 'supplierName', label: 'Supplier name', required: true },
    { name: 'contactPerson', label: 'Contact person', half: true },
    { name: 'phoneNumber', label: 'Phone number', half: true },
    { name: 'ntn', label: 'NTN', half: true, helper: 'Optional' },
    { name: 'paymentTerms', label: 'Payment terms', type: 'select', options: ['COD', 'Net 7', 'Net 15', 'Net 30', 'Net 60'], half: true },
    { name: 'address', label: 'Address', type: 'textarea' },
    { name: 'isActive', label: 'Active supplier', type: 'switch', editOnly: true },
  ],
  toForm: (s) => ({
    supplierName: s?.supplierName ?? '',
    contactPerson: s?.contactPerson ?? '',
    phoneNumber: s?.phoneNumber ?? '',
    ntn: s?.ntn ?? '',
    paymentTerms: s?.paymentTerms ?? 'Net 30',
    address: s?.address ?? '',
    isActive: s?.isActive ?? true,
  }),
};

const Suppliers = () => <CrudPage config={config} />;
export default Suppliers;
