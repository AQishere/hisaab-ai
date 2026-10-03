import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from '../context/AuthContext';
import { canAccess, homePath } from '../auth/permissions';

interface ProtectedRouteProps {
  children: React.ReactNode;
  // Page whose access rule applies, for routes with parameters (e.g. /invoices/5/print uses /invoices).
  accessPath?: string;
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children, accessPath }) => {
  const { user, isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100dvh', width: '100%' }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!canAccess(user?.role, accessPath ?? location.pathname)) {
    const home = homePath(user?.role);
    return <Navigate to={home === location.pathname ? '/login' : home} replace />;
  }

  return <>{children}</>;
};

export default ProtectedRoute;
