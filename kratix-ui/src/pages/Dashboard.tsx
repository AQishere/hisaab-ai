import React, { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useTheme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Card, Chip, CircularProgress, Grid, Typography } from '@mui/material';
import {
  CheckCircleOutlined as AllClearIcon,
  ChevronRight as ChevronRightIcon,
  ErrorOutlined as ErrorIcon,
  Factory as FactoryIcon,
  InfoOutlined as InfoIcon,
  Inventory as InventoryIcon,
  LocalShipping as TruckIcon,
  Payment as PaymentIcon,
  Receipt as ReceiptIcon,
  Refresh as RefreshIcon,
  ShoppingCart as CartIcon,
  TrendingUp as TrendingUpIcon,
  WarningAmber as WarningIcon,
} from '@mui/icons-material';
import { useFetch } from '../hooks/useFetch';
import { useAuth } from '../hooks/useAuth';
import { ROLE_LABELS, type Role } from '../auth/permissions';
import { formatCompact as compact, formatNumber } from '../utils/format';
import { ColumnChart, HBars, LineChart, SegmentBar, type ChartPoint } from '../components/dashboard/charts';

// Shapes of GET /api/dashboard/overview (Hisaab.ERP/DTOs/DashboardOverviewDto.cs).
interface Kpi { key: string; label: string; value: number | null; format: 'money' | 'kg' | 'bags' | 'percent'; caption: string; change: number | null; changeLabel: string | null }
interface Attention { key: string; severity: 'error' | 'warning' | 'info'; title: string; detail: string; count: number | null; amount: number | null; link: string }
interface Check { key: string; label: string }
interface Activity { at: string; kind: string; title: string; detail: string; link: string }
interface Overview {
  asOf: string;
  kpis: Kpi[];
  attention: Attention[];
  checks: Check[];
  sales: { days: { date: string; amount: number; orders: number }[]; last30Days: number; previous30Days: number } | null;
  receivables: { notDue: number; days1To30: number; days31To60: number; days61Plus: number; total: number; topDebtors: { customerName: string; balance: number; invoices: number; oldestDaysOverdue: number }[] } | null;
  production: { days: { date: string; inputKg: number; outputKg: number; yield: number | null; batches: number }[]; yieldLast30Days: number | null } | null;
  stock: { products: { productName: string; category: string; unit: string; quantity: number; dispatchedPerDay: number; daysLeft: number | null }[]; wheatKg: number; bulkKg: number; wheatExpectedKg: number | null; wheatExpectedOrders: number | null } | null;
  wheatQuality: { loads: number; rejected: number; avgMoisture: number | null; avgImpurities: number | null; bySupplier: { supplierName: string; loads: number; rejected: number; avgMoisture: number | null; avgImpurities: number | null; netKg: number }[] } | null;
  activity: Activity[];
}

const tint = (color: string, pct: number) => `color-mix(in srgb, ${color} ${pct}%, transparent)`;
const ACCENT = 'var(--mui-palette-primary-main)';
const money = (v: number) => `PKR ${formatNumber(v, 0)}`;
const shortDate = (iso: string) => new Date(iso).toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
const pct = (v: number | null | undefined) => (v == null ? '-' : `${formatNumber(v, 1)}%`);

