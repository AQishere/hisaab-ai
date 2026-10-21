import React from 'react';
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Typography } from '@mui/material';
import { Close as CloseIcon } from '@mui/icons-material';

interface DetailDialogProps {
  open: boolean;
  title: React.ReactNode;
  onClose: () => void;
  actions?: React.ReactNode;
  maxWidth?: 'sm' | 'md' | 'lg';
  children: React.ReactNode;
}

export const DetailDialog: React.FC<DetailDialogProps> = ({ open, title, onClose, actions, maxWidth = 'md', children }) => (
  <Dialog open={open} onClose={onClose} maxWidth={maxWidth} fullWidth slotProps={{ paper: { sx: { borderRadius: '12px' } } }}>
    <DialogTitle sx={{ fontSize: '18px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 1.5, pr: 7 }}>
      {title}
      <IconButton onClick={onClose} aria-label="Close" sx={{ position: 'absolute', right: 12, top: 12 }}>
        <CloseIcon fontSize="small" />
      </IconButton>
    </DialogTitle>
    <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>{children}</DialogContent>
    <DialogActions sx={{ px: 3, pb: 2.5 }}>
      {actions}
      <Button onClick={onClose} color="inherit">
        Close
      </Button>
    </DialogActions>
  </Dialog>
);

export const InfoGrid: React.FC<{ items: [string, React.ReactNode][] }> = ({ items }) => (
  <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', sm: 'repeat(4, 1fr)' }, gap: 2 }}>
    {items.map(([label, value]) => (
      <Box key={label}>
        <Typography sx={{ fontSize: '11px', fontWeight: 600, color: 'text.secondary', textTransform: 'uppercase', letterSpacing: '0.4px', mb: 0.5 }}>
          {label}
        </Typography>
        <Typography component="div" sx={{ fontSize: '14px', color: 'text.primary', fontWeight: 500 }}>
          {value ?? '-'}
        </Typography>
      </Box>
    ))}
  </Box>
);

export const SectionTitle: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <Typography sx={{ fontSize: '14px', fontWeight: 700, color: 'text.primary', mb: -1.5 }}>{children}</Typography>
);
