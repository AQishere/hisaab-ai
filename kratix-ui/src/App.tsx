import { lazy, Suspense } from 'react';
import { Box, CircularProgress } from '@mui/material';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { AuthProvider } from './context/AuthContext';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import { ToastProvider } from './context/ToastContext';
import { TOKENS } from './styles/tokens';

const Login = lazy(() => import('./pages/Login'));
const Dashboard = lazy(() => import('./pages/Dashboard'));
const Suppliers = lazy(() => import('./pages/Suppliers'));
const Customers = lazy(() => import('./pages/Customers'));
const Products = lazy(() => import('./pages/Products'));
const Warehouses = lazy(() => import('./pages/Warehouses'));
const PurchaseOrders = lazy(() => import('./pages/PurchaseOrders'));
const InboundReceipts = lazy(() => import('./pages/InboundReceipts'));
const Production = lazy(() => import('./pages/Production'));
const Inventory = lazy(() => import('./pages/Inventory'));
const SalesOrders = lazy(() => import('./pages/SalesOrders'));
const Invoices = lazy(() => import('./pages/Invoices'));
const Payments = lazy(() => import('./pages/Payments'));
const Reports = lazy(() => import('./pages/Reports'));
const Users = lazy(() => import('./pages/Users'));
const InvoicePrint = lazy(() => import('./pages/InvoicePrint'));
const Assistant = lazy(() => import('./pages/Assistant'));

// Yellow fails contrast as text on white, so accent-coloured text uses dark gold in light mode.
const ACCENT_VARS = {
  light: {
    '--accent-text': '#854d0e',
    '--accent-border': '#ca8a04',
    '--headline': '#18181b',
    '--tone-neutral': '#52525b',
    '--tone-accent': '#854d0e',
    '--tone-warning': '#c2410c',
    '--tone-success': '#15803d',
    '--tone-error': '#b91c1c',
  },
  dark: {
    '--accent-text': '#facc15',
    '--accent-border': '#facc15',
    '--headline': '#facc15',
    '--tone-neutral': '#a1a1aa',
    '--tone-accent': '#facc15',
    '--tone-warning': '#fb923c',
    '--tone-success': '#4ade80',
    '--tone-error': '#f87171',
  },
};

const transition = `all ${TOKENS.motion.transition.duration}ms ${TOKENS.motion.transition.easing}`;

const theme = createTheme({
  cssVariables: { colorSchemeSelector: 'data' },
  colorSchemes: {
    light: {
      palette: {
        primary: { main: '#facc15', dark: '#eab308', light: '#fef9c3', contrastText: '#18181b' },
        secondary: { main: '#52525b' },
        success: { main: '#16a34a' },
        warning: { main: '#ea580c' },
        error: { main: '#dc2626' },
        info: { main: '#0891b2' },
        background: { default: '#fafafa', paper: '#ffffff' },
        text: { primary: '#18181b', secondary: '#71717a', disabled: '#a1a1aa' },
        divider: '#e4e4e7',
        action: { hover: '#f4f4f5', disabledBackground: '#e4e4e7' },
      },
    },
    dark: {
      palette: {
        primary: { main: '#facc15', dark: '#eab308', light: '#422006', contrastText: '#0a0a0a' },
        secondary: { main: '#a1a1aa' },
        success: { main: '#4ade80' },
        warning: { main: '#fb923c' },
        error: { main: '#f87171' },
        info: { main: '#22d3ee' },
        background: { default: '#0a0a0a', paper: '#141414' },
        text: { primary: '#fafafa', secondary: '#a1a1aa', disabled: '#52525b' },
        divider: '#262626',
        action: { hover: '#1c1c1c', disabledBackground: '#262626' },
      },
    },
  },
  typography: {
    fontFamily: TOKENS.typography.fontFamily,
    fontSize: 13,
    button: { fontWeight: 600, textTransform: 'none', letterSpacing: '0.3px' },
  },
  shape: { borderRadius: 8 },
  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: TOKENS.radius.base,
          transition,
          '&:active': { transform: 'translateY(1px)' },
        },
        contained: { boxShadow: 'none', '&:hover': { boxShadow: 'none' } },
      },
      variants: [
        {
          props: { variant: 'text', color: 'primary' },
          style: { color: 'var(--accent-text)' },
        },
        {
          props: { variant: 'outlined', color: 'primary' },
          style: {
            color: 'var(--accent-text)',
            borderColor: 'color-mix(in srgb, var(--accent-text) 45%, transparent)',
            '&:hover': { borderColor: 'var(--accent-text)', backgroundColor: 'color-mix(in srgb, var(--mui-palette-primary-main) 14%, transparent)' },
          },
        },
      ],
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: TOKENS.radius.base,
          fontSize: '14px',
          transition,
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: 'var(--accent-border)' },
        },
      },
    },
    MuiCssBaseline: {
      styleOverrides: (theme) => ({
        body: { ...ACCENT_VARS.light, ...theme.applyStyles('dark', ACCENT_VARS.dark) },
      }),
    },
    MuiInputLabel: {
      styleOverrides: { root: { '&.Mui-focused': { color: 'var(--accent-text)' } } },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: TOKENS.radius.md,
          border: '1px solid var(--mui-palette-divider)',
          boxShadow: 'none',
          backgroundImage: 'none',
          transition,
        },
      },
    },
    MuiPaper: {
      styleOverrides: { root: { backgroundImage: 'none' } },
    },
    MuiAlert: {
      styleOverrides: { root: { borderRadius: TOKENS.radius.base } },
    },
  },
});

function App() {
  return (
    <ThemeProvider theme={theme} defaultMode="system">
      <CssBaseline enableColorScheme />
      <Router>
        <AuthProvider>
          <ToastProvider>
          <Suspense fallback={<Box role="status" aria-label="Loading page" sx={{ p: 4 }}><CircularProgress /></Box>}>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route
              element={
                <ProtectedRoute>
                  <Layout />
                </ProtectedRoute>
              }
            >
              <Route path="/dashboard" element={<Dashboard />} />
              <Route path="/suppliers" element={<Suppliers />} />
              <Route path="/customers" element={<Customers />} />
              <Route path="/products" element={<Products />} />
              <Route path="/warehouses" element={<Warehouses />} />
              <Route path="/purchase-orders" element={<PurchaseOrders />} />
              <Route path="/inbound-receipts" element={<InboundReceipts />} />
              <Route path="/production" element={<Production />} />
              <Route path="/inventory" element={<Inventory />} />
              <Route path="/sales-orders" element={<SalesOrders />} />
              <Route path="/invoices" element={<Invoices />} />
              <Route path="/payments" element={<Payments />} />
              <Route path="/reports" element={<Reports />} />
              <Route path="/users" element={<Users />} />
              <Route path="/assistant" element={<Assistant />} />
            </Route>
            <Route
              path="/invoices/:id/print"
              element={
                <ProtectedRoute accessPath="/invoices">
                  <InvoicePrint />
                </ProtectedRoute>
              }
            />
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
          </Suspense>
          </ToastProvider>
        </AuthProvider>
      </Router>
    </ThemeProvider>
  );
}

export default App;
