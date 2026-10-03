import React from 'react';
import { Box } from '@mui/material';

const BARS = [
  { x: 186, h: 34 },
  { x: 206, h: 52 },
  { x: 226, h: 40 },
  { x: 246, h: 62 },
  { x: 266, h: 48 },
  { x: 286, h: 70 },
  { x: 306, h: 58 },
  { x: 326, h: 76 },
];

const MODULES = [
  { x: 172, label: 'Inventory', color: 'var(--ill-accent)' },
  { x: 275, label: 'Finance', color: 'var(--ill-green)' },
  { x: 378, label: 'Production', color: 'var(--ill-amber)' },
];

const LoginIllustration: React.FC = () => (
  <Box
    sx={(theme) => ({
      width: '100%',
      maxWidth: 460,
      '--ill-frame': '#18181b',
      '--ill-screen': '#ffffff',
      '--ill-panel': '#f4f4f5',
      '--ill-line': '#e4e4e7',
      '--ill-muted': '#d4d4d8',
      '--ill-text': '#18181b',
      '--ill-accent': '#eab308',
      '--ill-accent-soft': '#fde68a',
      '--ill-green': '#16a34a',
      '--ill-amber': '#f97316',
      '--ill-jacket': '#facc15',
      '--ill-jacket-dark': '#eab308',
      '--ill-pants': '#27272a',
      '--ill-skin': '#f2c4a0',
      '--ill-hair': '#18181b',
      '--ill-shadow': 'rgba(24, 24, 27, 0.08)',
      '--ill-leaf': '#4ade80',
      ...theme.applyStyles('dark', {
        '--ill-frame': '#3f3f46',
        '--ill-screen': '#18181b',
        '--ill-panel': '#0f0f0f',
        '--ill-line': '#27272a',
        '--ill-muted': '#3f3f46',
        '--ill-text': '#fafafa',
        '--ill-accent': '#facc15',
        '--ill-accent-soft': '#3f3410',
        '--ill-green': '#4ade80',
        '--ill-amber': '#fb923c',
        '--ill-jacket': '#facc15',
        '--ill-jacket-dark': '#ca8a04',
        '--ill-pants': '#71717a',
        '--ill-hair': '#3f3f46',
        '--ill-shadow': 'rgba(0, 0, 0, 0.4)',
      }),
      '@keyframes ill-grow': { from: { transform: 'scaleY(0)' }, to: { transform: 'scaleY(1)' } },
      '@keyframes ill-draw': { from: { strokeDashoffset: 400 }, to: { strokeDashoffset: 0 } },
      '@keyframes ill-rise': { from: { opacity: 0, transform: 'translateY(8px)' }, to: { opacity: 1, transform: 'translateY(0)' } },
      '@media (prefers-reduced-motion: no-preference)': {
        '& .ill-bar': {
          transformBox: 'fill-box',
          transformOrigin: 'bottom',
          animation: 'ill-grow 0.7s cubic-bezier(0.16, 1, 0.3, 1) both',
        },
        '& .ill-trend': { strokeDasharray: 400, animation: 'ill-draw 1.2s 0.4s cubic-bezier(0.16, 1, 0.3, 1) both' },
        '& .ill-card': { animation: 'ill-rise 0.6s cubic-bezier(0.16, 1, 0.3, 1) both' },
      },
    })}
  >
    <svg viewBox="0 0 520 400" width="100%" role="img" aria-labelledby="ill-title">
      <title id="ill-title">A person presenting an ERP dashboard with charts and module cards</title>

      <ellipse cx="280" cy="372" rx="230" ry="14" fill="var(--ill-shadow)" />

      {/* monitor */}
      <rect x="150" y="40" width="340" height="230" rx="14" fill="var(--ill-frame)" />
      <rect x="160" y="50" width="320" height="210" rx="7" fill="var(--ill-screen)" />
      <rect x="306" y="270" width="28" height="56" fill="var(--ill-frame)" />
      <rect x="262" y="322" width="116" height="12" rx="6" fill="var(--ill-frame)" />

      {/* screen top bar */}
      <rect x="160" y="50" width="320" height="20" rx="7" fill="var(--ill-panel)" />
      <rect x="160" y="62" width="320" height="8" fill="var(--ill-panel)" />
      <circle cx="173" cy="60" r="3.5" fill="#f87171" />
      <circle cx="184" cy="60" r="3.5" fill="#fbbf24" />
      <circle cx="195" cy="60" r="3.5" fill="#34d399" />
      <rect x="400" y="56" width="66" height="8" rx="4" fill="var(--ill-line)" />

      {/* bar + trend chart */}
      <rect x="172" y="80" width="178" height="96" rx="7" fill="var(--ill-panel)" />
      <rect x="182" y="88" width="56" height="6" rx="3" fill="var(--ill-muted)" />
      {BARS.map((b, i) => (
        <rect
          key={b.x}
          className="ill-bar"
          x={b.x}
          y={168 - b.h}
          width="12"
          height={b.h}
          rx="3"
          fill={i === BARS.length - 1 ? 'var(--ill-accent)' : 'var(--ill-accent-soft)'}
          style={{ animationDelay: `${0.1 + i * 0.06}s` }}
        />
      ))}
      <polyline
        className="ill-trend"
        points="192,128 212,114 232,120 252,102 272,108 292,92 312,96 332,82"
        fill="none"
        stroke="var(--ill-accent)"
        strokeWidth="3"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="332" cy="82" r="4" fill="var(--ill-screen)" stroke="var(--ill-accent)" strokeWidth="2.5" />

      {/* donut chart */}
      <rect x="360" y="80" width="108" height="96" rx="7" fill="var(--ill-panel)" />
      <g transform="rotate(-90 414 128)">
        <circle cx="414" cy="128" r="26" fill="none" stroke="var(--ill-line)" strokeWidth="11" />
        <circle cx="414" cy="128" r="26" fill="none" stroke="var(--ill-accent)" strokeWidth="11" strokeDasharray="78 163.4" />
        <circle cx="414" cy="128" r="26" fill="none" stroke="var(--ill-green)" strokeWidth="11" strokeDasharray="44 163.4" strokeDashoffset="-80" />
        <circle cx="414" cy="128" r="26" fill="none" stroke="var(--ill-amber)" strokeWidth="11" strokeDasharray="28 163.4" strokeDashoffset="-126" />
      </g>

      {/* module cards */}
      {MODULES.map((m, i) => (
        <g key={m.label} className="ill-card" style={{ animationDelay: `${0.5 + i * 0.12}s` }}>
          <rect x={m.x} y="186" width="90" height="62" rx="7" fill="var(--ill-panel)" />
          <rect x={m.x + 10} y="196" width="18" height="18" rx="5" fill={m.color} />
          <rect x={m.x + 14} y="202" width="10" height="6" rx="1.5" fill="var(--ill-screen)" opacity="0.9" />
          <text x={m.x + 10} y="230" fontSize="10.5" fontWeight="700" fill="var(--ill-text)" fontFamily="Segoe UI, Roboto, sans-serif">
            {m.label}
          </text>
          <rect x={m.x + 10} y="237" width={40 + i * 8} height="4" rx="2" fill="var(--ill-muted)" />
        </g>
      ))}

      {/* person */}
      <rect x="62" y="350" width="30" height="11" rx="5.5" fill="var(--ill-hair)" />
      <rect x="99" y="350" width="30" height="11" rx="5.5" fill="var(--ill-hair)" />
      <path d="M70 226 L74 352 L90 352 L95 232 Z" fill="var(--ill-pants)" />
      <path d="M95 232 L102 352 L118 352 L122 226 Z" fill="var(--ill-pants)" />
      <path d="M66 145 L56 214 L67 217 L80 152 Z" fill="var(--ill-jacket-dark)" />
      <circle cx="61" cy="220" r="6.5" fill="var(--ill-skin)" />
      <path d="M68 142 Q95 130 122 142 L127 232 L64 232 Z" fill="var(--ill-jacket)" />
      <path d="M88 136 L95 152 L102 136 Z" fill="var(--ill-screen)" />
      <path d="M117 148 L150 126" stroke="var(--ill-jacket-dark)" strokeWidth="14" strokeLinecap="round" />
      <path d="M150 126 L168 110" stroke="var(--ill-jacket)" strokeWidth="12" strokeLinecap="round" />
      <circle cx="172" cy="106" r="6.5" fill="var(--ill-skin)" />
      <rect x="89" y="124" width="12" height="13" rx="3" fill="var(--ill-skin)" />
      <circle cx="95" cy="110" r="19" fill="var(--ill-skin)" />
      <path d="M76 112 A19 19 0 0 1 114 106 Q103 96 91 97 Q80 99 76 112 Z" fill="var(--ill-hair)" />
      <path d="M76 112 Q74 100 84 93 Q97 86 108 92 Q116 97 114 106 Q101 98 88 101 Q79 104 76 112 Z" fill="var(--ill-hair)" />

      {/* plant */}
      <path d="M455 300 Q440 270 450 250 Q462 274 460 300 Z" fill="var(--ill-leaf)" />
      <path d="M465 300 Q480 266 498 258 Q492 284 470 302 Z" fill="var(--ill-green)" />
      <path d="M460 300 Q462 262 474 244 Q478 276 468 300 Z" fill="var(--ill-leaf)" opacity="0.85" />
      <path d="M446 298 L486 298 L480 334 L452 334 Z" fill="var(--ill-frame)" />
    </svg>
  </Box>
);

export default LoginIllustration;
