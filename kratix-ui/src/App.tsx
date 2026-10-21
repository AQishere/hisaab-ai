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

// Hisaab brand: the logo's deep green on warm off-white; in dark mode a lighter green stays readable on green-black.
const ACCENT_VARS = {
  light: {
    '--accent-text': '#0e5a3a',
    '--accent-border': '#0e5a3a',
    '--headline': '#0e5a3a',
    '--tone-neutral': '#5c6660',
    '--tone-accent': '#0e5a3a',
    '--tone-warning': '#c2410c',
    '--tone-success': '#15803d',
    '--tone-error': '#b91c1c',
  },
  dark: {
    '--accent-text': '#7fcea4',
    '--accent-border': '#4fae7f',
    '--headline': '#7fcea4',
    '--tone-neutral': '#a3b0a8',
    '--tone-accent': '#7fcea4',
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
        primary: { main: '#0e5a3a', dark: '#0a4530', light: '#e4efe8', contrastText: '#fbf6ea' },
        secondary: { main: '#5c6660' },
        success: { main: '#16a34a' },
        warning: { main: '#ea580c' },
        error: { main: '#dc2626' },
        info: { main: '#0891b2' },
        background: { default: '#f8f6f0', paper: '#ffffff' },
        text: { primary: '#1b2420', secondary: '#66706a', disabled: '#a3aaa5' },
        divider: '#e6e2d8',
        action: { hover: '#f2efe6', disabledBackground: '#e6e2d8' },
      },
    },
    dark: {
      palette: {
        primary: { main: '#4fae7f', dark: '#3d9a6c', light: '#14301f', contrastText: '#0b1410' },
        secondary: { main: '#a3b0a8' },
        success: { main: '#4ade80' },
        warning: { main: '#fb923c' },
        error: { main: '#f87171' },
        info: { main: '#22d3ee' },
        background: { default: '#0b1410', paper: '#121d18' },
        text: { primary: '#f3efe4', secondary: '#a3b0a8', disabled: '#56635b' },
        divider: '#223029',
        action: { hover: '#18241e', disabledBackground: '#223029' },
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
