import React, { useState } from 'react';
import { Alert, Box, Button, MenuItem, Step, StepLabel, Stepper, TextField, Typography } from '@mui/material';
import { Add as AddIcon } from '@mui/icons-material';
import client from '../api/client';
import PackagingReceiptButton from '../components/PackagingReceiptButton';
import ReasonDialog from '../components/ReasonDialog';
import { useAuth } from '../context/AuthContext';
import { canPurchase } from '../auth/permissions';
import type { InboundReceipt, PurchaseOrder, Warehouse } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { useToast } from '../context/ToastContext';
import { formatDate, formatDateTime, formatNumber, getErrorMessage } from '../utils/format';
import PageHeader from '../components/ui/PageHeader';
import DataTable from '../components/ui/DataTable';
import FormDialog from '../components/ui/FormDialog';
import StatusChip from '../components/ui/StatusChip';
import { DetailDialog, InfoGrid, SectionTitle } from '../components/ui/DetailDialog';

type StepKind = 'weigh' | 'qc' | 'grn';

const STEPS = ['Gate entry', 'Weighbridge', 'Quality check', 'Goods received'];
const STEP_INDEX: Record<string, number> = { GateEntry: 1, Weighed: 2, QCApproved: 3, Received: 4, Rejected: 3 };
const NEXT_STEP: Record<string, { kind: StepKind; label: string } | undefined> = {
  GateEntry: { kind: 'weigh', label: 'Record weight' },
  Weighed: { kind: 'qc', label: 'Record QC' },
  QCApproved: { kind: 'grn', label: 'Receive into stock' },
};

const ReceiptStepper: React.FC<{ status: string }> = ({ status }) => (
  <Stepper activeStep={STEP_INDEX[status] ?? 0} alternativeLabel>
    {STEPS.map((label, i) => (
      <Step key={label}>
        <StepLabel error={status === 'Rejected' && i === 2}>{status === 'Rejected' && i === 2 ? 'QC rejected' : label}</StepLabel>
      </Step>
    ))}
  </Stepper>
);

