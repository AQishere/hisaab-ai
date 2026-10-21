export interface DemoAccount {
  username: string;
  password: string;
  access: string;
}

// Real demo passwords live in demoAccounts.local.ts, which is git-ignored.
// Copy demoAccounts.example.ts to demoAccounts.local.ts to show the demo chips on the login page.
// Development only: a production build (npm run build) never shows the chips or contains the passwords.
const localModules = import.meta.env.DEV
  ? import.meta.glob<{ default: DemoAccount[] }>('./demoAccounts.local.ts', { eager: true })
  : {};

export const DEMO_ACCOUNTS: DemoAccount[] = Object.values(localModules)[0]?.default ?? [];

// The public demo (VITE_DEMO_MODE=true, with Demo:Enabled on the API): one-click sign-in per role, no passwords.
export const PUBLIC_DEMO = import.meta.env.VITE_DEMO_MODE === 'true';

export const DEMO_ROLES = [
  { username: 'owner', role: 'Owner', access: 'Everything, including the AI assistant' },
  { username: 'operations', role: 'Operations', access: 'Purchasing, receiving, production, stock' },
  { username: 'sales', role: 'Sales', access: 'Customers, orders, invoices, payments' },
  { username: 'worker', role: 'Worker', access: 'Receiving, production, stock' },
  { username: 'admin', role: 'Admin', access: 'Everything, plus user accounts (read-only here)' },
];
