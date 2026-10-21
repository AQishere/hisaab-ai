import React from 'react';
import { Box, Typography } from '@mui/material';

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
}

const PageHeader: React.FC<PageHeaderProps> = ({ title, subtitle, actions }) => (
  <Box
    sx={{
      display: 'flex',
      flexDirection: { xs: 'column', sm: 'row' },
      alignItems: { xs: 'stretch', sm: 'flex-end' },
      justifyContent: 'space-between',
      gap: 2,
      mb: 3,
    }}
  >
    <Box>
      <Typography sx={{ fontSize: '28px', fontWeight: 700, letterSpacing: '-0.5px', color: 'text.primary', mb: 0.5 }}>
        {title}
      </Typography>
      {subtitle && <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>{subtitle}</Typography>}
    </Box>
    {actions && <Box sx={{ display: 'flex', gap: 1, flexShrink: 0 }}>{actions}</Box>}
  </Box>
);

export default PageHeader;
