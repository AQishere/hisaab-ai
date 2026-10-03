import React from 'react';
import {
  Alert,
  Box,
  Button,
  Paper,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';

export interface Column<T> {
  key: string;
  label: string;
  render: (row: T) => React.ReactNode;
  align?: 'left' | 'right' | 'center';
  width?: number | string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[] | null;
  rowKey: (row: T) => string | number;
  loading?: boolean;
  error?: string;
  onRetry?: () => void;
  onRowClick?: (row: T) => void;
  emptyTitle: string;
  emptyHint?: string;
  emptyAction?: React.ReactNode;
}

function DataTable<T>({
  columns,
  rows,
  rowKey,
  loading,
  error,
  onRetry,
  onRowClick,
  emptyTitle,
  emptyHint,
  emptyAction,
}: DataTableProps<T>) {
  if (error) {
    return (
      <Alert
        severity="error"
        action={onRetry && <Button color="inherit" size="small" onClick={onRetry}>Retry</Button>}
      >
        {error}
      </Alert>
    );
  }

  const showSkeleton = loading && !rows;
  const isEmpty = !loading && rows && rows.length === 0;

  return (
    <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: '12px', borderColor: 'divider' }}>
      <Table size="small" sx={{ minWidth: 640 }}>
        <TableHead>
          <TableRow>
            {columns.map((col) => (
              <TableCell
                key={col.key}
                align={col.align}
                sx={{
                  width: col.width,
                  fontSize: '12px',
                  fontWeight: 600,
                  color: 'text.secondary',
                  textTransform: 'uppercase',
                  letterSpacing: '0.4px',
                  py: 1.5,
                  bgcolor: 'action.hover',
                  whiteSpace: 'nowrap',
                }}
              >
                {col.label}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {showSkeleton &&
            Array.from({ length: 4 }).map((_, i) => (
              <TableRow key={i}>
                {columns.map((col) => (
                  <TableCell key={col.key} sx={{ py: 1.75 }}>
                    <Skeleton variant="text" width="70%" />
                  </TableCell>
                ))}
              </TableRow>
            ))}

          {isEmpty && (
            <TableRow>
              <TableCell colSpan={columns.length} sx={{ border: 0 }}>
                <Box sx={{ textAlign: 'center', py: 6 }}>
                  <Typography sx={{ fontSize: '15px', fontWeight: 600, color: 'text.primary', mb: 0.5 }}>{emptyTitle}</Typography>
                  {emptyHint && (
                    <Typography sx={{ fontSize: '13px', color: 'text.secondary', mb: emptyAction ? 2 : 0 }}>{emptyHint}</Typography>
                  )}
                  {emptyAction}
                </Box>
              </TableCell>
            </TableRow>
          )}

          {rows?.map((row) => (
            <TableRow
              key={rowKey(row)}
              hover
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              sx={{ cursor: onRowClick ? 'pointer' : 'default', '&:last-child td': { border: 0 } }}
            >
              {columns.map((col) => (
                <TableCell key={col.key} align={col.align} sx={{ fontSize: '13px', py: 1.5, color: 'text.primary' }}>
                  {col.render(row)}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

export default DataTable;
