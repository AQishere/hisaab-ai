import React, { useState } from 'react';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
} from '@mui/material';
import { agentApi, type AgentReport } from '../../api/agent';
import { canSeeOperationsReports, canSeeSalesReports } from '../../auth/permissions';
import { todayISO } from '../../utils/format';

const TYPES = [
  { value: 'sales', label: 'Sales', sales: true },
  { value: 'receivables', label: 'Receivables (as of today)', sales: true },
  { value: 'stock', label: 'Stock', sales: false },
  { value: 'production', label: 'Production', sales: false },
];
const PERIODS = [
  { value: 'this_month', label: 'This month' },
  { value: 'last_month', label: 'Last month' },
  { value: 'this_week', label: 'This week' },
  { value: 'last_week', label: 'Last week' },
  { value: 'this_year', label: 'This year' },
  { value: 'custom', label: 'Choose dates' },
];

interface Props {
  open: boolean;
  role: string | undefined;
  onClose: () => void;
  onCreated: (report: AgentReport) => void;
}

// Same reports the assistant makes from chat, for people who prefer a button. Roles follow the ERP Reports page.
const ReportDialog: React.FC<Props> = ({ open, role, onClose, onCreated }) => {
  const types = TYPES.filter((t) => (t.sales ? canSeeSalesReports(role) : canSeeOperationsReports(role)));
  const [type, setType] = useState(types[0]?.value ?? 'sales');
  const [format, setFormat] = useState<'xlsx' | 'pptx'>('xlsx');
  const [period, setPeriod] = useState('this_month');
  const [from, setFrom] = useState(`${todayISO().slice(0, 8)}01`);
  const [to, setTo] = useState(todayISO());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const create = async () => {
    setBusy(true);
    setError('');
    try {
      const report = await agentApi.createReport(
        period === 'custom' ? { type, format, from, to } : { type, format, period },
      );
      await agentApi.download(report.id, report.fileName);
      onCreated(report);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'The report could not be made.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle sx={{ fontWeight: 700 }}>Make a report</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField select label="Report" value={type} onChange={(e) => setType(e.target.value)} fullWidth>
            {types.map((t) => <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>)}
          </TextField>
          <ToggleButtonGroup exclusive fullWidth size="small" value={format} onChange={(_, v) => v && setFormat(v)}>
            <ToggleButton value="xlsx">Excel</ToggleButton>
            <ToggleButton value="pptx">PowerPoint</ToggleButton>
          </ToggleButtonGroup>
          {type !== 'receivables' && (
            <TextField select label="Period" value={period} onChange={(e) => setPeriod(e.target.value)} fullWidth>
              {PERIODS.map((p) => <MenuItem key={p.value} value={p.value}>{p.label}</MenuItem>)}
            </TextField>
          )}
          {type !== 'receivables' && period === 'custom' && (
            <Stack direction="row" spacing={1.5}>
              <TextField type="date" label="From" value={from} onChange={(e) => setFrom(e.target.value)} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
              <TextField type="date" label="To" value={to} onChange={(e) => setTo(e.target.value)} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
            </Stack>
          )}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button variant="contained" onClick={create} disabled={busy || !types.length}>
          {busy ? 'Making...' : 'Make and download'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default ReportDialog;
