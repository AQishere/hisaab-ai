import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { agentApi, type AgentStats } from '../../api/agent';
import { formatDateTime } from '../../utils/format';

const seconds = (ms: number | null) => (ms == null ? '-' : `${(ms / 1000).toFixed(2)} s`);

const Tile: React.FC<{ label: string; value: string }> = ({ label, value }) => (
  <Paper variant="outlined" sx={{ p: 1.5, borderRadius: '8px' }}>
    <Typography sx={{ fontSize: '11px', fontWeight: 700, letterSpacing: '0.5px', textTransform: 'uppercase', color: 'text.secondary' }}>{label}</Typography>
    <Typography sx={{ fontSize: '20px', fontWeight: 700, mt: 0.25 }}>{value}</Typography>
  </Paper>
);

const Heading: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <Typography sx={{ fontSize: '13px', fontWeight: 700, mt: 2.5, mb: 1 }}>{children}</Typography>
);

// Owner and Admin: how the assistant is used and where it gets answers wrong (thumbs down).
const UsageDialog: React.FC<{ open: boolean; onClose: () => void }> = ({ open, onClose }) => {
  const [days, setDays] = useState(7);
  const [result, setResult] = useState<{ days: number; stats: AgentStats | null; error: string } | null>(null);

  useEffect(() => {
    if (!open) return;
    let live = true;
    agentApi.stats(days).then(
      (s) => live && setResult({ days, stats: s, error: '' }),
      (err) => live && setResult({ days, stats: null, error: err instanceof Error ? err.message : 'Usage could not be loaded.' }),
    );
    return () => { live = false; };
  }, [open, days]);

  // Never show another period's numbers while the new ones load.
  const current = result?.days === days ? result : null;
  const stats = current?.stats ?? null;
  const error = current?.error ?? '';

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
        Assistant usage
        <ToggleButtonGroup exclusive size="small" value={days} onChange={(_, v) => v && setDays(v)}>
          <ToggleButton value={1}>Today</ToggleButton>
          <ToggleButton value={7}>7 days</ToggleButton>
          <ToggleButton value={30}>30 days</ToggleButton>
        </ToggleButtonGroup>
      </DialogTitle>
      <DialogContent dividers>
        {error && <Alert severity="error">{error}</Alert>}
        {!stats && !error && <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>}
        {stats && (
          <>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(5, 1fr)' }, gap: 1.5 }}>
              <Tile label="Questions" value={String(stats.questions)} />
              <Tile label="Failed" value={`${stats.failedPercent}%`} />
              <Tile label="First words (typical)" value={seconds(stats.latencyMs.firstWords.p50)} />
              <Tile label="First words (slowest 5%)" value={seconds(stats.latencyMs.firstWords.p95)} />
              <Tile label="Thumbs up / down" value={`${stats.feedback.up} / ${stats.feedback.down}`} />
            </Box>

            <Heading>AI providers</Heading>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' }, gap: 1.5 }}>
              <Tile
                label="Answered by"
                value={Object.entries(stats.llm.byProvider).map(([name, n]) => `${name} ${n}`).join(' · ') || '-'}
              />
              <Tile label="Needed a fallback" value={`${stats.llm.fallbackPercent}%`} />
              <Tile label="Tokens in / out" value={`${stats.llm.promptTokens.toLocaleString()} / ${stats.llm.completionTokens.toLocaleString()}`} />
              <Tile label="Estimated cost" value={`$${stats.llm.costUsd.toFixed(2)}`} />
            </Box>

            <Heading>Thumbs down</Heading>
            {stats.thumbsDown.length === 0 ? (
              <Typography sx={{ fontSize: '13px', color: 'text.secondary' }}>No answers were marked wrong.</Typography>
            ) : stats.thumbsDown.map((t) => (
              <Paper key={t.messageId} variant="outlined" sx={{ p: 1.5, mb: 1, borderRadius: '8px' }}>
                <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>{t.username ?? 'Unknown'} · {formatDateTime(t.createdAt)}</Typography>
                <Typography sx={{ fontSize: '13px', fontWeight: 600, mt: 0.5 }}>{t.question ?? '(question not logged)'}</Typography>
                <Typography sx={{ fontSize: '13px', mt: 0.5 }}>{t.answer}</Typography>
                {t.comment && <Typography sx={{ fontSize: '13px', mt: 0.5, color: 'error.main' }}>"{t.comment}"</Typography>}
              </Paper>
            ))}

            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 3 }}>
              <Box>
                <Heading>By user</Heading>
                <Table size="small">
                  <TableHead><TableRow><TableCell>User</TableCell><TableCell>Role</TableCell><TableCell align="right">Questions</TableCell></TableRow></TableHead>
                  <TableBody>
                    {stats.perUser.map((u) => (
                      <TableRow key={u.username}><TableCell>{u.username}</TableCell><TableCell>{u.role}</TableCell><TableCell align="right">{u.questions}</TableCell></TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
              <Box>
                <Heading>What was looked up</Heading>
                <Table size="small">
                  <TableHead><TableRow><TableCell>Tool</TableCell><TableCell align="right">Uses</TableCell><TableCell align="right">Refused or failed</TableCell></TableRow></TableHead>
                  <TableBody>
                    {stats.tools.map((t) => (
                      <TableRow key={t.name}><TableCell>{t.name.replace(/_/g, ' ')}</TableCell><TableCell align="right">{t.calls}</TableCell><TableCell align="right">{t.errors}</TableCell></TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            </Box>
          </>
        )}
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Close</Button></DialogActions>
    </Dialog>
  );
};

export default UsageDialog;
