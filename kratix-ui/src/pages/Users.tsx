import React, { useState } from 'react';
import { Alert, Box, Button, FormControlLabel, MenuItem, Switch, TextField } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import client from '../api/client';
import type { UserAccount } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { useToast } from '../context/ToastContext';
import { useAuth } from '../context/AuthContext';
import { formatDate, getErrorMessage } from '../utils/format';
import { ROLE_LABELS, type Role } from '../auth/permissions';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import FormDialog from '../components/ui/FormDialog';
import StatusChip from '../components/ui/StatusChip';

const ROLES: Role[] = ['Admin', 'Owner', 'Operations', 'Sales', 'Worker'];

const ROLE_HINTS: Record<Role, string> = {
  Admin: 'Everything, including users and the dashboard',
  Owner: 'Everything, including users and the dashboard',
  Operations: 'Purchasing, receiving, production, inventory, master data',
  Sales: 'Customers, sales orders, invoices, payments',
  Worker: 'Receiving, production, inventory',
};

const RoleField: React.FC<{ value: string; onChange: (role: string) => void; disabled?: boolean; helperText?: string }> = ({ value, onChange, disabled, helperText }) => (
  <TextField select required label="Role" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}
    helperText={helperText ?? (value ? ROLE_HINTS[value as Role] : undefined)}>
    {ROLES.map((r) => <MenuItem key={r} value={r}>{ROLE_LABELS[r]}</MenuItem>)}
  </TextField>
);

// Accounts are deactivated rather than deleted, because their names appear in the business history.
const Users: React.FC = () => {
  const users = useFetch<UserAccount[]>('/users');
  const toast = useToast();
  const { user: me } = useAuth();

  const [createOpen, setCreateOpen] = useState(false);
  const [draft, setDraft] = useState({ username: '', email: '', role: 'Worker', password: '' });
  const [editing, setEditing] = useState<UserAccount | null>(null);
  const [edit, setEdit] = useState({ email: '', role: '', isActive: true });
  const [resetting, setResetting] = useState<UserAccount | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);

  const isMe = (u: UserAccount) => String(u.userID) === me?.id;

  const run = async (fn: () => Promise<unknown>, success: string, done: () => void) => {
    setSaving(true);
    setFormError('');
    try {
      await fn();
      toast(success);
      done();
      users.reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const openCreate = () => {
    setDraft({ username: '', email: '', role: 'Worker', password: '' });
    setFormError('');
    setCreateOpen(true);
  };

  const openEdit = (u: UserAccount) => {
    setEditing(u);
    setEdit({ email: u.email ?? '', role: u.role, isActive: u.isActive });
    setFormError('');
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
      Add user
    </Button>
  );

  return (
    <Box>
      <PageHeader title="Users" subtitle="Who can sign in and what each account can do" actions={addButton} />

      <DataTable
        rows={users.data}
        rowKey={(u) => u.userID}
        loading={users.loading}
        error={users.error}
        onRetry={users.reload}
        onRowClick={openEdit}
        emptyTitle="No users yet"
        columns={[
          { key: 'u', label: 'Username', render: (u) => <strong>{u.username}{isMe(u) ? ' (you)' : ''}</strong> },
          { key: 'e', label: 'Email', render: (u) => u.email || '-' },
          { key: 'r', label: 'Role', render: (u) => ROLE_LABELS[u.role as Role] ?? u.role },
          { key: 's', label: 'Status', render: (u) => <StatusChip status={u.isActive ? 'Active' : 'Inactive'} /> },
          { key: 'c', label: 'Created', render: (u) => formatDate(u.createdDate) },
        ]}
      />

      <FormDialog
        open={createOpen}
        title="Add user"
        submitLabel="Create user"
        submitting={saving}
        submitDisabled={draft.username.trim().length < 3 || draft.password.length < 8 || !draft.role}
        error={formError}
        onClose={() => setCreateOpen(false)}
        onSubmit={() => run(
          () => client.post('/users', { ...draft, username: draft.username.trim().toLowerCase(), email: draft.email.trim() || null }),
          `${draft.username.trim().toLowerCase()} can now sign in`,
          () => setCreateOpen(false),
        )}
      >
        <TextField required label="Username" value={draft.username} onChange={(e) => setDraft({ ...draft, username: e.target.value })}
          helperText="3-50 characters: lowercase letters, digits, dot, dash or underscore" />
        <TextField label="Email" type="email" value={draft.email} onChange={(e) => setDraft({ ...draft, email: e.target.value })} />
        <RoleField value={draft.role} onChange={(role) => setDraft({ ...draft, role })} />
        <TextField required label="Temporary password" type="password" autoComplete="new-password" value={draft.password}
          onChange={(e) => setDraft({ ...draft, password: e.target.value })}
          helperText="At least 8 characters. Ask the user to change it after their first sign-in." />
      </FormDialog>

      <FormDialog
        open={!!editing}
        title={editing ? `Edit ${editing.username}` : ''}
        submitLabel="Save changes"
        submitting={saving}
        error={formError}
        onClose={() => setEditing(null)}
        onSubmit={() => editing && run(
          () => client.put(`/users/${editing.userID}`, { email: edit.email.trim() || null, role: edit.role, isActive: edit.isActive }),
          `${editing.username} updated`,
          () => setEditing(null),
        )}
      >
        {editing && isMe(editing) && <Alert severity="info">You cannot change your own role or deactivate your own account.</Alert>}
        <TextField label="Email" type="email" value={edit.email} onChange={(e) => setEdit({ ...edit, email: e.target.value })} />
        <RoleField value={edit.role} disabled={!!editing && isMe(editing)} onChange={(role) => setEdit({ ...edit, role })}
          helperText={editing && edit.role !== editing.role ? 'The user will be signed out and must sign in again.' : undefined} />
        <FormControlLabel
          control={<Switch checked={edit.isActive} disabled={!!editing && isMe(editing)} onChange={(e) => setEdit({ ...edit, isActive: e.target.checked })} />}
          label={edit.isActive ? 'Active: can sign in' : 'Inactive: signed out and blocked'}
        />
        <Box>
          <Button size="small" color="inherit" onClick={() => { setResetting(editing); setNewPassword(''); setFormError(''); setEditing(null); }}>
            Reset password
          </Button>
        </Box>
      </FormDialog>

      <FormDialog
        open={!!resetting}
        title={resetting ? `Reset password for ${resetting.username}` : ''}
        submitLabel="Set password"
        submitting={saving}
        submitDisabled={newPassword.length < 8}
        error={formError}
        onClose={() => setResetting(null)}
        onSubmit={() => resetting && run(
          () => client.put(`/users/${resetting.userID}/password`, { newPassword }),
          `Password reset for ${resetting.username}`,
          () => setResetting(null),
        )}
      >
        <Alert severity="info">The user is signed out everywhere and must sign in with the new password.</Alert>
        <TextField required autoFocus label="New password" type="password" autoComplete="new-password" value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)} helperText="At least 8 characters" />
      </FormDialog>
    </Box>
  );
};

export default Users;
