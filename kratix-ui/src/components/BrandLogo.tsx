import React from 'react';
import { Box } from '@mui/material';
import markDark from '../assets/brand/kratix-mark-dark-ink.png';
import markWhite from '../assets/brand/kratix-mark-white-ink.png';
import wordDark from '../assets/brand/kratix-wordmark-dark-ink.png';
import wordWhite from '../assets/brand/kratix-wordmark-white-ink.png';

const MARK_RATIO = 238 / 200;
const WORD_RATIO = 561 / 87;

const ThemedImg: React.FC<{ light: string; dark: string; height: number; ratio: number }> = ({ light, dark, height, ratio }) => {
  const size = { height, width: Math.round(height * ratio), display: 'block' };
  return (
    <>
      <Box component="img" src={light} alt="" sx={(theme) => ({ ...size, ...theme.applyStyles('dark', { display: 'none' }) })} />
      <Box component="img" src={dark} alt="" sx={(theme) => ({ ...size, display: 'none', ...theme.applyStyles('dark', { display: 'block' }) })} />
    </>
  );
};

const BrandLogo: React.FC<{ height?: number }> = ({ height = 32 }) => (
  <Box role="img" aria-label="Kratix" sx={{ display: 'flex', alignItems: 'center', gap: `${Math.round(height * 0.3)}px` }}>
    <ThemedImg light={markDark} dark={markWhite} height={height} ratio={MARK_RATIO} />
    <ThemedImg light={wordDark} dark={wordWhite} height={Math.round(height * 0.5)} ratio={WORD_RATIO} />
  </Box>
);

export default BrandLogo;
