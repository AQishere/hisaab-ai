import React from 'react';
import { Box } from '@mui/material';

type Tone = 'neutral' | 'info' | 'warning' | 'success' | 'error';

const TONE_BY_STATUS: Record<string, Tone> = {
  Draft: 'neutral',
  Sent: 'info',
  Received: 'success',
  Cancelled: 'error',
  Closed: 'neutral',
  Voided: 'neutral',
  GateEntry: 'neutral',
  Weighed: 'info',
  QCApproved: 'warning',
  Rejected: 'error',
  Planned: 'neutral',
  InProgress: 'info',
  Completed: 'success',
  OnHold: 'warning',
  Quotation: 'neutral',
  Confirmed: 'info',
  NotDispatched: 'neutral',
  PartiallyDispatched: 'warning',
  Dispatched: 'success',
  Delivered: 'success',
  Unpaid: 'error',
  PartiallyPaid: 'warning',
  Paid: 'success',
  Active: 'success',
  Inactive: 'neutral',
};

const LABELS: Record<string, string> = {
  GateEntry: 'Gate entry',
  QCApproved: 'QC approved',
  InProgress: 'In progress',
  OnHold: 'On hold',
  NotDispatched: 'Not dispatched',
  PartiallyDispatched: 'Partly dispatched',
  PartiallyPaid: 'Partly paid',
  Closed: 'Closed short',
};

const TONE_COLOR: Record<Tone, string> = {
  neutral: 'var(--tone-neutral)',
  info: 'var(--tone-accent)',
  warning: 'var(--tone-warning)',
  success: 'var(--tone-success)',
  error: 'var(--tone-error)',
};

const StatusChip: React.FC<{ status: string }> = ({ status }) => {
  const color = TONE_COLOR[TONE_BY_STATUS[status] ?? 'neutral'];
  return (
    <Box
      component="span"
      sx={{
        display: 'inline-block',
        px: 1,
        py: '2px',
        borderRadius: '6px',
        fontSize: '12px',
        fontWeight: 600,
        whiteSpace: 'nowrap',
        color,
        bgcolor: `color-mix(in srgb, ${color} 14%, transparent)`,
      }}
    >
      {LABELS[status] ?? status}
    </Box>
  );
};

export default StatusChip;
