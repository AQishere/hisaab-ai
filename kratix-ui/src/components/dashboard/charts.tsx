import React, { useLayoutEffect, useRef, useState } from 'react';
import { Box, Typography } from '@mui/material';

// Small SVG charts for the dashboard. One series and one y-axis each; theme colours come from CSS variables
// so light and dark mode both work. Every chart has a hover tooltip and an aria-label summary.

const SERIES = 'var(--accent-border)';
const GRID = 'var(--mui-palette-divider)';
const MUTED = 'var(--mui-palette-text-secondary)';
const SURFACE = 'var(--mui-palette-background-paper)';

export interface ChartPoint {
  label: string;          // x-axis label (short date)
  value: number | null;   // null = no data that day (a gap, not a zero)
  tip: React.ReactNode;   // tooltip body
}

function useWidth<T extends HTMLElement>() {
  const ref = useRef<T | null>(null);
  const [width, setWidth] = useState(0);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => setWidth(Math.floor(entry.contentRect.width)));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);
  return [ref, width] as const;
}

function niceTicks(min: number, max: number, count = 4): number[] {
  if (max <= min) max = min + 1;
  const raw = (max - min) / count;
  const mag = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * mag).find((s) => s >= raw) ?? raw;
  const start = Math.floor(min / step) * step;
  const ticks: number[] = [];
  for (let v = start; v <= max + step * 0.001; v += step) ticks.push(+v.toFixed(6));
  if (ticks[ticks.length - 1] < max) ticks.push(+(ticks[ticks.length - 1] + step).toFixed(6));
  return ticks;
}

const PAD = { left: 48, right: 14, top: 12, bottom: 26 };

const Tooltip: React.FC<{ x: number; y: number; width: number; children: React.ReactNode }> = ({ x, y, width, children }) => (
  <Box
    role="tooltip"
    sx={{
      position: 'absolute', top: Math.max(0, y - 12), left: x, pointerEvents: 'none', zIndex: 2,
      transform: x > width * 0.6 ? 'translate(calc(-100% - 12px), -100%)' : 'translate(12px, -100%)',
      bgcolor: 'background.paper', border: 1, borderColor: 'divider', borderRadius: '8px', px: 1.25, py: 0.75,
      boxShadow: '0 4px 14px rgba(0,0,0,.18)', fontSize: '12px', whiteSpace: 'nowrap',
    }}
  >
    {children}
  </Box>
);

interface Frame { width: number; height: number; x: (i: number) => number; y: (v: number) => number; ticks: number[]; band: number }

