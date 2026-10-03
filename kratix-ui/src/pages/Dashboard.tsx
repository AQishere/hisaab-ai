import React, { useEffect, useState } from 'react';
import { Box, Grid, Card, Typography, CircularProgress, Alert } from '@mui/material';
import {
  Inventory as InventoryIcon,
  TrendingUp as TrendingUpIcon,
  ShoppingCart as ShoppingCartIcon,
  Payment as PaymentIcon,
} from '@mui/icons-material';
import client from '../api/client';

interface DashboardMetrics {
  wheatStockKg: number;
  flourStockBags: number;
  todayMillingKg: number;
  todayYieldPercent: number;
  pendingPOs: number;
  salesToday: number;
  outstandingReceivables: number;
}

interface KPICard {
  title: string;
  value: string;
  unit: string;
  Icon: typeof InventoryIcon;
}

const tint = (color: string, pct: number) => `color-mix(in srgb, ${color} ${pct}%, transparent)`;

const ACCENT = 'var(--mui-palette-primary-main)';

const KpiCard: React.FC<KPICard> = ({ title, value, unit, Icon }) => (
  <Card
    sx={{
      height: '100%',
      bgcolor: 'background.paper',
      p: '20px',
      '&:hover': { borderColor: ACCENT, boxShadow: `0 4px 12px ${tint(ACCENT, 18)}` },
    }}
  >
    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 2 }}>
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Typography
          sx={{ fontSize: '12px', fontWeight: 600, color: 'text.secondary', mb: 1.5, textTransform: 'uppercase', letterSpacing: '0.5px' }}
        >
          {title}
        </Typography>
        <Typography sx={{ fontSize: '32px', fontWeight: 700, color: 'text.primary', lineHeight: 1 }}>{value}</Typography>
        {unit && (
          <Typography sx={{ fontSize: '12px', color: 'text.secondary', mt: 1, fontWeight: 500 }}>{unit}</Typography>
        )}
      </Box>
      <Box
        sx={{
          width: 56,
          height: 56,
          borderRadius: '12px',
          bgcolor: tint(ACCENT, 22),
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
        }}
      >
        <Icon sx={{ fontSize: 28, color: 'var(--accent-text)' }} />
      </Box>
    </Box>
  </Card>
);

const Dashboard: React.FC = () => {
  const [metrics, setMetrics] = useState<DashboardMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchMetrics = async () => {
      try {
        const response = await client.get('/dashboard/metrics');
        setMetrics(response.data);
      } catch (err: any) {
        setError(err.response?.data?.message || 'Failed to load dashboard metrics');
      } finally {
        setLoading(false);
      }
    };

    fetchMetrics();
  }, []);

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '400px' }}>
        <CircularProgress size={40} />
      </Box>
    );
  }

  const cards: KPICard[] = [
    {
      title: 'Wheat Stock',
      value: (metrics?.wheatStockKg ?? 0).toLocaleString(),
      unit: 'kg',
      Icon: InventoryIcon,
    },
    {
      title: 'Flour Stock',
      value: (metrics?.flourStockBags ?? 0).toLocaleString(),
      unit: 'bags',
      Icon: ShoppingCartIcon,
    },
    {
      title: "Today's Milling",
      value: (metrics?.todayMillingKg ?? 0).toLocaleString(),
      unit: 'kg',
      Icon: TrendingUpIcon,
    },
    {
      title: "Today's Yield",
      value: (metrics?.todayYieldPercent ?? 0).toFixed(1),
      unit: '%',
      Icon: TrendingUpIcon,
    },
    {
      title: 'Pending POs',
      value: String(metrics?.pendingPOs ?? 0),
      unit: 'orders',
      Icon: ShoppingCartIcon,
    },
    {
      title: 'Sales Today',
      value: `PKR ${(metrics?.salesToday ?? 0).toLocaleString()}`,
      unit: '',
      Icon: PaymentIcon,
    },
  ];

  return (
    <Box>
      <Box sx={{ mb: 4 }}>
        <Typography sx={{ fontSize: '28px', fontWeight: 700, letterSpacing: '-0.5px', color: 'text.primary', mb: 0.5 }}>
          Dashboard
        </Typography>
        <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>Overview of key metrics and operations</Typography>
      </Box>

      {error && (
        <Alert severity="warning" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      <Grid container spacing={2} sx={{ mb: 2 }}>
        {cards.map((card) => (
          <Grid size={{ xs: 12, sm: 6, lg: 4 }} key={card.title}>
            <KpiCard {...card} />
          </Grid>
        ))}
      </Grid>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 6 }}>
          <KpiCard
            title="Outstanding Receivables"
            value={`PKR ${(metrics?.outstandingReceivables ?? 0).toLocaleString()}`}
            unit="Pending payments"
            Icon={PaymentIcon}
          />
        </Grid>
      </Grid>

      <Box
        sx={{
          mt: 6,
          p: 2,
          bgcolor: tint('var(--mui-palette-primary-main)', 8),
          borderLeft: '4px solid',
          borderColor: 'primary.main',
          borderRadius: '8px',
        }}
      >
        <Typography sx={{ fontSize: '13px', color: 'text.primary', fontWeight: 500, lineHeight: 1.6 }}>
          Figures update each time this page loads. Use the sidebar to open each module.
        </Typography>
      </Box>
    </Box>
  );
};

export default Dashboard;
