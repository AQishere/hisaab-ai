import CrudPage, { type CrudConfig } from '../components/CrudPage';
import type { Warehouse } from '../api/types';
import { formatNumber } from '../utils/format';

const config: CrudConfig<Warehouse> = {
  title: 'Warehouses',
  subtitle: 'Godowns and stores where stock is kept',
  singular: 'Warehouse',
  endpoint: '/warehouses',
  getId: (w) => w.warehouseID,
  getName: (w) => w.warehouseName,
  searchText: (w) => `${w.warehouseName} ${w.location}`,
  columns: [
    { key: 'active', label: 'Status', render: (w) => w.isActive ? 'Active' : 'Inactive' },
    { key: 'name', label: 'Warehouse', render: (w) => <strong>{w.warehouseName}</strong> },
    { key: 'location', label: 'Location', render: (w) => w.location || '-' },
    { key: 'capacity', label: 'Capacity', align: 'right', render: (w) => formatNumber(w.capacity) },
  ],
  fields: [
    { name: 'isActive', label: 'Active', type: 'switch', editOnly: true },
    { name: 'warehouseName', label: 'Warehouse name', required: true },
    { name: 'location', label: 'Location', half: true },
    { name: 'capacity', label: 'Capacity', type: 'number', half: true, helper: 'In kg, bags or pieces' },
  ],
  toForm: (w) => ({
    isActive: w?.isActive ?? true,
    warehouseName: w?.warehouseName ?? '',
    location: w?.location ?? '',
    capacity: w ? String(w.capacity) : '',
  }),
};

const Warehouses = () => <CrudPage config={config} />;
export default Warehouses;
