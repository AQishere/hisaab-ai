import React, { useState } from 'react';
import { TextField, Typography } from '@mui/material';
import FormDialog from './ui/FormDialog';
import client from '../api/client';
import { useToast } from '../context/ToastContext';
import { getErrorMessage } from '../utils/format';

interface ReasonDialogProps {
  open: boolean;
  title: string;
  submitLabel: string;
  description: string;
  url: string;
  success: string;
  onClose: () => void;
  onDone: () => void;
}

// Close-short, cancel and void actions all require a recorded reason.
const ReasonDialog: React.FC<ReasonDialogProps> = ({ open, title, submitLabel, description, url, success, onClose, onDone }) => {
  const toast = useToast();
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const close = () => {
    setReason('');
    setError('');
    onClose();
  };

  return (
    <FormDialog open={open} title={title} submitLabel={submitLabel} submitting={saving} error={error}
      submitDisabled={!reason.trim() || reason.trim().length > 500} onClose={close}
      onSubmit={async () => {
        setSaving(true);
        setError('');
        try {
          await client.put(url, { reason: reason.trim() });
          toast(success);
          setReason('');
          onDone();
        } catch (err) {
          setError(getErrorMessage(err));
        } finally {
          setSaving(false);
        }
      }}>
      <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>{description}</Typography>
      <TextField required multiline minRows={2} label="Reason" value={reason} onChange={(e) => setReason(e.target.value)}
        helperText={`${reason.trim().length}/500`} error={reason.trim().length > 500} />
    </FormDialog>
  );
};

export default ReasonDialog;
