import React, { useState } from 'react';
import { IconButton, Menu, MenuItem, ListItemIcon, ListItemText, Tooltip } from '@mui/material';
import { useColorScheme } from '@mui/material/styles';
import {
  LightMode as LightModeIcon,
  DarkMode as DarkModeIcon,
  SettingsBrightness as SystemModeIcon,
} from '@mui/icons-material';

type Mode = 'light' | 'dark' | 'system';

const OPTIONS: { mode: Mode; label: string; icon: React.ReactNode }[] = [
  { mode: 'light', label: 'Light', icon: <LightModeIcon fontSize="small" /> },
  { mode: 'dark', label: 'Dark', icon: <DarkModeIcon fontSize="small" /> },
  { mode: 'system', label: 'System', icon: <SystemModeIcon fontSize="small" /> },
];

const ThemeToggle: React.FC = () => {
  const { mode, systemMode, setMode } = useColorScheme();
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);

  if (!mode) return null;

  const resolved = mode === 'system' ? systemMode : mode;

  return (
    <>
      <Tooltip title="Theme">
        <IconButton
          onClick={(e) => setAnchorEl(e.currentTarget)}
          aria-label="Change theme"
          sx={{ color: 'text.primary' }}
        >
          {resolved === 'dark' ? <DarkModeIcon fontSize="small" /> : <LightModeIcon fontSize="small" />}
        </IconButton>
      </Tooltip>
      <Menu
        anchorEl={anchorEl}
        open={!!anchorEl}
        onClose={() => setAnchorEl(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { borderRadius: '8px', border: 1, borderColor: 'divider', mt: 1, minWidth: 160 } } }}
      >
        {OPTIONS.map((opt) => (
          <MenuItem
            key={opt.mode}
            selected={mode === opt.mode}
            onClick={() => {
              setMode(opt.mode);
              setAnchorEl(null);
            }}
            sx={{ fontSize: '13px' }}
          >
            <ListItemIcon>{opt.icon}</ListItemIcon>
            <ListItemText primary={opt.label} slotProps={{ primary: { sx: { fontSize: '13px' } } }} />
          </MenuItem>
        ))}
      </Menu>
    </>
  );
};

export default ThemeToggle;
