import React, { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  Box,
  AppBar,
  Toolbar,
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Typography,
  IconButton,
  Menu,
  MenuItem,
  Divider,
} from '@mui/material';
import {
  Dashboard as DashboardIcon,
  People as PeopleIcon,
  ShoppingCart as ShoppingCartIcon,
  LocalShipping as LocalShippingIcon,
  Factory as FactoryIcon,
  Inventory as InventoryIcon,
  Receipt as ReceiptIcon,
  Payment as PaymentIcon,
  Menu as MenuIcon,
  Logout as LogoutIcon,
  KeyboardArrowDown as KeyboardArrowDownIcon,
} from '@mui/icons-material';
import { useAuth } from '../context/AuthContext';
import ThemeToggle from './ThemeToggle';
import BrandLogo from './BrandLogo';
import { canAccess, ROLE_LABELS, type Role } from '../auth/permissions';

const DRAWER_WIDTH = 256;
const APPBAR_HEIGHT = 64;

type NavItem = { label: string; path: string; icon: React.ReactNode } | { label: string; section: true };

const menuItems: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: <DashboardIcon /> },
  { label: 'Master Data', section: true },
  { label: 'Suppliers', path: '/suppliers', icon: <PeopleIcon /> },
  { label: 'Customers', path: '/customers', icon: <PeopleIcon /> },
  { label: 'Products', path: '/products', icon: <ShoppingCartIcon /> },
  { label: 'Warehouses', path: '/warehouses', icon: <LocalShippingIcon /> },
  { label: 'Operations', section: true },
  { label: 'Purchase Orders', path: '/purchase-orders', icon: <ShoppingCartIcon /> },
  { label: 'Inbound Receipts', path: '/inbound-receipts', icon: <LocalShippingIcon /> },
  { label: 'Production', path: '/production', icon: <FactoryIcon /> },
  { label: 'Inventory', path: '/inventory', icon: <InventoryIcon /> },
  { label: 'Sales & Finance', section: true },
  { label: 'Sales Orders', path: '/sales-orders', icon: <ShoppingCartIcon /> },
  { label: 'Invoices', path: '/invoices', icon: <ReceiptIcon /> },
  { label: 'Payments', path: '/payments', icon: <PaymentIcon /> },
];

