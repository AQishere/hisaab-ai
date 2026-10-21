import React from 'react';
import { Box } from '@mui/material';
import logoGreen from '../assets/brand/hisaab-logo-green.png';
import logoCream from '../assets/brand/hisaab-logo-cream.png';

// Icon and wordmark in one image: green lettering on light backgrounds, cream lettering in dark mode.
const RATIO = 1493 / 415;

const BrandLogo: React.FC<{ height?: number }> = ({ height = 32 }) => {
  const size = { height, width: Math.round(height * RATIO), display: 'block' };
  return (
    <Box role="img" aria-label="Hisaab" sx={{ display: 'flex', alignItems: 'center' }}>
      <Box component="img" src={logoGreen} alt="" sx={(theme) => ({ ...size, ...theme.applyStyles('dark', { display: 'none' }) })} />
      <Box component="img" src={logoCream} alt="" sx={(theme) => ({ ...size, display: 'none', ...theme.applyStyles('dark', { display: 'block' }) })} />
    </Box>
  );
};

export default BrandLogo;
