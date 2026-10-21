import React from 'react';
import { Box, Button, IconButton, MenuItem, TextField, Tooltip, Typography } from '@mui/material';
import { Add as AddIcon, Close as CloseIcon } from '@mui/icons-material';
import type { Product } from '../api/types';
import { formatMoney } from '../utils/format';

import { emptyLine, lineTotal, type LineDraft } from '../utils/lineItems';

interface LineItemsEditorProps {
  lines: LineDraft[];
  products: Product[];
  quantityLabel: string;
  onChange: (lines: LineDraft[]) => void;
}

const LineItemsEditor: React.FC<LineItemsEditorProps> = ({ lines, products, quantityLabel, onChange }) => {
  const update = (idx: number, patch: Partial<LineDraft>) => onChange(lines.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  const total = lines.reduce((sum, l) => sum + lineTotal(l), 0);

  return (
    <Box>
      <Typography sx={{ fontSize: '13px', fontWeight: 600, color: 'text.primary', mb: 1 }}>Items</Typography>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        {lines.map((line, idx) => {
          const unit = products.find((p) => String(p.productID) === line.productID)?.unit;
          return (
            <Box
              key={idx}
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr 1fr', sm: '2.2fr 1fr 1fr 1fr 40px' },
                gap: 1,
                alignItems: 'center',
              }}
            >
              <TextField
                select
                size="small"
                label="Product"
                value={line.productID}
                onChange={(e) => update(idx, { productID: e.target.value })}
                sx={{ gridColumn: { xs: '1 / -1', sm: 'auto' } }}
              >
                {products.length === 0 && (
                  <MenuItem disabled value="">
                    No products available. Add products first.
                  </MenuItem>
                )}
                {products.map((p) => (
                  <MenuItem key={p.productID} value={String(p.productID)}>
                    {p.productName} ({p.productCode})
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                size="small"
                type="number"
                label={unit ? `${quantityLabel} (${unit})` : quantityLabel}
                value={line.quantity}
                onChange={(e) => update(idx, { quantity: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 'any' } }}
              />
              <TextField
                size="small"
                type="number"
                label="Rate (PKR)"
                value={line.ratePerUnit}
                onChange={(e) => update(idx, { ratePerUnit: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 'any' } }}
              />
              <Typography sx={{ fontSize: '13px', fontWeight: 600, textAlign: { sm: 'right' }, color: 'text.primary' }}>
                {formatMoney(lineTotal(line))}
              </Typography>
              <Tooltip title="Remove item">
                <span>
                  <IconButton
                    size="small"
                    disabled={lines.length === 1}
                    onClick={() => onChange(lines.filter((_, i) => i !== idx))}
                    aria-label="Remove item"
                  >
                    <CloseIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
            </Box>
          );
        })}
      </Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mt: 1.5 }}>
        <Button size="small" startIcon={<AddIcon />} onClick={() => onChange([...lines, emptyLine()])}>
          Add item
        </Button>
        <Typography sx={{ fontSize: '15px', fontWeight: 700, color: 'text.primary' }}>Total {formatMoney(total)}</Typography>
      </Box>
    </Box>
  );
};

export default LineItemsEditor;
