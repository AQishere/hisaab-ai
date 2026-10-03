import React, { useState } from 'react';
import { Alert, Box, Button, MenuItem, TextField, Typography } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import client from '../api/client';
import type { InventoryItem, Product, ProductionBatch, Warehouse } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { useToast } from '../context/ToastContext';
import { formatDate, formatNumber, getErrorMessage, todayISO } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import FormDialog from '../components/ui/FormDialog';
import StatusChip from '../components/ui/StatusChip';
import { DetailDialog, InfoGrid, SectionTitle } from '../components/ui/DetailDialog';

type ActionKind = 'input' | 'output' | 'complete';

const yieldOf = (b: ProductionBatch) => (b.totalInputQuantity > 0 ? (b.totalOutputQuantity / b.totalInputQuantity) * 100 : 0);

const Production: React.FC = () => {
  const batches = useFetch<ProductionBatch[]>('/productionbatches');
  const inventory = useFetch<InventoryItem[]>('/inventory?inStockOnly=true');
  const products = useFetch<Product[]>('/products');
  const warehouses = useFetch<Warehouse[]>('/warehouses');
  const toast = useToast();

  const [createOpen, setCreateOpen] = useState(false);
  const [draft, setDraft] = useState({ productionDate: todayISO(), shiftNumber: '1', operatorName: '' });
  const [selected, setSelected] = useState<ProductionBatch | null>(null);
  const [action, setAction] = useState<ActionKind | null>(null);
  const [input, setInput] = useState({ inventoryID: '', quantity: '' });
  const [output, setOutput] = useState({ productID: '', quantity: '', materialType: 'Output' });
  const [finishWarehouse, setFinishWarehouse] = useState('');
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);

  const rawLots = inventory.data?.filter((i) => i.category === 'Raw Material') ?? [];
  const chosenLot = rawLots.find((l) => String(l.inventoryID) === input.inventoryID);
  const outputProducts = products.data?.filter((p) => p.category !== 'Raw Material' && p.category !== 'Packaging') ?? [];

  const refreshSelected = async (id: number) => {
    const res = await client.get<ProductionBatch>(`/productionbatches/${id}`);
    setSelected(res.data);
  };

  const run = async (fn: () => Promise<unknown>, success: string, after?: () => Promise<void> | void) => {
    setSaving(true);
    setFormError('');
    try {
      await fn();
      toast(success);
      await after?.();
      batches.reload();
      inventory.reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const createBatch = () =>
    run(
      () => client.post('/productionbatches', { ...draft, shiftNumber: Number(draft.shiftNumber), operatorName: draft.operatorName.trim() }),
      'Batch planned',
      () => setCreateOpen(false),
    );

  const openAction = (kind: ActionKind) => {
    setFormError('');
    if (kind === 'input') setInput({ inventoryID: '', quantity: '' });
    if (kind === 'output') setOutput({ productID: '', quantity: '', materialType: 'Output' });
    if (kind === 'complete') setFinishWarehouse('');
    setAction(kind);
  };

  const submitAction = () => {
    if (!selected || !action) return;
    const id = selected.batchID;
    const done = async () => {
      setAction(null);
      await refreshSelected(id);
    };
    if (action === 'input' && chosenLot)
      return run(
        () =>
          client.post(`/productionbatches/${id}/add-input`, {
            productID: chosenLot.productID,
            quantity: Number(input.quantity),
            sourceLotNumber: chosenLot.lotNumber,
            sourceWarehouseID: chosenLot.warehouseID,
          }),
        `${formatNumber(Number(input.quantity))} ${chosenLot.unit} from ${chosenLot.lotNumber} added`,
        done,
      );
    if (action === 'output')
      return run(
        () =>
          client.post(`/productionbatches/${id}/add-output`, {
            productID: Number(output.productID),
            quantity: Number(output.quantity),
            materialType: output.materialType,
          }),
        `${output.materialType === 'Waste' ? 'Waste' : 'Output'} recorded`,
        done,
      );
    return run(
      () => client.put(`/productionbatches/${id}/finalize`, { warehouseID: Number(finishWarehouse) }),
      `${selected.batchNumber} completed`,
      done,
    );
  };

  const actionValid =
    action === 'input'
      ? !!chosenLot && Number(input.quantity) > 0 && Number(input.quantity) <= chosenLot.quantityAvailable
      : action === 'output'
        ? !!output.productID && Number(output.quantity) > 0
        : !!finishWarehouse;

  const newButton = (
    <Button
      variant="contained"
      startIcon={<AddIcon />}
      onClick={() => {
        setDraft({ productionDate: todayISO(), shiftNumber: '1', operatorName: '' });
        setFormError('');
        setCreateOpen(true);
      }}
    >
      Plan batch
    </Button>
  );

  const balance = selected ? selected.totalInputQuantity - selected.totalOutputQuantity - selected.totalWasteQuantity : 0;

  return (
    <Box>
      <PageHeader title="Production" subtitle="Milling batches: wheat in, flour, bran and waste out" actions={newButton} />

      <DataTable
        rows={batches.data}
        rowKey={(b) => b.batchID}
        loading={batches.loading}
        error={batches.error}
        onRetry={batches.reload}
        onRowClick={setSelected}
        emptyTitle="No batches yet"
        emptyHint="Plan a milling batch, then add the wheat lots it consumes and what it produces."
        emptyAction={newButton}
        columns={[
          { key: 'no', label: 'Batch', render: (b) => <strong>{b.batchNumber}</strong> },
          { key: 'date', label: 'Date', render: (b) => formatDate(b.productionDate) },
          { key: 'shift', label: 'Shift', render: (b) => b.shiftNumber },
          { key: 'op', label: 'Operator', render: (b) => b.operatorName || '-' },
          { key: 'in', label: 'Input', align: 'right', render: (b) => `${formatNumber(b.totalInputQuantity)} kg` },
          { key: 'out', label: 'Output', align: 'right', render: (b) => `${formatNumber(b.totalOutputQuantity)} kg` },
          {
            key: 'yield',
            label: 'Yield',
            align: 'right',
            render: (b) => (b.totalInputQuantity > 0 ? `${formatNumber(b.status === 'Completed' ? b.yieldPercentage : yieldOf(b), 1)}%` : '-'),
          },
          { key: 'status', label: 'Status', render: (b) => <StatusChip status={b.status} /> },
        ]}
      />

      <FormDialog
        open={createOpen}
        title="Plan a batch"
        submitLabel="Plan batch"
        submitting={saving}
        submitDisabled={!draft.productionDate}
        error={formError}
        onClose={() => setCreateOpen(false)}
        onSubmit={createBatch}
      >
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
          <TextField
            required
            type="date"
            label="Production date"
            value={draft.productionDate}
            onChange={(e) => setDraft({ ...draft, productionDate: e.target.value })}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField select label="Shift" value={draft.shiftNumber} onChange={(e) => setDraft({ ...draft, shiftNumber: e.target.value })}>
            {['1', '2', '3'].map((s) => (
              <MenuItem key={s} value={s}>
                Shift {s}
              </MenuItem>
            ))}
          </TextField>
        </Box>
        <TextField label="Operator name" value={draft.operatorName} onChange={(e) => setDraft({ ...draft, operatorName: e.target.value })} />
      </FormDialog>

      {selected && !action && (
        <DetailDialog
          open
          maxWidth="lg"
          title={
            <>
              {selected.batchNumber} <StatusChip status={selected.status} />
            </>
          }
          onClose={() => setSelected(null)}
          actions={
            selected.status !== 'Completed' && (
              <>
                <Button onClick={() => openAction('input')}>Add wheat input</Button>
                <Button onClick={() => openAction('output')} disabled={selected.totalInputQuantity === 0}>
                  Record output
                </Button>
                <Button variant="contained" onClick={() => openAction('complete')} disabled={selected.totalInputQuantity === 0}>
                  Complete batch
                </Button>
              </>
            )
          }
        >
          <InfoGrid
            items={[
              ['Date / shift', `${formatDate(selected.productionDate)}, shift ${selected.shiftNumber}`],
              ['Operator', selected.operatorName || '-'],
              ['Input', `${formatNumber(selected.totalInputQuantity)} kg`],
              ['Output / waste', `${formatNumber(selected.totalOutputQuantity)} / ${formatNumber(selected.totalWasteQuantity)} kg`],
            ]}
          />
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1.5 }}>
            <Typography sx={{ fontSize: '32px', fontWeight: 700, color: 'text.primary', lineHeight: 1 }}>
              {formatNumber(selected.status === 'Completed' ? selected.yieldPercentage : yieldOf(selected), 1)}%
            </Typography>
            <Typography sx={{ fontSize: '13px', color: 'text.secondary' }}>
              yield {selected.status === 'Completed' ? '' : '(so far)'}
            </Typography>
          </Box>
          {selected.totalInputQuantity > 0 && Math.abs(balance) > 0.001 && selected.totalOutputQuantity + selected.totalWasteQuantity > 0 && (
            <Alert severity={balance < 0 ? 'error' : 'info'}>
              {balance > 0
                ? `${formatNumber(balance)} kg of input is not yet accounted for as output or waste.`
                : `Output and waste exceed input by ${formatNumber(-balance)} kg. Check the quantities.`}
            </Alert>
          )}
          <SectionTitle>Materials</SectionTitle>
          <DataTable
            rows={selected.materials}
            rowKey={(m) => m.batchMaterialID}
            emptyTitle="Nothing recorded yet"
            emptyHint="Start by adding the wheat lots this batch will mill."
            columns={[
              { key: 'type', label: 'Type', render: (m) => m.materialType },
              { key: 'p', label: 'Product', render: (m) => m.productName },
              { key: 'lot', label: 'Source lot', render: (m) => m.sourceLotNumber || '-' },
              { key: 'q', label: 'Quantity', align: 'right', render: (m) => `${formatNumber(m.quantity)} kg` },
            ]}
          />
        </DetailDialog>
      )}

      <FormDialog
        open={!!action}
        title={action === 'input' ? 'Add wheat input' : action === 'output' ? 'Record output' : `Complete ${selected?.batchNumber ?? ''}`}
        submitLabel={action === 'complete' ? 'Complete batch' : 'Add'}
        submitting={saving}
        submitDisabled={!actionValid}
        error={formError}
        onClose={() => setAction(null)}
        onSubmit={submitAction}
      >
        {action === 'input' && (
          <>
            <TextField
              select
              required
              label="Wheat lot"
              value={input.inventoryID}
              onChange={(e) => setInput({ ...input, inventoryID: e.target.value })}
              helperText={rawLots.length === 0 ? 'No raw material in stock. Receive wheat on the Inbound Receipts page first.' : undefined}
            >
              {rawLots.map((l) => (
                <MenuItem key={l.inventoryID} value={String(l.inventoryID)}>
                  {l.lotNumber} : {l.productName}, {formatNumber(l.quantityAvailable)} {l.unit} in {l.warehouseName}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              required
              type="number"
              label={`Quantity (${chosenLot?.unit ?? 'kg'})`}
              value={input.quantity}
              onChange={(e) => setInput({ ...input, quantity: e.target.value })}
              error={!!chosenLot && Number(input.quantity) > chosenLot.quantityAvailable}
              helperText={chosenLot ? `Up to ${formatNumber(chosenLot.quantityAvailable)} ${chosenLot.unit} available` : undefined}
              slotProps={{ htmlInput: { min: 0, step: 'any' } }}
            />
          </>
        )}

        {action === 'output' && (
          <>
            <TextField
              select
              required
              label="Product"
              value={output.productID}
              onChange={(e) => {
                const p = outputProducts.find((x) => String(x.productID) === e.target.value);
                setOutput({ ...output, productID: e.target.value, materialType: p?.category === 'Waste' ? 'Waste' : 'Output' });
              }}
              helperText={outputProducts.length === 0 ? 'Add finished goods, by-products or waste on the Products page first.' : undefined}
            >
              {outputProducts.map((p) => (
                <MenuItem key={p.productID} value={String(p.productID)}>
                  {p.productName} ({p.category})
                </MenuItem>
              ))}
            </TextField>
            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
              <TextField
                required
                type="number"
                label="Quantity (kg)"
                value={output.quantity}
                onChange={(e) => setOutput({ ...output, quantity: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 'any' } }}
              />
              <TextField select label="Counts as" value={output.materialType} onChange={(e) => setOutput({ ...output, materialType: e.target.value })}>
                <MenuItem value="Output">Output (counts toward yield)</MenuItem>
                <MenuItem value="Waste">Waste</MenuItem>
              </TextField>
            </Box>
          </>
        )}

        {action === 'complete' && selected && (
          <>
            <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>
              Final yield will be {formatNumber(yieldOf(selected), 1)}%. Outputs are added to stock under lot {selected.batchNumber}. After this the batch cannot be changed.
            </Typography>
            <TextField select required label="Finished goods warehouse" value={finishWarehouse} onChange={(e) => setFinishWarehouse(e.target.value)}>
              {warehouses.data?.map((w) => (
                <MenuItem key={w.warehouseID} value={String(w.warehouseID)}>
                  {w.warehouseName}
                </MenuItem>
              ))}
            </TextField>
          </>
        )}
      </FormDialog>
    </Box>
  );
};

export default Production;
