import React, { useMemo, useState } from 'react';
import { Box, FormControlLabel, InputAdornment, MenuItem, Switch, TextField, Typography } from '@mui/material';
import { Search as SearchIcon } from '@mui/icons-material';
import type { InventoryItem } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { formatDateTime, formatNumber } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import { DetailDialog } from '../components/ui/DetailDialog';
import { StockHistory } from '../components/HistoryTables';
import DataTable from '../components/ui/DataTable';

const Inventory: React.FC = () => {
  const { data, loading, error, reload } = useFetch<InventoryItem[]>('/inventory');
  const [selected, setSelected] = useState<InventoryItem | null>(null);
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('All');
  const [hideEmpty, setHideEmpty] = useState(true);

  const categories = useMemo(() => ['All', ...new Set(data?.map((i) => i.category) ?? [])], [data]);

  const rows = useMemo(() => {
    if (!data) return null;
    const q = search.trim().toLowerCase();
    return data.filter(
      (i) =>
        (!hideEmpty || i.quantityAvailable > 0) &&
        (category === 'All' || i.category === category) &&
        (!q || `${i.productName} ${i.productCode} ${i.lotNumber ?? ''} ${i.warehouseName}`.toLowerCase().includes(q)),
    );
  }, [data, search, category, hideEmpty]);

  const totals = useMemo(() => {
    const byCat = new Map<string, Map<string, number>>();
    for (const i of data ?? []) {
      const units = byCat.get(i.category) ?? new Map<string, number>();
      units.set(i.unit, (units.get(i.unit) ?? 0) + i.quantityAvailable);
      byCat.set(i.category, units);
    }
    return [...byCat.entries()];
  }, [data]);

  return (
    <Box>
      <PageHeader title="Inventory" subtitle="Stock on hand by product, warehouse and lot" />

      {totals.length > 0 && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' }, gap: 2, mb: 3 }}>
          {totals.map(([cat, units]) => (
            <Box key={cat} sx={{ border: 1, borderColor: 'divider', borderRadius: '12px', p: 2, bgcolor: 'background.paper' }}>
              <Typography sx={{ fontSize: '12px', fontWeight: 600, color: 'text.secondary', textTransform: 'uppercase', letterSpacing: '0.4px', mb: 1 }}>
                {cat}
              </Typography>
              {[...units.entries()].map(([unit, qty]) => (
                <Typography key={unit} sx={{ fontSize: '22px', fontWeight: 700, color: 'text.primary', lineHeight: 1.3 }}>
                  {formatNumber(qty)} <Box component="span" sx={{ fontSize: '13px', color: 'text.secondary', fontWeight: 500 }}>{unit}</Box>
                </Typography>
              ))}
            </Box>
          ))}
        </Box>
      )}

      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, alignItems: 'center', mb: 2 }}>
        <TextField
          size="small"
          placeholder="Search product, lot or warehouse"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          sx={{ width: { xs: '100%', sm: 320 } }}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }}
        />
        <TextField select size="small" label="Category" value={category} onChange={(e) => setCategory(e.target.value)} sx={{ width: 180 }}>
          {categories.map((c) => (
            <MenuItem key={c} value={c}>
              {c}
            </MenuItem>
          ))}
        </TextField>
        <FormControlLabel control={<Switch checked={hideEmpty} onChange={(e) => setHideEmpty(e.target.checked)} />} label="Hide empty lots" />
      </Box>

      <DataTable
        rows={rows}
        rowKey={(i) => i.inventoryID}
        onRowClick={setSelected}
        loading={loading}
        error={error}
        onRetry={reload}
        emptyTitle={data?.length ? 'No stock matches these filters' : 'No stock yet'}
        emptyHint={data?.length ? undefined : 'Stock appears here when wheat is received at the gate or a production batch is completed.'}
        columns={[
          {
            key: 'product',
            label: 'Product',
            render: (i) => (
              <>
                <strong>{i.productName}</strong>
                <Typography component="span" sx={{ fontSize: '12px', color: 'text.secondary', ml: 1 }}>{i.productCode}</Typography>
              </>
            ),
          },
          { key: 'form', label: 'Form', render: (i) => i.stockForm === 'Bulk' ? 'Unpacked bulk' : i.unit === 'bag' ? 'Packed bags' : 'Stock' },
          { key: 'cat', label: 'Category', render: (i) => i.category },
          { key: 'wh', label: 'Warehouse', render: (i) => i.warehouseName },
          { key: 'bin', label: 'Bin', render: (i) => i.binCode || '-' },
          { key: 'lot', label: 'Lot', render: (i) => i.lotNumber || '-' },
          { key: 'qty', label: 'Available', align: 'right', render: (i) => <strong>{formatNumber(i.quantityAvailable)} {i.unit}</strong> },
          { key: 'upd', label: 'Updated', render: (i) => formatDateTime(i.lastUpdatedDate) },
        ]}
      />
      {selected && <DetailDialog open maxWidth="lg" title={`Stock history: ${selected.productName} / ${selected.lotNumber || '-'}`} onClose={() => setSelected(null)}>
        <Typography variant="body2">Opening balances preserve stock at the history upgrade; older individual movements are not reconstructed.</Typography>
        <StockHistory inventoryID={selected.inventoryID} />
      </DetailDialog>}
    </Box>
  );
};

export default Inventory;