const Layout: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);

  const visibleItems = menuItems.filter((item, idx) => {
    if (!('section' in item)) return canAccess(user?.role, item.path);
    const nextSection = menuItems.findIndex((m, i) => i > idx && 'section' in m);
    const sectionPages = menuItems.slice(idx + 1, nextSection === -1 ? undefined : nextSection);
    return sectionPages.some((m) => !('section' in m) && canAccess(user?.role, m.path));
  });

  const roleLabel = ROLE_LABELS[user?.role as Role] ?? user?.role;

  const handleLogout = () => {
    setAnchorEl(null);
    logout();
    navigate('/login');
  };

  const drawerContent = (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', bgcolor: 'background.paper' }}>
      <Box sx={{ px: 2, py: 2.25, borderBottom: 1, borderColor: 'divider' }}>
        <BrandLogo height={30} />
      </Box>

      <Box
        sx={{
          flex: 1,
          overflowY: 'auto',
          py: 1.5,
          scrollbarWidth: 'thin',
          scrollbarColor: 'transparent transparent',
          transition: 'scrollbar-color 0.2s ease',
          '&:hover': { scrollbarColor: 'var(--mui-palette-text-disabled) transparent' },
        }}
      >
        <List disablePadding>
          {visibleItems.map((item, idx) => {
            if ('section' in item) {
              return (
                <Box key={item.label}>
                  {idx > 0 && <Divider sx={{ mt: 1, mb: 0.5, mx: 1 }} />}
                  <Typography
                    sx={{
                      fontSize: '11px',
                      fontWeight: 700,
                      color: 'text.secondary',
                      px: 2,
                      pt: 1,
                      pb: 0.5,
                      textTransform: 'uppercase',
                      letterSpacing: '0.5px',
                    }}
                  >
                    {item.label}
                  </Typography>
                </Box>
              );
            }

            const isActive = location.pathname === item.path;

            return (
              <ListItem key={item.path} disablePadding>
                <ListItemButton
                  onClick={() => {
                    navigate(item.path);
                    setMobileOpen(false);
                  }}
                  sx={{
                    mx: 1,
                    my: 0.25,
                    py: 0.75,
                    borderRadius: '8px',
                    color: isActive ? 'var(--accent-text)' : 'text.secondary',
                    bgcolor: isActive ? 'color-mix(in srgb, var(--mui-palette-primary-main) 20%, transparent)' : 'transparent',
                    fontWeight: isActive ? 600 : 500,
                    '&:hover': {
                      bgcolor: isActive
                        ? 'color-mix(in srgb, var(--mui-palette-primary-main) 28%, transparent)'
                        : 'action.hover',
                    },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 36, color: 'inherit', '& svg': { fontSize: 20 } }}>{item.icon}</ListItemIcon>
                  <ListItemText
                    primary={item.label}
                    slotProps={{ primary: { sx: { fontSize: '13px', fontWeight: 'inherit' } } }}
                  />
                </ListItemButton>
              </ListItem>
            );
          })}
        </List>
      </Box>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100dvh', width: '100%', bgcolor: 'background.default' }}>
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          zIndex: (theme) => theme.zIndex.drawer + 1,
          bgcolor: 'background.paper',
          color: 'text.primary',
          borderBottom: 1,
          borderColor: 'divider',
          height: APPBAR_HEIGHT,
        }}
      >
        <Toolbar sx={{ minHeight: `${APPBAR_HEIGHT}px !important`, px: 2 }}>
          <IconButton
            aria-label="open drawer"
            onClick={() => setMobileOpen(!mobileOpen)}
            sx={{ mr: 2, display: { sm: 'none' }, color: 'text.primary' }}
          >
            <MenuIcon />
          </IconButton>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, flex: 1, minWidth: 0 }}>
            <Box sx={{ width: 3, height: 30, borderRadius: 2, bgcolor: 'primary.main', flexShrink: 0 }} />
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontSize: '10px', fontWeight: 800, letterSpacing: '1.1px', lineHeight: 1.3, color: 'text.secondary' }}>
                WORKSPACE
              </Typography>
              <Typography sx={{ fontSize: { xs: '14px', sm: '15px' }, fontWeight: 750, letterSpacing: '-0.35px', lineHeight: 1.25, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                Crescent Flour Mills
              </Typography>
            </Box>
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <ThemeToggle />
            <IconButton
              onClick={(e) => setAnchorEl(e.currentTarget)}
              sx={{ display: 'flex', alignItems: 'center', gap: 0.5, color: 'text.primary', borderRadius: '8px' }}
            >
              <Typography sx={{ fontSize: '13px', fontWeight: 500 }}>{user?.username}</Typography>
              <KeyboardArrowDownIcon sx={{ fontSize: '18px' }} />
            </IconButton>

            <Menu
              anchorEl={anchorEl}
              open={!!anchorEl}
              onClose={() => setAnchorEl(null)}
              anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
              transformOrigin={{ vertical: 'top', horizontal: 'right' }}
              slotProps={{ paper: { sx: { borderRadius: '8px', border: 1, borderColor: 'divider', mt: 1 } } }}
            >
              <Box sx={{ px: 2, py: 1 }}>
                <Typography sx={{ fontSize: '13px', fontWeight: 600, color: 'text.primary' }}>{user?.username}</Typography>
                <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>{roleLabel}</Typography>
              </Box>
              <Divider />
              <MenuItem onClick={handleLogout} sx={{ fontSize: '13px', color: 'error.main' }}>
                <LogoutIcon sx={{ mr: 1, fontSize: '18px' }} /> Logout
              </MenuItem>
            </Menu>
          </Box>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        sx={{
          display: { xs: 'block', sm: 'none' },
          '& .MuiDrawer-paper': { width: DRAWER_WIDTH, mt: `${APPBAR_HEIGHT}px`, height: `calc(100% - ${APPBAR_HEIGHT}px)` },
        }}
      >
        {drawerContent}
      </Drawer>

      <Drawer
        variant="permanent"
        sx={{
          display: { xs: 'none', sm: 'block' },
          width: DRAWER_WIDTH,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: DRAWER_WIDTH,
            boxSizing: 'border-box',
            mt: `${APPBAR_HEIGHT}px`,
            height: `calc(100% - ${APPBAR_HEIGHT}px)`,
            border: 'none',
            borderRight: 1,
            borderColor: 'divider',
          },
        }}
      >
        {drawerContent}
      </Drawer>

      <Box component="main" sx={{ flex: 1, minWidth: 0, mt: `${APPBAR_HEIGHT}px`, overflow: 'auto' }}>
        <Box sx={{ p: 3, maxWidth: '1600px', mx: 'auto' }}>
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
};

export default Layout;