function frame(width: number, height: number, n: number, domain: [number, number], centred: boolean): Frame {
  const plotW = Math.max(10, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const ticks = niceTicks(domain[0], domain[1]);
  const lo = ticks[0], hi = ticks[ticks.length - 1];
  const band = plotW / Math.max(1, centred ? n : n - 1);
  return {
    width, height, ticks, band,
    x: (i) => PAD.left + (centred ? band * (i + 0.5) : band * i),
    y: (v) => PAD.top + plotH - ((v - lo) / (hi - lo || 1)) * plotH,
  };
}

// Date labels counted back from the latest day, about one per 60px, so they never collide at any width.
const labelStep = (f: Frame, n: number) => Math.max(1, Math.ceil(n / Math.max(2, Math.floor((f.width - PAD.left - PAD.right) / 60))));

const Axes: React.FC<{ f: Frame; points: ChartPoint[]; format: (v: number) => string }> = ({ f, points, format }) => {
  const every = labelStep(f, points.length);
  return (
  <g>
    {f.ticks.map((t) => (
      <g key={t}>
        <line x1={PAD.left} x2={f.width - PAD.right} y1={f.y(t)} y2={f.y(t)} stroke={GRID} strokeWidth={1} />
        <text x={PAD.left - 8} y={f.y(t)} dy="0.32em" textAnchor="end" fontSize={11} fill={MUTED}>{format(t)}</text>
      </g>
    ))}
    {points.map((p, i) => (points.length - 1 - i) % every === 0 && (
      <text key={i} x={f.x(i)} y={f.height - 6} textAnchor="middle" fontSize={11} fill={MUTED}>{p.label}</text>
    ))}
  </g>
  );
};

function useHover(points: ChartPoint[], f: Frame | null, centred: boolean) {
  const [index, setIndex] = useState<number | null>(null);
  const onMove = (e: React.MouseEvent<SVGRectElement>) => {
    if (!f) return;
    const rect = e.currentTarget.ownerSVGElement!.getBoundingClientRect();
    const px = e.clientX - rect.left - PAD.left;
    const i = Math.round(centred ? px / f.band - 0.5 : px / f.band);
    setIndex(Math.min(points.length - 1, Math.max(0, i)));
  };
  return { index, onMove, onLeave: () => setIndex(null) };
}

// Line (optionally with a soft area) for change over time. Null values break the line.
export const LineChart: React.FC<{
  points: ChartPoint[]; height?: number; format: (v: number) => string; area?: boolean;
  domain?: [number, number]; reference?: { value: number; label: string }; summary: string;
}> = ({ points, height = 200, format, area, domain, reference, summary }) => {
  const [ref, width] = useWidth<HTMLDivElement>();
  const values = points.map((p) => p.value).filter((v): v is number => v != null);
  const dom: [number, number] = domain ?? [0, Math.max(1, ...values)];
  const f = width ? frame(width, height, points.length, dom, false) : null;
  const { index, onMove, onLeave } = useHover(points, f, false);

  let line = '', fill = '';
  if (f) {
    let open = false, segStart = 0;
    points.forEach((p, i) => {
      if (p.value == null) {
        if (open && area) fill += `L${f.x(i - 1)},${f.y(f.ticks[0])}L${f.x(segStart)},${f.y(f.ticks[0])}Z`;
        open = false;
        return;
      }
      const cmd = open ? 'L' : 'M';
      if (!open) segStart = i;
      line += `${cmd}${f.x(i)},${f.y(p.value)}`;
      fill += `${cmd}${f.x(i)},${f.y(p.value)}`;
      open = true;
    });
    if (open && area) fill += `L${f.x(points.length - 1)},${f.y(f.ticks[0])}L${f.x(segStart)},${f.y(f.ticks[0])}Z`;
  }
  const hp = index != null ? points[index] : null;

  return (
    <Box ref={ref} sx={{ position: 'relative', width: '100%', height }}>
      {f && (
        <svg width={f.width} height={height} role="img" aria-label={summary} style={{ display: 'block', overflow: 'visible' }}>
          <Axes f={f} points={points} format={format} />
          {area && <path d={fill} fill={SERIES} opacity={0.12} />}
          {reference && (
            <g>
              <line x1={PAD.left} x2={f.width - PAD.right} y1={f.y(reference.value)} y2={f.y(reference.value)} stroke={MUTED} strokeWidth={1} strokeDasharray="4 4" />
              <text x={f.width - PAD.right} y={f.y(reference.value) - 6} textAnchor="end" fontSize={11} fill={MUTED}>{reference.label}</text>
            </g>
          )}
          <path d={line} fill="none" stroke={SERIES} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
          {points.map((p, i) => p.value != null && (points[i - 1]?.value == null && points[i + 1]?.value == null) && (
            <circle key={i} cx={f.x(i)} cy={f.y(p.value)} r={4} fill={SERIES} stroke={SURFACE} strokeWidth={2} />
          ))}
          {hp && index != null && (
            <g>
              <line x1={f.x(index)} x2={f.x(index)} y1={PAD.top} y2={height - PAD.bottom} stroke={MUTED} strokeWidth={1} />
              {hp.value != null && <circle cx={f.x(index)} cy={f.y(hp.value)} r={5} fill={SERIES} stroke={SURFACE} strokeWidth={2} />}
            </g>
          )}
          <rect x={PAD.left} y={0} width={Math.max(0, f.width - PAD.left - PAD.right)} height={height} fill="transparent" onMouseMove={onMove} onMouseLeave={onLeave} />
        </svg>
      )}
      {f && hp && index != null && <Tooltip x={f.x(index)} y={hp.value != null ? f.y(hp.value) : PAD.top + 20} width={f.width}>{hp.tip}</Tooltip>}
    </Box>
  );
};

// Columns for amounts per day. Rounded tops, anchored to the baseline, 2px gap at minimum.
export const ColumnChart: React.FC<{ points: ChartPoint[]; height?: number; format: (v: number) => string; summary: string }> = ({
  points, height = 200, format, summary,
}) => {
  const [ref, width] = useWidth<HTMLDivElement>();
  const max = Math.max(1, ...points.map((p) => p.value ?? 0));
  const f = width ? frame(width, height, points.length, [0, max], true) : null;
  const { index, onMove, onLeave } = useHover(points, f, true);
  const hp = index != null ? points[index] : null;

  return (
    <Box ref={ref} sx={{ position: 'relative', width: '100%', height }}>
      {f && (
        <svg width={f.width} height={height} role="img" aria-label={summary} style={{ display: 'block' }}>
          <Axes f={f} points={points} format={format} />
          {index != null && <rect x={f.x(index) - f.band / 2} y={PAD.top} width={f.band} height={height - PAD.top - PAD.bottom} fill={MUTED} opacity={0.08} />}
          {points.map((p, i) => {
            if (!p.value) return null;
            const w = Math.max(2, Math.min(28, f.band - 2, f.band * 0.62));
            const x = f.x(i) - w / 2, y = f.y(p.value), base = f.y(0), r = Math.min(4, w / 2, base - y);
            return <path key={i} d={`M${x},${base}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${base}Z`} fill={SERIES} />;
          })}
          <rect x={PAD.left} y={0} width={Math.max(0, f.width - PAD.left - PAD.right)} height={height} fill="transparent" onMouseMove={onMove} onMouseLeave={onLeave} />
        </svg>
      )}
      {f && hp && index != null && <Tooltip x={f.x(index)} y={f.y(hp.value ?? 0)} width={f.width}>{hp.tip}</Tooltip>}
    </Box>
  );
};

// One horizontal bar split into ordered parts (for example receivables by age). Sequential steps of the series
// colour, a 2px surface gap between parts, and a legend with values so colour is never the only cue.
export const SegmentBar: React.FC<{ parts: { label: string; value: number; strength: number }[]; format: (v: number) => string; summary: string }> = ({
  parts, format, summary,
}) => {
  const total = parts.reduce((s, p) => s + p.value, 0);
  const [hover, setHover] = useState<number | null>(null);
  const colour = (strength: number) => strength === 0 ? 'var(--mui-palette-text-disabled)' : `color-mix(in srgb, ${SERIES} ${strength}%, ${SURFACE})`;
  return (
    <Box>
      <Box role="img" aria-label={summary} sx={{ display: 'flex', gap: '2px', height: 14, borderRadius: '4px', overflow: 'hidden', bgcolor: 'divider' }}>
        {total > 0 && parts.map((p, i) => p.value > 0 && (
          <Box key={p.label} onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)}
            title={`${p.label}: ${format(p.value)}`}
            sx={{ flex: `${p.value} 0 0`, bgcolor: colour(p.strength), opacity: hover == null || hover === i ? 1 : 0.55, transition: 'opacity .15s' }} />
        ))}
      </Box>
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 1, mt: 1.5 }}>
        {parts.map((p, i) => (
          <Box key={p.label} onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)} sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
            <Box sx={{ width: 10, height: 10, borderRadius: '3px', bgcolor: colour(p.strength), mt: '4px', flexShrink: 0 }} />
            <Box>
              <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>{p.label}</Typography>
              <Typography sx={{ fontSize: '14px', fontWeight: 600 }}>{format(p.value)}</Typography>
            </Box>
          </Box>
        ))}
      </Box>
    </Box>
  );
};

// Horizontal bars for comparing products. Labels and values in text ink; the bar carries the magnitude.
export const HBars: React.FC<{ rows: { label: string; value: number; valueText: string; note?: React.ReactNode }[]; summary: string }> = ({ rows, summary }) => {
  const max = Math.max(1, ...rows.map((r) => r.value));
  return (
    <Box role="img" aria-label={summary} sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      {rows.map((r) => (
        <Box key={r.label} title={`${r.label}: ${r.valueText}`}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, mb: 0.5 }}>
            <Typography sx={{ fontSize: '13px', fontWeight: 600 }}>{r.label}</Typography>
            <Typography sx={{ fontSize: '13px', fontWeight: 600, whiteSpace: 'nowrap' }}>{r.valueText}</Typography>
          </Box>
          <Box sx={{ height: 8, borderRadius: '4px', bgcolor: 'divider', overflow: 'hidden' }}>
            <Box sx={{ width: `${(r.value / max) * 100}%`, height: '100%', borderRadius: '4px', bgcolor: SERIES }} />
          </Box>
          {r.note && <Typography component="div" sx={{ fontSize: '12px', color: 'text.secondary', mt: 0.5 }}>{r.note}</Typography>}
        </Box>
      ))}
    </Box>
  );
};
