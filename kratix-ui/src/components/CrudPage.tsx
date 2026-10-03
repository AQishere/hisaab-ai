import { useMemo, useState } from 'react';
import {
  Box,
  Button,
  FormControlLabel,
  IconButton,
  InputAdornment,
  MenuItem,
  Switch,
  TextField,
  Tooltip,
} from '@mui/material';
import { Add as AddIcon, Delete as DeleteIcon, Edit as EditIcon, Search as SearchIcon } from '@mui/icons-material';
import client from '../api/client';
import { useFetch } from '../hooks/useFetch';
import { useToast } from '../context/ToastContext';
import { getErrorMessage } from '../utils/format';
import PageHeader from './ui/PageHeader';
import DataTable, { type Column } from './ui/DataTable';
import FormDialog from './ui/FormDialog';
import ConfirmDialog from './ui/ConfirmDialog';

export type FormValues = Record<string, string | boolean>;

export interface FieldDef {
  name: string;
  label: string;
  type?: 'text' | 'number' | 'select' | 'switch' | 'textarea';
  options?: string[];
  required?: boolean;
  editOnly?: boolean;
  half?: boolean;
  helper?: string;
}

export interface CrudConfig<T> {
  title: string;
  subtitle: string;
  singular: string;
  endpoint: string;
  getId: (row: T) => number;
  getName: (row: T) => string;
  searchText: (row: T) => string;
  columns: Column<T>[];
  fields: FieldDef[];
  toForm: (row: T | null) => FormValues;
}

function toPayload(fields: FieldDef[], values: FormValues) {
  return Object.fromEntries(
    fields.map((f) => {
      const v = values[f.name];
      if (f.type === 'number') return [f.name, v === '' ? 0 : Number(v)];
      if (f.type === 'switch') return [f.name, !!v];
      return [f.name, String(v ?? '').trim()];
    }),
  );
}

function CrudPage<T>({ config }: { config: CrudConfig<T> }) {
  const { data, loading, error, reload } = useFetch<T[]>(config.endpoint);
  const toast = useToast();
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<T | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [values, setValues] = useState<FormValues>({});
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState<T | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);

  const rows = useMemo(() => {
    if (!data) return null;
    const q = search.trim().toLowerCase();
    return q ? data.filter((r) => config.searchText(r).toLowerCase().includes(q)) : data;
  }, [data, search, config]);

  const openForm = (row: T | null) => {
    setEditing(row);
    setValues(config.toForm(row));
    setFormError('');
    setFormOpen(true);
  };

  const visibleFields = config.fields.filter((f) => !f.editOnly || editing);
  const missingRequired = visibleFields.some((f) => f.required && !String(values[f.name] ?? '').trim());

  const save = async () => {
    setSaving(true);
    setFormError('');
    try {
      const payload = toPayload(visibleFields, values);
      if (editing) {
        await client.put(`${config.endpoint}/${config.getId(editing)}`, payload);
      } else {
        await client.post(config.endpoint, payload);
      }
      toast(`${config.singular} ${editing ? 'updated' : 'added'}`);
      setFormOpen(false);
      reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await client.delete(`${config.endpoint}/${config.getId(deleting)}`);
      toast(`${config.singular} deleted`);
      setDeleting(null);
      reload();
    } catch (err) {
      toast(getErrorMessage(err, `Could not delete this ${config.singular.toLowerCase()}. It may be used by existing orders or stock.`), 'error');
      setDeleting(null);
    } finally {
      setDeleteBusy(false);
    }
  };

  const columns: Column<T>[] = [
    ...config.columns,
    {
      key: '_actions',
      label: '',
      align: 'right',
      width: 96,
      render: (row) => (
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 0.5 }} onClick={(e) => e.stopPropagation()}>
          <Tooltip title="Edit">
            <IconButton size="small" onClick={() => openForm(row)} aria-label={`Edit ${config.getName(row)}`}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Delete">
            <IconButton size="small" onClick={() => setDeleting(row)} aria-label={`Delete ${config.getName(row)}`}>
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      ),
    },
  ];

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={() => openForm(null)}>
      Add {config.singular.toLowerCase()}
    </Button>
  );

  return (
    <Box>
      <PageHeader title={config.title} subtitle={config.subtitle} actions={addButton} />

      <TextField
        size="small"
        placeholder={`Search ${config.title.toLowerCase()}`}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        sx={{ mb: 2, width: { xs: '100%', sm: 320 } }}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" />
              </InputAdornment>
            ),
          },
        }}
      />

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={config.getId}
        loading={loading}
        error={error}
        onRetry={reload}
        onRowClick={(row) => openForm(row)}
        emptyTitle={search ? 'No matches' : `No ${config.title.toLowerCase()} yet`}
        emptyHint={search ? 'Try a different search.' : `Add your first ${config.singular.toLowerCase()} to get started.`}
        emptyAction={search ? undefined : addButton}
      />

      <FormDialog
        open={formOpen}
        title={editing ? `Edit ${config.singular.toLowerCase()}` : `Add ${config.singular.toLowerCase()}`}
        submitLabel={editing ? 'Save changes' : 'Add'}
        submitting={saving}
        submitDisabled={missingRequired}
        error={formError}
        onClose={() => setFormOpen(false)}
        onSubmit={save}
      >
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
          {visibleFields.map((f) => {
            const span = f.half ? undefined : { sm: '1 / -1' };
            if (f.type === 'switch') {
              return (
                <FormControlLabel
                  key={f.name}
                  sx={{ gridColumn: span }}
                  control={
                    <Switch checked={!!values[f.name]} onChange={(e) => setValues((v) => ({ ...v, [f.name]: e.target.checked }))} />
                  }
                  label={f.label}
                />
              );
            }
            return (
              <TextField
                key={f.name}
                label={f.label}
                required={f.required}
                select={f.type === 'select'}
                type={f.type === 'number' ? 'number' : 'text'}
                multiline={f.type === 'textarea'}
                minRows={f.type === 'textarea' ? 2 : undefined}
                value={values[f.name] ?? ''}
                helperText={f.helper}
                onChange={(e) => setValues((v) => ({ ...v, [f.name]: e.target.value }))}
                sx={{ gridColumn: span }}
                slotProps={f.type === 'number' ? { htmlInput: { min: 0, step: 'any' } } : undefined}
              >
                {f.options?.map((o) => (
                  <MenuItem key={o} value={o}>
                    {o}
                  </MenuItem>
                ))}
              </TextField>
            );
          })}
        </Box>
      </FormDialog>

      <ConfirmDialog
        open={!!deleting}
        title={`Delete ${config.singular.toLowerCase()}?`}
        message={deleting ? `"${config.getName(deleting)}" will be permanently deleted.` : ''}
        confirmLabel="Delete"
        busy={deleteBusy}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      />
    </Box>
  );
}

export default CrudPage;
