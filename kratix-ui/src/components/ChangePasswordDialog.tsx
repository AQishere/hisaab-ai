import React, { useState } from 'react';
import { Alert, TextField } from '@mui/material';
import FormDialog from './ui/FormDialog';
import client from '../api/client';
import { session } from '../auth/session';
import { useAuth } from '../hooks/useAuth';
import { useToast } from '../hooks/useToast';
import { getErrorMessage } from '../utils/format';

// Changing the password signs out every other session; this one continues with the fresh token.
const ChangePasswordDialog: React.FC<{ open: boolean; onClose: () => void }> = ({ open, onClose }) => {
  const { login } = useAuth();
  const toast = useToast();
  const [form, setForm] = useState({ current: '', next: '', confirm: '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const close = () => {
    setForm({ current: '', next: '', confirm: '' });
    setError('');
    onClose();
  };

  const mismatch = form.confirm !== '' && form.next !== form.confirm;

  return (
    <FormDialog
      open={open}
      title="Change my password"
      submitLabel="Change password"
      submitting={saving}
      submitDisabled={!form.current || form.next.length < 8 || form.next !== form.confirm}
      error={error}
      onClose={close}
      onSubmit={async () => {
        setSaving(true);
        setError('');
        try {
          const remember = session.isRemembered();
          const res = await client.post<{ token: string; user: { id: string; username: string; email: string; role: string } }>(
            '/auth/change-password', { currentPassword: form.current, newPassword: form.next, rememberMe: remember });
          login(res.data.token, res.data.user, remember);
          toast('Password changed. Other devices have been signed out.');
          close();
        } catch (err) {
          setError(getErrorMessage(err));
        } finally {
          setSaving(false);
        }
      }}
    >
      <Alert severity="info">Any other browser or device signed in to your account will be signed out.</Alert>
      <TextField required label="Current password" type="password" autoComplete="current-password" value={form.current}
        onChange={(e) => setForm({ ...form, current: e.target.value })} />
      <TextField required label="New password" type="password" autoComplete="new-password" value={form.next}
        onChange={(e) => setForm({ ...form, next: e.target.value })} helperText="At least 8 characters, not your username" />
      <TextField required label="Repeat new password" type="password" autoComplete="new-password" value={form.confirm}
        onChange={(e) => setForm({ ...form, confirm: e.target.value })} error={mismatch} helperText={mismatch ? 'Passwords do not match' : ' '} />
    </FormDialog>
  );
};

export default ChangePasswordDialog;
