export interface DemoAccount {
  username: string;
  password: string;
  access: string;
}

// Real demo passwords live in demoAccounts.local.ts, which is git-ignored.
// Copy demoAccounts.example.ts to demoAccounts.local.ts to show the demo chips on the login page.
const localModules = import.meta.glob<{ default: DemoAccount[] }>('./demoAccounts.local.ts', { eager: true });

export const DEMO_ACCOUNTS: DemoAccount[] = Object.values(localModules)[0]?.default ?? [];
