import type { DemoAccount } from './demoAccounts';

// Copy this file to demoAccounts.local.ts (git-ignored) and fill in the passwords
// that match DemoUsers in CrescentFlourMill/Kratix.ERP/appsettings.Development.json.
const accounts: DemoAccount[] = [
  { username: 'owner', password: '', access: 'Everything' },
  { username: 'admin', password: '', access: 'Everything' },
  { username: 'operations', password: '', access: 'Purchasing, receiving, production, inventory' },
  { username: 'sales', password: '', access: 'Customers, sales orders, invoices, payments' },
  { username: 'worker', password: '', access: 'Receiving, production, inventory' },
];

export default accounts;
