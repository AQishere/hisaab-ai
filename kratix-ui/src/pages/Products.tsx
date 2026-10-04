import CrudPage, { type CrudConfig } from '../components/CrudPage';
import type { Product } from '../api/types';

const PRODUCT_CATEGORIES = ['Raw Material', 'Finished Good', 'By-product', 'Packaging', 'Waste'];

const config: CrudConfig<Product> = {
  title: 'Products',
  subtitle: 'Raw wheat, finished flour, by-products and packaging',
  singular: 'Product',
  endpoint: '/products',
  getId: (p) => p.productID,
  getName: (p) => p.productName,
  searchText: (p) => `${p.productCode} ${p.productName} ${p.category}`,
  columns: [
    { key: 'code', label: 'Code', render: (p) => <code>{p.productCode}</code> },
    { key: 'name', label: 'Product', render: (p) => <strong>{p.productName}</strong> },
    { key: 'category', label: 'Category', render: (p) => p.category },
    { key: 'unit', label: 'Unit', render: (p) => p.unit },
    { key: 'bagSize', label: 'Bag size', render: (p) => p.unit === 'bag' ? `${p.kgPerBag ?? '?'} kg` : '-' },
    { key: 'hsn', label: 'HSN code', render: (p) => p.hsnCode || '-' },
  ],
  fields: [
    { name: 'productCode', label: 'Product code', required: true, half: true, helper: 'For example ATTA-10' },
    { name: 'category', label: 'Category', type: 'select', options: PRODUCT_CATEGORIES, required: true, half: true },
    { name: 'productName', label: 'Product name', required: true },
    { name: 'unit', label: 'Unit', type: 'select', options: ['kg', 'bag', 'piece'], required: true, half: true },
    { name: 'kgPerBag', label: 'Kg per bag', type: 'number', half: true, helper: 'Required when the unit is bag' },
    { name: 'hsnCode', label: 'HSN code', half: true, helper: 'Optional, for tax' },
  ],
  toForm: (p) => ({
    productCode: p?.productCode ?? '',
    category: p?.category ?? 'Raw Material',
    productName: p?.productName ?? '',
    unit: p?.unit ?? 'kg',
    kgPerBag: p?.kgPerBag ? String(p.kgPerBag) : '',
    hsnCode: p?.hsnCode ?? '',
  }),
};

const Products = () => <CrudPage config={config} />;
export default Products;
