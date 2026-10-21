import React from 'react';
import { Box, InputAdornment, TextField } from '@mui/material';
import { Search as SearchIcon } from '@mui/icons-material';

interface ListToolbarProps {
  search: string;
  onSearch: (value: string) => void;
  placeholder: string;
  children?: React.ReactNode; // filters shown next to the search box
}

const ListToolbar: React.FC<ListToolbarProps> = ({ search, onSearch, placeholder, children }) => (
  <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1.5, mb: 2 }}>
    <TextField
      size="small"
      value={search}
      onChange={(e) => onSearch(e.target.value)}
      placeholder={placeholder}
      aria-label={placeholder}
      sx={{ width: { xs: '100%', sm: 340 } }}
      slotProps={{
        input: {
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon fontSize="small" sx={{ color: 'text.secondary' }} />
            </InputAdornment>
          ),
        },
      }}
    />
    {children}
  </Box>
);

export default ListToolbar;
