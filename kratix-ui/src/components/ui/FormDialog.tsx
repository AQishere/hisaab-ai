import React from 'react';
import { Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';

interface FormDialogProps {
  open: boolean;
  title: string;
  submitLabel: string;
  submitting?: boolean;
  submitDisabled?: boolean;
  error?: string;
  maxWidth?: 'xs' | 'sm' | 'md' | 'lg';
  onClose: () => void;
  onSubmit: () => void;
  children: React.ReactNode;
}

const FormDialog: React.FC<FormDialogProps> = ({
  open,
  title,
  submitLabel,
  submitting,
  submitDisabled,
  error,
  maxWidth = 'sm',
  onClose,
  onSubmit,
  children,
}) => (
  <Dialog
    open={open}
    onClose={submitting ? undefined : onClose}
    maxWidth={maxWidth}
    fullWidth
    slotProps={{
      paper: {
        component: 'form',
        onSubmit: (e: React.FormEvent) => {
          e.preventDefault();
          if (!submitting && !submitDisabled) onSubmit();
        },
        sx: { borderRadius: '12px' },
      } as object,
    }}
  >
    <DialogTitle sx={{ fontSize: '18px', fontWeight: 700 }}>{title}</DialogTitle>
    <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '8px !important' }}>
      {error && <Alert severity="error">{error}</Alert>}
      {children}
    </DialogContent>
    <DialogActions sx={{ px: 3, pb: 2.5 }}>
      <Button onClick={onClose} disabled={submitting} color="inherit">
        Cancel
      </Button>
      <Button type="submit" variant="contained" disabled={submitting || submitDisabled} sx={{ minWidth: 110 }}>
        {submitting ? <CircularProgress size={18} color="inherit" /> : submitLabel}
      </Button>
    </DialogActions>
  </Dialog>
);

export default FormDialog;
