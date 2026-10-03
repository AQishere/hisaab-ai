import { Typography } from '@mui/material';
import { useFetch } from '../hooks/useFetch';
import { formatDateTime, formatNumber } from '../utils/format';
import DataTable from './ui/DataTable';
import { SectionTitle } from './ui/DetailDialog';

const MOVEMENT_LABELS: Record<string, string> = {
  OpeningBalance: 'Opening balance', Receipt: 'Receipt', ProductionInput: 'Production input',
  ProductionReversal: 'Production reversal', ProductionOutput: 'Production output', LegacyBulkOpening: 'Bulk location recorded',
  PackingBulk: 'Packed (bulk used)', PackingSupply: 'Packed (empty bags used)', PackingOutput: 'Packed (bags added)', Dispatch: 'Dispatch',
};
const unitLabel = (quantity: number, unit: string) => (unit === 'kg' || Math.abs(quantity) === 1 ? unit : `${unit}s`);

export function StockHistory({ inventoryID }: { inventoryID: number }) {
  const rows = useFetch<{ stockMovementID: number; recordedAt: string; movementType: string; reference: string; quantityChange: number; unit: string; reason: string | null }[]>(`/inventory/${inventoryID}/movements`);
  return <DataTable rows={rows.data} rowKey={r => r.stockMovementID} loading={rows.loading} error={rows.error} onRetry={rows.reload}
    emptyTitle="No movements recorded" columns={[
      { key: 'date', label: 'Date', render: r => formatDateTime(r.recordedAt) },
      { key: 'type', label: 'Movement', render: r => MOVEMENT_LABELS[r.movementType] ?? r.movementType },
      { key: 'ref', label: 'Reference', render: r => r.reference },
      { key: 'qty', label: 'Change', render: r => `${r.quantityChange > 0 ? '+' : ''}${formatNumber(r.quantityChange)} ${unitLabel(r.quantityChange, r.unit)}` },
      { key: 'reason', label: 'Note', render: r => r.reason || '-' },
    ]} />;
}

export function PackingHistory({ batchID }: { batchID: number }) {
  const rows = useFetch<{ packingRunID: number; productName: string; quantityKg: number; bagsPacked: number; kgPerBag: number; packedAt: string; sourceWarehouse: string; destinationWarehouse: string; packagingLot: string }[]>(`/productionbatches/${batchID}/packing-runs`);
  return <><SectionTitle>Packing history</SectionTitle><DataTable rows={rows.data} rowKey={r => r.packingRunID} loading={rows.loading} error={rows.error} onRetry={rows.reload}
    emptyTitle="No packing runs recorded" emptyHint="History starts with packing recorded after the stock-history upgrade." columns={[
      { key: 'date', label: 'Packed', render: r => formatDateTime(r.packedAt) },
      { key: 'product', label: 'Product', render: r => r.productName },
      { key: 'qty', label: 'Quantity', render: r => `${formatNumber(r.quantityKg)} kg / ${formatNumber(r.bagsPacked)} bags` },
      { key: 'from', label: 'Bulk warehouse', render: r => r.sourceWarehouse },
      { key: 'to', label: 'Bag warehouse', render: r => r.destinationWarehouse },
      { key: 'packaging', label: 'Empty bag lot', render: r => r.packagingLot },
    ]} /></>;
}

export function DispatchHistory({ orderID }: { orderID: number }) {
  const rows = useFetch<{ dispatchID: number; truckNumber: string; driverName: string; dispatchedAt: string; lines: { dispatchLineID: number; quantity: number; unit: string; productName: string; lotNumber: string; warehouse: string }[] }[]>(`/salesorders/${orderID}/dispatches`);
  return <><SectionTitle>Delivery history</SectionTitle><Typography variant="body2" color="text.secondary">Deliveries recorded before the history upgrade retain only their last truck details on the order line.</Typography>
    <DataTable rows={rows.data} rowKey={r => r.dispatchID} loading={rows.loading} error={rows.error} onRetry={rows.reload} emptyTitle="No delivery history recorded" columns={[
      { key: 'date', label: 'Dispatched', render: r => formatDateTime(r.dispatchedAt) },
      { key: 'truck', label: 'Truck / driver', render: r => `${r.truckNumber} / ${r.driverName}` },
      { key: 'lines', label: 'Stock dispatched', render: r => r.lines.map(l => <div key={l.dispatchLineID}>{l.productName}: {formatNumber(l.quantity)} {unitLabel(l.quantity, l.unit)} from {l.warehouse}, lot {l.lotNumber}</div>) },
    ]} /></>;
}