function timeAgo(iso: string) {
  const mins = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins} min ago`;
  const hours = Math.round(mins / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.round(hours / 24);
  return days < 7 ? `${days} d ago` : new Date(iso).toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
}

const Panel: React.FC<{ title: string; subtitle?: string; action?: React.ReactNode; children: React.ReactNode }> = ({ title, subtitle, action, children }) => (
  <Card sx={{ p: '20px', bgcolor: 'background.paper', height: '100%' }}>
    <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 2, mb: 2 }}>
      <Box>
        <Typography sx={{ fontSize: '15px', fontWeight: 700 }}>{title}</Typography>
        {subtitle && <Typography sx={{ fontSize: '12px', color: 'text.secondary', mt: 0.25 }}>{subtitle}</Typography>}
      </Box>
      {action}
    </Box>
    {children}
  </Card>
);

const KPI_ICON: Record<string, typeof InventoryIcon> = {
  'sales-month': PaymentIcon, receivables: ReceiptIcon, 'flour-stock': CartIcon, 'wheat-stock': InventoryIcon, 'milled-7d': FactoryIcon, 'yield-7d': TrendingUpIcon,
};

function kpiValue(k: Kpi) {
  if (k.value == null) return { main: '-', unit: '' };
  switch (k.format) {
    case 'money': return { main: `PKR ${formatNumber(k.value, 0)}`, unit: '' };
    case 'percent': return { main: formatNumber(k.value, 1), unit: '%' };
    default: return { main: formatNumber(k.value, 0), unit: k.format };
  }
}

const KpiCard: React.FC<{ kpi: Kpi }> = ({ kpi }) => {
  const Icon = KPI_ICON[kpi.key] ?? TrendingUpIcon;
  const { main, unit } = kpiValue(kpi);
  const points = kpi.format === 'percent';
  const up = (kpi.change ?? 0) > 0, flat = kpi.change === 0;
  return (
    <Card sx={{ height: '100%', bgcolor: 'background.paper', p: '20px', '&:hover': { borderColor: ACCENT, boxShadow: `0 4px 12px ${tint(ACCENT, 18)}` } }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 2 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography sx={{ fontSize: '12px', fontWeight: 600, color: 'text.secondary', mb: 1.5, textTransform: 'uppercase', letterSpacing: '0.5px' }}>{kpi.label}</Typography>
          <Typography sx={{ fontSize: '30px', fontWeight: 700, color: 'text.primary', lineHeight: 1 }}>
            {main}{unit && <Box component="span" sx={{ fontSize: '15px', fontWeight: 600, color: 'text.secondary', ml: 0.75 }}>{unit}</Box>}
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 1.25, flexWrap: 'wrap' }}>
            {kpi.change != null && (
              <Chip size="small" label={`${up ? '+' : ''}${formatNumber(kpi.change, 1)}${points ? ' pts' : '%'}`}
                sx={{ height: 22, fontSize: '12px', fontWeight: 700, bgcolor: flat ? 'action.hover' : tint(up ? 'var(--tone-success)' : 'var(--tone-error)', 16), color: flat ? 'text.secondary' : up ? 'var(--tone-success)' : 'var(--tone-error)' }} />
            )}
            <Typography sx={{ fontSize: '12px', color: 'text.secondary', fontWeight: 500 }}>{kpi.change != null && kpi.changeLabel ? kpi.changeLabel : kpi.caption}</Typography>
          </Box>
          {kpi.change != null && kpi.changeLabel && <Typography sx={{ fontSize: '12px', color: 'text.secondary', mt: 0.5 }}>{kpi.caption}</Typography>}
        </Box>
        <Box sx={{ width: 52, height: 52, borderRadius: '12px', bgcolor: tint(ACCENT, 22), display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
          <Icon sx={{ fontSize: 26, color: 'var(--accent-text)' }} />
        </Box>
      </Box>
    </Card>
  );
};

const SEVERITY = {
  error: { Icon: ErrorIcon, color: 'var(--tone-error)', label: 'Urgent' },
  warning: { Icon: WarningIcon, color: 'var(--tone-warning)', label: 'Needs action' },
  info: { Icon: InfoIcon, color: 'var(--tone-neutral)', label: 'For information' },
};

const AttentionPanel: React.FC<{ items: Attention[]; checks: Check[] }> = ({ items, checks }) => {
  const navigate = useNavigate();
  const clear = checks.filter((c) => !items.some((i) => i.key === c.key));
  return (
    <Panel title="Needs attention" subtitle={items.length ? `${items.length} ${items.length === 1 ? 'item' : 'items'} to act on today` : 'Nothing to act on today'}>
      {items.length > 0 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, mb: clear.length ? 2 : 0 }}>
          {items.map((a) => {
            const s = SEVERITY[a.severity] ?? SEVERITY.info;
            return (
              <Box key={a.key} component="button" type="button" onClick={() => navigate(a.link)}
                sx={{ all: 'unset', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 1.5, p: 1.25, borderRadius: '8px', border: 1, borderColor: 'divider', borderLeft: `3px solid ${s.color}`,
                  '&:hover, &:focus-visible': { bgcolor: 'action.hover' } }}>
                <s.Icon sx={{ color: s.color, fontSize: 22 }} aria-label={s.label} />
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography sx={{ fontSize: '14px', fontWeight: 600 }}>{a.title}</Typography>
                  <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>{a.detail}</Typography>
                </Box>
                {a.amount != null && a.key !== 'bulk-to-pack' && <Typography sx={{ fontSize: '13px', fontWeight: 700, whiteSpace: 'nowrap' }}>{money(a.amount)}</Typography>}
                <ChevronRightIcon sx={{ color: 'text.secondary' }} />
              </Box>
            );
          })}
        </Box>
      )}
      {clear.length > 0 && (
        <>
          {items.length > 0 && <Typography sx={{ fontSize: '12px', fontWeight: 700, color: 'text.secondary', textTransform: 'uppercase', letterSpacing: '.5px', mb: 1 }}>All clear</Typography>}
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(190px, 1fr))', gap: 1 }}>
            {clear.map((c) => (
              <Box key={c.key} sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.25, py: 1, borderRadius: '8px', bgcolor: tint('var(--tone-success)', 8) }}>
                <AllClearIcon sx={{ color: 'var(--tone-success)', fontSize: 18 }} />
                <Typography sx={{ fontSize: '13px', fontWeight: 500 }}>{c.label}</Typography>
              </Box>
            ))}
          </Box>
        </>
      )}
    </Panel>
  );
};

const SalesPanel: React.FC<{ sales: NonNullable<Overview['sales']> }> = ({ sales }) => {
  const change = sales.previous30Days > 0 ? ((sales.last30Days - sales.previous30Days) / sales.previous30Days) * 100 : null;
  const points: ChartPoint[] = sales.days.map((d) => ({
    label: shortDate(d.date), value: d.amount,
    tip: <><b>{shortDate(d.date)}</b><br />{money(d.amount)} · {d.orders} {d.orders === 1 ? 'order' : 'orders'}</>,
  }));
  return (
    <Panel title="Sales, last 30 days" subtitle="Value of sales orders by order date"
      action={<Box sx={{ textAlign: 'right' }}>
        <Typography sx={{ fontSize: '18px', fontWeight: 700 }}>{money(sales.last30Days)}</Typography>
        <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>
          {change == null ? 'No sales in the 30 days before' : `${change >= 0 ? '+' : ''}${formatNumber(change, 1)}% vs previous 30 days`}
        </Typography>
      </Box>}>
      <LineChart points={points} area format={compact} summary={`Sales over the last 30 days, ${money(sales.last30Days)} in total`} />
    </Panel>
  );
};

const MillingPanel: React.FC<{ production: NonNullable<Overview['production']> }> = ({ production }) => {
  const points: ChartPoint[] = production.days.map((d) => ({
    label: shortDate(d.date), value: d.inputKg || null,
    tip: d.inputKg ? <><b>{shortDate(d.date)}</b><br />{formatNumber(d.inputKg, 0)} kg wheat milled<br />{d.batches} {d.batches === 1 ? 'batch' : 'batches'}</> : <><b>{shortDate(d.date)}</b><br />No completed batches</>,
  }));
  const total = production.days.reduce((s, d) => s + d.inputKg, 0);
  return (
    <Panel title="Wheat milled, last 14 days" subtitle="Input of completed batches, kg per day"
      action={<Typography sx={{ fontSize: '18px', fontWeight: 700 }}>{formatNumber(total, 0)} kg</Typography>}>
      <ColumnChart points={points} format={compact} summary={`Wheat milled per day over 14 days, ${formatNumber(total, 0)} kg in total`} />
    </Panel>
  );
};

const YieldPanel: React.FC<{ production: NonNullable<Overview['production']> }> = ({ production }) => {
  const values = production.days.map((d) => d.yield).filter((v): v is number => v != null);
  const avg = production.yieldLast30Days;
  const all = avg != null ? [...values, avg] : values;
  const domain: [number, number] = all.length ? [Math.floor(Math.min(...all) - 2), Math.min(100, Math.ceil(Math.max(...all) + 1))] : [80, 100];
  const points: ChartPoint[] = production.days.map((d) => ({
    label: shortDate(d.date), value: d.yield,
    tip: d.yield != null ? <><b>{shortDate(d.date)}</b><br />Yield {pct(d.yield)}<br />{formatNumber(d.outputKg, 0)} of {formatNumber(d.inputKg, 0)} kg</> : <><b>{shortDate(d.date)}</b><br />No completed batches</>,
  }));
  return (
    <Panel title="Milling yield, last 14 days" subtitle="Output as a share of wheat input"
      action={<Box sx={{ textAlign: 'right' }}>
        <Typography sx={{ fontSize: '18px', fontWeight: 700 }}>{pct(avg)}</Typography>
        <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>30-day average</Typography>
      </Box>}>
      {values.length === 0
        ? <Typography sx={{ fontSize: '13px', color: 'text.secondary', py: 6, textAlign: 'center' }}>No completed batches in the last 14 days.</Typography>
        : <LineChart points={points} domain={domain} format={(v) => `${v}%`} reference={avg != null ? { value: avg, label: `30-day avg ${pct(avg)}` } : undefined}
            summary={`Daily milling yield over 14 days; 30-day average ${pct(avg)}`} />}
    </Panel>
  );
};

const ReceivablesPanel: React.FC<{ r: NonNullable<Overview['receivables']> }> = ({ r }) => {
  const navigate = useNavigate();
  const overdue = r.days1To30 + r.days31To60 + r.days61Plus;
  return (
    <Panel title="Money owed to you" subtitle="Unpaid invoices by days past due"
      action={<Box sx={{ textAlign: 'right' }}>
        <Typography sx={{ fontSize: '18px', fontWeight: 700 }}>{money(r.total)}</Typography>
        <Typography sx={{ fontSize: '12px', color: overdue > 0 ? 'var(--tone-error)' : 'text.secondary' }}>{overdue > 0 ? `${money(overdue)} overdue` : 'Nothing overdue'}</Typography>
      </Box>}>
      <SegmentBar format={money} summary={`Receivables ${money(r.total)}, of which ${money(overdue)} overdue`}
        parts={[
          { label: 'Not yet due', value: r.notDue, strength: 0 },
          { label: '1-30 days late', value: r.days1To30, strength: 45 },
          { label: '31-60 days late', value: r.days31To60, strength: 72 },
          { label: '61+ days late', value: r.days61Plus, strength: 100 },
        ]} />
      <Typography sx={{ fontSize: '12px', fontWeight: 700, color: 'text.secondary', textTransform: 'uppercase', letterSpacing: '.5px', mt: 2.5, mb: 1 }}>Top customers by amount owed</Typography>
      {r.topDebtors.length === 0
        ? <Typography sx={{ fontSize: '13px', color: 'text.secondary' }}>Every invoice is paid.</Typography>
        : r.topDebtors.map((d) => (
          <Box key={d.customerName} component="button" type="button" onClick={() => navigate('/invoices')}
            sx={{ all: 'unset', cursor: 'pointer', display: 'flex', justifyContent: 'space-between', gap: 2, py: 0.75, borderBottom: 1, borderColor: 'divider', width: '100%', '&:hover': { bgcolor: 'action.hover' } }}>
            <Box>
              <Typography sx={{ fontSize: '13px', fontWeight: 600 }}>{d.customerName}</Typography>
              <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>
                {d.invoices} {d.invoices === 1 ? 'invoice' : 'invoices'}{d.oldestDaysOverdue > 0 ? ` · oldest ${d.oldestDaysOverdue} days late` : ' · not yet due'}
              </Typography>
            </Box>
            <Typography sx={{ fontSize: '13px', fontWeight: 700, whiteSpace: 'nowrap' }}>{money(d.balance)}</Typography>
          </Box>
        ))}
    </Panel>
  );
};

const StockPanel: React.FC<{ s: NonNullable<Overview['stock']> }> = ({ s }) => (
  <Panel title="Stock by product" subtitle="Packed bags on hand, with days left at the last 30 days' dispatch rate">
    {s.products.length === 0
      ? <Typography sx={{ fontSize: '13px', color: 'text.secondary' }}>No finished goods in stock.</Typography>
      : <HBars summary="Bags in stock per product"
          rows={s.products.map((p) => ({
            label: p.productName, value: p.quantity, valueText: `${formatNumber(p.quantity, 0)} ${p.unit === 'bag' ? 'bags' : p.unit}`,
            note: p.daysLeft == null ? 'No dispatches in the last 30 days'
              : <Box component="span" sx={{ color: p.daysLeft < 7 ? 'var(--tone-error)' : p.daysLeft < 14 ? 'var(--tone-warning)' : 'inherit' }}>
                  About {p.daysLeft} {p.daysLeft === 1 ? 'day' : 'days'} left · {formatNumber(p.dispatchedPerDay, 1)} per day</Box>,
          }))} />}
    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 1.5, mt: 2.5, pt: 2, borderTop: 1, borderColor: 'divider' }}>
      <Stat label="Wheat on hand" value={`${formatNumber(s.wheatKg, 0)} kg`} />
      {s.wheatExpectedKg != null && <Stat label="Wheat on order" value={`${formatNumber(s.wheatExpectedKg, 0)} kg`}
        note={s.wheatExpectedOrders ? `${s.wheatExpectedOrders} open ${s.wheatExpectedOrders === 1 ? 'order' : 'orders'}` : 'Nothing on order'} />}
      <Stat label="Waiting to pack" value={`${formatNumber(s.bulkKg, 0)} kg`} note="Bulk, not yet in bags" />
    </Box>
  </Panel>
);

const Stat: React.FC<{ label: string; value: string; note?: string }> = ({ label, value, note }) => (
  <Box>
    <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>{label}</Typography>
    <Typography sx={{ fontSize: '17px', fontWeight: 700 }}>{value}</Typography>
    {note && <Typography sx={{ fontSize: '11.5px', color: 'text.secondary' }}>{note}</Typography>}
  </Box>
);

const WheatPanel: React.FC<{ q: NonNullable<Overview['wheatQuality']> }> = ({ q }) => {
  const rate = q.loads ? (q.rejected / q.loads) * 100 : null;
  return (
    <Panel title="Wheat quality, last 30 days" subtitle="Loads checked at QC">
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 1.5, mb: 2 }}>
        <Stat label="Loads checked" value={formatNumber(q.loads, 0)} />
        <Stat label="Rejected" value={rate == null ? '-' : `${formatNumber(rate, 1)}%`} note={`${q.rejected} ${q.rejected === 1 ? 'load' : 'loads'}`} />
        <Stat label="Avg moisture" value={pct(q.avgMoisture)} />
        <Stat label="Avg impurities" value={pct(q.avgImpurities)} />
      </Box>
      {q.bySupplier.length === 0
        ? <Typography sx={{ fontSize: '13px', color: 'text.secondary' }}>No wheat checked in the last 30 days.</Typography>
        : (
          <Box sx={{ overflowX: 'auto' }}><Box component="table" sx={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', '& th, & td': { py: 0.9, px: 1, borderBottom: 1, borderColor: 'divider', textAlign: 'right', whiteSpace: 'nowrap' }, '& th:first-of-type, & td:first-of-type': { textAlign: 'left', pl: 0 }, '& th': { fontSize: '12px', color: 'text.secondary', fontWeight: 700 } }}>
            <thead><tr><th>Supplier</th><th>Loads</th><th>Net kg</th><th>Moisture</th><th>Impurities</th><th>Rejected</th></tr></thead>
            <tbody>
              {q.bySupplier.map((s) => (
                <tr key={s.supplierName}>
                  <td style={{ fontWeight: 600 }}>{s.supplierName}</td>
                  <td>{s.loads}</td>
                  <td>{formatNumber(s.netKg, 0)}</td>
                  <td>{pct(s.avgMoisture)}</td>
                  <td>{pct(s.avgImpurities)}</td>
                  <td style={{ color: s.rejected ? 'var(--tone-error)' : undefined, fontWeight: s.rejected ? 700 : undefined }}>{s.rejected}</td>
                </tr>
              ))}
            </tbody>
          </Box></Box>
        )}
    </Panel>
  );
};

const ACTIVITY_ICON: Record<string, typeof InventoryIcon> = {
  receipt: TruckIcon, batch: FactoryIcon, purchase: CartIcon, dispatch: TruckIcon, invoice: ReceiptIcon, payment: PaymentIcon,
};

const ActivityPanel: React.FC<{ items: Activity[] }> = ({ items }) => {
  const navigate = useNavigate();
  return (
    <Panel title="Recent activity" subtitle="Latest events you have access to">
      {items.length === 0
        ? <Typography sx={{ fontSize: '13px', color: 'text.secondary' }}>Nothing recorded yet.</Typography>
        : (
          <Box sx={{ display: 'flex', flexDirection: 'column' }}>
            {items.map((a, i) => {
              const Icon = ACTIVITY_ICON[a.kind] ?? InfoIcon;
              return (
                <Box key={`${a.kind}-${a.at}-${i}`} component="button" type="button" onClick={() => navigate(a.link)}
                  sx={{ all: 'unset', cursor: 'pointer', display: 'flex', gap: 1.5, alignItems: 'flex-start', py: 1, borderBottom: i < items.length - 1 ? 1 : 0, borderColor: 'divider', '&:hover, &:focus-visible': { bgcolor: 'action.hover' } }}>
                  <Box sx={{ width: 30, height: 30, borderRadius: '8px', bgcolor: tint(ACCENT, 16), display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                    <Icon sx={{ fontSize: 17, color: 'var(--accent-text)' }} />
                  </Box>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography sx={{ fontSize: '13px', fontWeight: 600 }}>{a.title}</Typography>
                    <Typography sx={{ fontSize: '12px', color: 'text.secondary', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{a.detail}</Typography>
                  </Box>
                  <Typography sx={{ fontSize: '11.5px', color: 'text.secondary', whiteSpace: 'nowrap' }} title={new Date(a.at).toLocaleString()}>{timeAgo(a.at)}</Typography>
                </Box>
              );
            })}
          </Box>
        )}
    </Panel>
  );
};

// Reports its rendered height, so the columns can be balanced on real sizes rather than guesses.
const Slot: React.FC<{ id: string; grow: boolean; onSize: (id: string, h: number) => void; children: React.ReactNode }> = ({ id, grow, onSize, children }) => {
  const ref = useRef<HTMLDivElement | null>(null);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const inner = el.firstElementChild as HTMLElement | null;
    const observer = new ResizeObserver(() => {
      // Measure the content's natural height, not the stretched slot.
      const card = inner;
      if (!card) return;
      const { height, flex } = card.style;
      card.style.height = 'auto';
      card.style.flex = 'none';
      onSize(id, card.getBoundingClientRect().height);
      card.style.height = height;
      card.style.flex = flex;
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, [id, onSize]);
  return <Box ref={ref} sx={{ flex: grow ? 1 : 'none', display: 'flex', flexDirection: 'column', '& > *': { flex: 1 } }}>{children}</Box>;
};

const REFRESH_MS = 60_000;

const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const { data, loading, error, reload } = useFetch<Overview>('/dashboard/overview');
  const twoColumns = useMediaQuery(useTheme().breakpoints.up('lg'));
  const [sizes, setSizes] = useState<Record<string, number>>({});
  const onSize = useCallback((id: string, h: number) => {
    setSizes((prev) => (Math.abs((prev[id] ?? 0) - h) < 2 ? prev : { ...prev, [id]: h }));
  }, []);

  // Figures refresh every minute while the page is open.
  useEffect(() => {
    const id = window.setInterval(reload, REFRESH_MS);
    return () => window.clearInterval(id);
  }, [reload]);

  const asOf = data ? new Date(data.asOf) : null;

  if (!data && loading) {
    return <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '400px' }}><CircularProgress size={40} /></Box>;
  }

  const roleLabel = ROLE_LABELS[user?.role as Role] ?? user?.role ?? '';
  // Each panel with a rough height, so the two columns can be balanced (masonry) instead of rows of equal height.
  const panels: { id: string; node: React.ReactNode; h: number }[] = [];
  if (data) {
    const rows = (n: number, each: number) => n * each;
    panels.push({ id: "attention", node: <AttentionPanel key="attention" items={data.attention} checks={data.checks} />, h: 100 + rows(data.attention.length, 64) + Math.ceil(data.checks.length / 2) * 44 });
    if (data.sales) panels.push({ id: "sales", node: <SalesPanel key="sales" sales={data.sales} />, h: 300 });
    if (data.production) panels.push({ id: "milling", node: <MillingPanel key="milling" production={data.production} />, h: 300 });
    if (data.receivables) panels.push({ id: "receivables", node: <ReceivablesPanel key="receivables" r={data.receivables} />, h: 230 + rows(Math.max(1, data.receivables.topDebtors.length), 46) });
    if (data.production) panels.push({ id: "yield", node: <YieldPanel key="yield" production={data.production} />, h: 300 });
    if (data.stock) panels.push({ id: "stock", node: <StockPanel key="stock" s={data.stock} />, h: 190 + rows(data.stock.products.length, 66) });
    if (data.wheatQuality) panels.push({ id: "wheat", node: <WheatPanel key="wheat" q={data.wheatQuality} />, h: 200 + rows(Math.max(1, data.wheatQuality.bySupplier.length), 40) });
    panels.push({ id: "activity", node: <ActivityPanel key="activity" items={data.activity} />, h: 100 + rows(Math.max(1, data.activity.length), 51) });
  }
  const columns: { id: string; node: React.ReactNode }[][] = [[], []];
  if (twoColumns) {
    // At most 8 panels, so try every left/right split (attention stays top-left; order within a column follows
    // priority) and keep the one whose columns end closest together.
    const h = panels.map((p) => (sizes[p.id] ?? p.h) + 16);
    let best = 0, bestDiff = Infinity;
    for (let mask = 0; mask < 1 << panels.length; mask += 2) {
      let left = 0, right = 0;
      h.forEach((v, i) => { if (mask & (1 << i)) right += v; else left += v; });
      if (Math.abs(left - right) < bestDiff) { bestDiff = Math.abs(left - right); best = mask; }
    }
    panels.forEach((p, i) => columns[best & (1 << i) ? 1 : 0].push(p));
  } else {
    columns[0] = panels;
  }
  // KPI cards fill the row: 2 cards -> halves, 4 -> quarters, otherwise thirds.
  const kpiCount = data?.kpis.length ?? 0;
  const kpiLg = kpiCount === 1 ? 12 : kpiCount === 2 ? 6 : kpiCount === 4 ? 3 : 4;

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
        <Box>
          <Typography sx={{ fontSize: '28px', fontWeight: 700, letterSpacing: '-0.5px', color: 'text.primary', mb: 0.5 }}>Dashboard</Typography>
          <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>
            {asOf && asOf.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}
            {roleLabel && `${asOf ? ' · ' : ''}${roleLabel} view`}
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          {asOf && <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>Updated {asOf.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}</Typography>}
          <Button size="small" variant="outlined" startIcon={loading ? <CircularProgress size={14} /> : <RefreshIcon />} onClick={reload} disabled={loading}>Refresh</Button>
        </Box>
      </Box>

      {error && <Alert severity="warning" sx={{ mb: 3 }}>{error}</Alert>}

      {data && (
        <>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            {data.kpis.map((k) => (
              <Grid size={{ xs: 12, sm: kpiCount === 1 ? 12 : 6, lg: kpiLg }} key={k.key}><KpiCard kpi={k} /></Grid>
            ))}
          </Grid>
          <Box sx={{ display: 'flex', gap: 2, alignItems: 'stretch' }}>
            {columns.filter((c) => c.length).map((col, i) => (
              <Box key={i} sx={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2 }}>
                {col.map((p, j) => <Slot key={p.id} id={p.id} grow={twoColumns && j === col.length - 1} onSize={onSize}>{p.node}</Slot>)}
              </Box>
            ))}
          </Box>
        </>
      )}
    </Box>
  );
};

export default Dashboard;
