import React, { useState } from 'react';
import { Box, Button, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { ExpandLess as ExpandLessIcon, ExpandMore as ExpandMoreIcon } from '@mui/icons-material';
import type { AgentTable } from '../../api/agent';
import { formatDate, formatNumber } from '../../utils/format';

// The agent sends the full rows a tool read; the model only summarised them. Shown as a plain table under the answer.
const TITLES: Record<string, string> = {
  openInvoices: 'Unpaid invoices',
  invoices: 'Invoices',
  stock: 'Stock',
  rows: 'Stock movements',
  batches: 'Milling batches',
  suppliers: 'Suppliers',
  purchaseOrders: 'Purchase orders',
  salesOrders: 'Sales orders',
  customers: 'Customers',
};
const HEADERS: Record<string, string> = {
  po: 'PO', so: 'SO', qcApproved: 'QC approved', qcRejected: 'QC rejected', passRatePercent: 'Pass rate %',
  avgMoisturePercent: 'Moisture %', avgImpuritiesPercent: 'Impurities %', totalNetKg: 'Net kg', totalAmountPKR: 'Total (PKR)',
  creditLimitPKR: 'Credit limit (PKR)', outstandingBalancePKR: 'Outstanding (PKR)', inputKg: 'Input kg', outputKg: 'Output kg',
  wasteKg: 'Waste kg', yieldPercent: 'Yield %',
};
const COLLAPSED_ROWS = 8;

const header = (key: string) =>
  HEADERS[key] ?? key.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase());

const isDate = (value: string) => /^\d{4}-\d{2}-\d{2}(T|$)/.test(value);

// Order lines and lot lists arrive as arrays; a short text keeps the table one line per row.
const cell = (value: unknown): string => {
  if (value == null || value === '') return '-';
  if (typeof value === 'number') return formatNumber(value);
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'string') return isDate(value) ? formatDate(value.slice(0, 10)) : value;
  if (Array.isArray(value)) {
    return value.map((item) => {
      if (item && typeof item === 'object') {
        const line = item as Record<string, unknown>;
        if ('product' in line) {
          const done = line.received ?? line.dispatched;
          return `${line.product}: ${formatNumber(Number(line.ordered))}${done != null ? ` / ${formatNumber(Number(done))}` : ''} ${line.unit ?? ''}`.trim();
        }
        return Object.values(line).join(' ');
      }
      return String(item);
    }).join('; ') || '-';
  }
  return String(value);
};

const AnswerTable: React.FC<{ table: AgentTable }> = ({ table }) => {
  const [expanded, setExpanded] = useState(false);
  if (!table.rows.length) return null;
  const keys = Object.keys(table.rows[0]);
  const numeric = new Set(keys.filter((k) => table.rows.every((r) => r[k] == null || typeof r[k] === 'number')));
  const rows = expanded ? table.rows : table.rows.slice(0, COLLAPSED_ROWS);

  return (
    <Paper variant="outlined" sx={{ mt: 1.5, borderRadius: '8px', overflow: 'hidden' }}>
      <Box sx={{ px: 1.5, py: 1, display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: 1, borderColor: 'divider' }}>
        <Typography sx={{ fontSize: '12px', fontWeight: 700, color: 'text.secondary' }}>
          {TITLES[table.title] ?? header(table.title)} ({table.count})
        </Typography>
        {table.rows.length > COLLAPSED_ROWS && (
          <Button size="small" onClick={() => setExpanded(!expanded)} endIcon={expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}>
            {expanded ? 'Show less' : `Show all ${table.rows.length}`}
          </Button>
        )}
      </Box>
      <TableContainer sx={{ maxHeight: 360 }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              {keys.map((k) => (
                <TableCell key={k} align={numeric.has(k) ? 'right' : 'left'} sx={{ fontSize: '12px', fontWeight: 700, whiteSpace: 'nowrap' }}>
                  {header(k)}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row, i) => (
              <TableRow key={i} hover>
                {keys.map((k) => (
                  <TableCell key={k} align={numeric.has(k) ? 'right' : 'left'} sx={{ fontSize: '12px', whiteSpace: Array.isArray(row[k]) ? 'normal' : 'nowrap' }}>
                    {cell(row[k])}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
};

export default AnswerTable;
