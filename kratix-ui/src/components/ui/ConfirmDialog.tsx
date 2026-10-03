import React from 'react';
import { Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from '@mui/material';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  busy?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

const ConfirmDialog: React.FC<ConfirmDialogProps> = ({ open, title, message, confirmLabel, busy, onConfirm, onClose }) => (
  <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { borderRadius: '12px' } } }}>
    <DialogTitle sx={{ fontSize: '18px', fontWeight: 700 }}>{title}</DialogTitle>
    <DialogContent>
      <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>{message}</Typography>
    </DialogContent>
    <DialogActions sx={{ px: 3, pb: 2.5 }}>
      <Button onClick={onClose} disabled={busy} color="inherit">
        Cancel
      </Button>
      <Button onClick={onConfirm} variant="contained" color="error" disabled={busy} sx={{ minWidth: 100 }}>
        {busy ? <CircularProgress size={18} color="inherit" /> : confirmLabel}
      </Button>
    </DialogActions>
  </Dialog>
);

export default ConfirmDialog;