const InboundReceipts: React.FC = () => {
  const receipts = useFetch<InboundReceipt[]>('/inboundreceipts');
  const orders = useFetch<PurchaseOrder[]>('/purchaseorders');
  const warehouses = useFetch<Warehouse[]>('/warehouses');
  const toast = useToast();

  const [gateOpen, setGateOpen] = useState(false);
  const [gate, setGate] = useState({ poid: '', poLineItemID: '', truckNumber: '', driverName: '', driverPhone: '' });
  const [stepFor, setStepFor] = useState<{ receipt: InboundReceipt; kind: StepKind } | null>(null);
  const [weigh, setWeigh] = useState({ grossWeight: '', tareWeight: '' });
  const [qc, setQc] = useState({ moisture: '', impurities: '', grainType: '', qcResult: 'Approved' });
  const [grn, setGrn] = useState({ warehouseID: '', binCode: '', lotNumber: '' });
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);
  const [viewing, setViewing] = useState<InboundReceipt | null>(null);
  const [voiding, setVoiding] = useState(false);
  const { user } = useAuth();

  const openPOs = orders.data?.filter((o) => o.status === 'Sent') ?? [];
  const gatePO = openPOs.find((o) => String(o.poid) === gate.poid);
  const net = (Number(weigh.grossWeight) || 0) - (Number(weigh.tareWeight) || 0);

  const openStep = (receipt: InboundReceipt, kind: StepKind) => {
    setFormError('');
    if (kind === 'weigh') setWeigh({ grossWeight: '', tareWeight: '' });
    if (kind === 'qc') setQc({ moisture: '', impurities: '', grainType: '', qcResult: 'Approved' });
    if (kind === 'grn') setGrn({ warehouseID: '', binCode: '', lotNumber: `LOT-${receipt.receiptNumber.replace(/^GE-/, '')}` });
    setViewing(null);
    setStepFor({ receipt, kind });
  };

  const submit = async (action: () => Promise<unknown>, success: string, close: () => void) => {
    setSaving(true);
    setFormError('');
    try {
      await action();
      toast(success);
      close();
      receipts.reload();
      orders.reload();
    } catch (err) {
      setFormError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const createGate = () =>
    submit(
      () => client.post('/inboundreceipts/gate-entry', { ...gate, poid: Number(gate.poid), poLineItemID: Number(gate.poLineItemID) }),
      `Truck ${gate.truckNumber} checked in`,
      () => setGateOpen(false),
    );

  const submitStep = () => {
    if (!stepFor) return;
    const { receipt, kind } = stepFor;
    const id = receipt.receiptID;
    const close = () => setStepFor(null);
    if (kind === 'weigh')
      return submit(
        () => client.put(`/inboundreceipts/${id}/weighbridge`, { grossWeight: Number(weigh.grossWeight), tareWeight: Number(weigh.tareWeight) }),
        `${receipt.receiptNumber}: net weight ${formatNumber(net)} kg recorded`,
        close,
      );
    if (kind === 'qc')
      return submit(
        () =>
          client.put(`/inboundreceipts/${id}/qc`, {
            moisture: Number(qc.moisture),
            impurities: Number(qc.impurities),
            grainType: qc.grainType.trim(),
            qcResult: qc.qcResult,
          }),
        `${receipt.receiptNumber}: QC ${qc.qcResult.toLowerCase()}`,
        close,
      );
    return submit(
      () => client.put(`/inboundreceipts/${id}/grn`, { warehouseID: Number(grn.warehouseID), binCode: grn.binCode.trim(), lotNumber: grn.lotNumber.trim() }),
      `${receipt.receiptNumber}: received into stock as ${grn.lotNumber}`,
      close,
    );
  };

  const stepValid =
    stepFor?.kind === 'weigh'
      ? Number(weigh.grossWeight) > 0 && weigh.tareWeight !== '' && net > 0
      : stepFor?.kind === 'qc'
        ? qc.moisture !== '' && qc.impurities !== '' && !!qc.grainType.trim()
        : !!grn.warehouseID && !!grn.lotNumber.trim();

  const newButton = (
    <Button
      variant="contained"
      startIcon={<AddIcon />}
      onClick={() => {
        setGate({ poid: '', poLineItemID: '', truckNumber: '', driverName: '', driverPhone: '' });
        setFormError('');
        setGateOpen(true);
      }}
    >
      New gate entry
    </Button>
  );

  return (
    <Box>
      <PageHeader title="Inbound Receipts" subtitle="Receive weighed wheat loads or count packaging supplies into stock" actions={<Box sx={{ display: 'flex', gap: 1 }}><PackagingReceiptButton onReceived={() => { receipts.reload(); orders.reload(); }} />{newButton}</Box>} />

      <DataTable
        rows={receipts.data}
        rowKey={(r) => r.receiptID}
        loading={receipts.loading}
        error={receipts.error}
        onRetry={receipts.reload}
        onRowClick={setViewing}
        emptyTitle="No trucks received yet"
        emptyHint="Record a gate entry when a supplier's truck arrives."
        emptyAction={newButton}
        columns={[
          { key: 'no', label: 'Receipt', render: (r) => <strong>{r.receiptNumber}</strong> },
          { key: 'po', label: 'PO', render: (r) => r.poNumber },
          { key: 'truck', label: 'Truck', render: (r) => r.truckNumber },
          { key: 'arrived', label: 'Arrived', render: (r) => formatDateTime(r.arrivalDateTime) },
          { key: 'net', label: 'Quantity', align: 'right', render: (r) => r.receiptType === 'Counted' ? `${formatNumber(r.receivedQuantity)} pieces` : (r.netWeight != null ? `${formatNumber(r.netWeight)} kg` : '-') },
          { key: 'status', label: 'Status', render: (r) => <StatusChip status={r.status} /> },
          {
            key: 'next',
            label: '',
            align: 'right',
            render: (r) => {
              const next = NEXT_STEP[r.status];
              return next ? (
                <Button
                  size="small"
                  variant="outlined"
                  onClick={(e) => {
                    e.stopPropagation();
                    openStep(r, next.kind);
                  }}
                  sx={{ whiteSpace: 'nowrap' }}
                >
                  {next.label}
                </Button>
              ) : null;
            },
          },
        ]}
      />

      <FormDialog
        open={gateOpen}
        title="New gate entry"
        submitLabel="Check truck in"
        submitting={saving}
        submitDisabled={!gate.poid || !gate.poLineItemID || !gate.truckNumber.trim() || !gate.driverName.trim()}
        error={formError}
        onClose={() => setGateOpen(false)}
        onSubmit={createGate}
      >
        <TextField
          select
          required
          label="Purchase order"
          value={gate.poid}
          onChange={(e) => setGate({ ...gate, poid: e.target.value, poLineItemID: '' })}
          helperText={openPOs.length === 0 ? 'No purchase orders are marked as sent. A PO must be sent before its truck can arrive.' : undefined}
        >
          {openPOs.map((o) => (
            <MenuItem key={o.poid} value={String(o.poid)}>
              {o.poNumber} : {o.supplierName} ({o.lineItems.map((l) => `${formatNumber(l.quantity)} ${l.unit} ${l.productName}`).join(', ')})
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          required
          label="Product on purchase order"
          value={gate.poLineItemID}
          onChange={(e) => setGate({ ...gate, poLineItemID: e.target.value })}
          helperText="Create one receipt per PO line so its measured weight is assigned to the correct product."
        >
          {gatePO?.lineItems.filter((l) => l.unit === 'kg' && l.receivedQuantity < l.quantity).map((l) => (
            <MenuItem key={l.lineItemID} value={String(l.lineItemID)}>
              {l.productName}: {formatNumber(l.quantity - l.receivedQuantity)} kg remaining
            </MenuItem>
          ))}
        </TextField>
        <TextField required label="Truck number" value={gate.truckNumber} onChange={(e) => setGate({ ...gate, truckNumber: e.target.value })} />
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
          <TextField required label="Driver name" value={gate.driverName} onChange={(e) => setGate({ ...gate, driverName: e.target.value })} />
          <TextField label="Driver phone" value={gate.driverPhone} onChange={(e) => setGate({ ...gate, driverPhone: e.target.value })} />
        </Box>
      </FormDialog>

      <FormDialog
        open={!!stepFor}
        title={
          stepFor
            ? `${stepFor.receipt.receiptNumber}: ${{ weigh: 'Weighbridge', qc: 'Quality check', grn: 'Receive into stock' }[stepFor.kind]}`
            : ''
        }
        submitLabel={stepFor?.kind === 'grn' ? 'Receive' : 'Save'}
        submitting={saving}
        submitDisabled={!stepValid}
        error={formError}
        onClose={() => setStepFor(null)}
        onSubmit={submitStep}
      >
        {stepFor?.kind === 'weigh' && (
          <>
            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
              <TextField
                required
                type="number"
                label="Gross weight (kg)"
                value={weigh.grossWeight}
                onChange={(e) => setWeigh({ ...weigh, grossWeight: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 'any' } }}
              />
              <TextField
                required
                type="number"
                label="Tare weight (kg)"
                value={weigh.tareWeight}
                onChange={(e) => setWeigh({ ...weigh, tareWeight: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 'any' } }}
              />
            </Box>
            <Typography sx={{ fontSize: '15px', fontWeight: 700, color: net > 0 || !weigh.tareWeight ? 'text.primary' : 'error.main' }}>
              Net weight: {formatNumber(net)} kg
              {weigh.tareWeight !== '' && net <= 0 && ' (gross must be more than tare)'}
            </Typography>
          </>
        )}

        {stepFor?.kind === 'qc' && (
          <>
            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
              <TextField
                required
                type="number"
                label="Moisture (%)"
                value={qc.moisture}
                onChange={(e) => setQc({ ...qc, moisture: e.target.value })}
                slotProps={{ htmlInput: { min: 0, max: 100, step: 'any' } }}
              />
              <TextField
                required
                type="number"
                label="Impurities (%)"
                value={qc.impurities}
                onChange={(e) => setQc({ ...qc, impurities: e.target.value })}
                slotProps={{ htmlInput: { min: 0, max: 100, step: 'any' } }}
              />
            </Box>
            <TextField required label="Grain type" placeholder="For example Grade A hard wheat" value={qc.grainType} onChange={(e) => setQc({ ...qc, grainType: e.target.value })} />
            <TextField select label="Result" value={qc.qcResult} onChange={(e) => setQc({ ...qc, qcResult: e.target.value })}>
              <MenuItem value="Approved">Approved</MenuItem>
              <MenuItem value="Rejected">Rejected</MenuItem>
            </TextField>
            {qc.qcResult === 'Rejected' && <Alert severity="warning">A rejected load cannot be received into stock.</Alert>}
          </>
        )}

        {stepFor?.kind === 'grn' && (
          <>
            <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>
              {formatNumber(stepFor.receipt.netWeight)} kg will be added to stock.
            </Typography>
            <TextField
              select
              required
              label="Warehouse"
              value={grn.warehouseID}
              onChange={(e) => setGrn({ ...grn, warehouseID: e.target.value })}
              helperText={warehouses.data?.length === 0 ? 'No warehouses yet. Add one on the Warehouses page.' : undefined}
            >
              {warehouses.data?.filter(w => w.isActive).map((w) => (
                <MenuItem key={w.warehouseID} value={String(w.warehouseID)}>
                  {w.warehouseName}
                </MenuItem>
              ))}
            </TextField>
            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
              <TextField label="Bin" placeholder="A-01" value={grn.binCode} onChange={(e) => setGrn({ ...grn, binCode: e.target.value })} />
              <TextField required label="Lot number" value={grn.lotNumber} onChange={(e) => setGrn({ ...grn, lotNumber: e.target.value })} />
            </Box>
          </>
        )}
      </FormDialog>

      {viewing && (
        <DetailDialog
          open
          title={
            <>
              {viewing.receiptNumber} <StatusChip status={viewing.status} />
            </>
          }
          onClose={() => setViewing(null)}
          actions={
            NEXT_STEP[viewing.status] && (
              <>
                {canPurchase(user?.role) && (
                  <Button color="inherit" onClick={() => setVoiding(true)} sx={{ mr: 'auto' }}>
                    Void receipt
                  </Button>
                )}
                <Button variant="contained" onClick={() => openStep(viewing, NEXT_STEP[viewing.status]!.kind)}>
                  {NEXT_STEP[viewing.status]!.label}
                </Button>
              </>
            )
          }
        >
          {viewing.status === 'Voided' && <Alert severity="info">Voided: {viewing.voidReason}</Alert>}
          {viewing.receiptType === 'Weighed' ? viewing.status !== 'Voided' && <ReceiptStepper status={viewing.status} /> : <Alert severity="info">Counted packaging receipt: {formatNumber(viewing.receivedQuantity)} pieces</Alert>}
          <SectionTitle>Truck</SectionTitle>
          <InfoGrid
            items={[
              ['Purchase order', viewing.poNumber],
              ['Product', viewing.productName || '-'],
              ['Truck', viewing.truckNumber],
              ['Driver', `${viewing.driverName}${viewing.driverPhone ? ` (${viewing.driverPhone})` : ''}`],
              ['Arrived', formatDateTime(viewing.arrivalDateTime)],
            ]}
          />
          <SectionTitle>Weight and quality</SectionTitle>
          <InfoGrid
            items={[
              ['Gross / tare', viewing.grossWeight != null ? `${formatNumber(viewing.grossWeight)} / ${formatNumber(viewing.tareWeight)} kg` : '-'],
              ['Net weight', viewing.netWeight != null ? `${formatNumber(viewing.netWeight)} kg` : '-'],
              ['Moisture / impurities', viewing.moisture != null ? `${viewing.moisture}% / ${viewing.impurities}%` : '-'],
              ['QC result', viewing.qcResult ? `${viewing.qcResult}${viewing.grainType ? ` (${viewing.grainType})` : ''}` : '-'],
            ]}
          />
          {viewing.status === 'Received' && (
            <>
              <SectionTitle>Stock</SectionTitle>
              <InfoGrid
                items={[
                  ['Warehouse', viewing.warehouseName],
                  ['Bin', viewing.binCode || '-'],
                  ['Lot number', viewing.lotNumber],
                  ['Received', formatDate(viewing.receivedDate)],
                ]}
              />
            </>
          )}
        </DetailDialog>
      )}
      {viewing && (
        <ReasonDialog open={voiding} title={`Void ${viewing.receiptNumber}`} submitLabel="Void receipt"
          description="Use this for a truck that was turned away or a load that cannot be accepted. Nothing is added to stock and the record is kept."
          url={`/inboundreceipts/${viewing.receiptID}/void`} success={`${viewing.receiptNumber} voided`}
          onClose={() => setVoiding(false)} onDone={() => { setVoiding(false); setViewing(null); receipts.reload(); orders.reload(); }} />
      )}
    </Box>
  );
};

export default InboundReceipts;
