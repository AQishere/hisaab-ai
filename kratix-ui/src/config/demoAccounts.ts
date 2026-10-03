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
