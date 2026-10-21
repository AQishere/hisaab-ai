export type Role = 'Admin' | 'Owner' | 'Operations' | 'Sales' | 'Worker';

export const ROLE_LABELS: Record<Role, string> = {
  Admin: 'Administrator',
  Owner: 'Owner',
  Operations: 'Operations',
  Sales: 'Sales',
  Worker: 'Worker',
};

const MANAGEMENT: Role[] = ['Admin', 'Owner'];
const PURCHASING: Role[] = [...MANAGEMENT, 'Operations'];
const FLOOR: Role[] = [...PURCHASING, 'Worker'];
const SALES_FINANCE: Role[] = [...MANAGEMENT, 'Sales'];

// Page access only; the API enforces the same rules (Kratix.ERP/Auth/Roles.cs).
export const PAGE_ACCESS: Record<string, Role[]> = {
  // Every role has a dashboard; the API returns only the sections that role may see.
  '/dashboard': [...FLOOR, 'Sales'],
  '/suppliers': PURCHASING,
  '/customers': SALES_FINANCE,
  '/products': PURCHASING,
  '/warehouses': PURCHASING,
  '/purchase-orders': PURCHASING,
  '/inbound-receipts': FLOOR,
  '/production': FLOOR,
  '/inventory': FLOOR,
  '/sales-orders': SALES_FINANCE,
  '/invoices': SALES_FINANCE,
  '/payments': SALES_FINANCE,
  '/reports': [...FLOOR, 'Sales'],
  '/users': MANAGEMENT,
  // Last, so it never becomes anyone's home page. The agent applies the same ERP role rules to every answer.
  '/assistant': [...FLOOR, 'Sales'],
};

// Report tabs follow the same split as the API (Controllers/ReportsController.cs).
export const canSeeOperationsReports = (role: string | undefined): boolean => FLOOR.includes(role as Role);
export const canSeeSalesReports = (role: string | undefined): boolean => SALES_FINANCE.includes(role as Role);

export const canPurchase = (role: string | undefined): boolean => PURCHASING.includes(role as Role);

export const canAccess = (role: string | undefined, path: string): boolean =>
  !!role && (PAGE_ACCESS[path]?.includes(role as Role) ?? false);

export const homePath = (role: string | undefined): string =>
  Object.keys(PAGE_ACCESS).find((path) => canAccess(role, path)) ?? '/login';
