import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, TextField, Button, Typography, Alert, CircularProgress, Tooltip, Checkbox, FormControlLabel } from '@mui/material';
import { useAuth } from '../context/AuthContext';
import client from '../api/client';
import ThemeToggle from '../components/ThemeToggle';
import LoginIllustration from '../components/LoginIllustration';
import BrandLogo from '../components/BrandLogo';
import { homePath } from '../auth/permissions';
import { session } from '../auth/session';
import { DEMO_ACCOUNTS } from '../config/demoAccounts';
import { getErrorMessage } from '../utils/format';


const ACCENT_TEXT = 'var(--accent-text)';
const HEADLINE = 'var(--headline)';

const labelSx = {
  display: 'block',
  fontSize: '12px',
  fontWeight: 600,
  color: 'text.primary',
  mb: 1,
} as const;

const inputSx = {
  '& .MuiOutlinedInput-root': {
    bgcolor: 'background.paper',
    '&.Mui-focused': {
      boxShadow: '0 0 0 3px color-mix(in srgb, var(--mui-palette-primary-main) 20%, transparent)',
    },
  },
  '& .MuiOutlinedInput-input::placeholder': { color: 'text.secondary', opacity: 1 },
} as const;

const BackdropArcs: React.FC = () => (
  <Box
    component="svg"
    aria-hidden
    viewBox="0 0 1440 900"
    preserveAspectRatio="xMidYMid slice"
    sx={(theme) => ({
      position: 'absolute',
      inset: 0,
      width: '100%',
      height: '100%',
      pointerEvents: 'none',
      '--arc-strong': '#18181b',
      '--arc-soft': 'rgba(255, 255, 255, 0.35)',
      ...theme.applyStyles('dark', {
        '--arc-strong': '#a16207',
        '--arc-soft': 'rgba(250, 204, 21, 0.10)',
      }),
    })}
  >
    <circle cx="1180" cy="-40" r="430" fill="none" stroke="var(--arc-soft)" strokeWidth="90" />
    <circle cx="1180" cy="-40" r="330" fill="none" stroke="var(--arc-strong)" strokeWidth="16" />
    <circle cx="60" cy="790" r="250" fill="none" stroke="var(--arc-strong)" strokeWidth="14" />
    <circle cx="60" cy="790" r="90" fill="none" stroke="var(--arc-soft)" strokeWidth="44" />
    <circle cx="60" cy="790" r="22" fill="var(--arc-strong)" />
  </Box>
);

const Login: React.FC = () => {
  const [username, setUsername] = useState(() => session.rememberedUsername());
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(() => !!session.rememberedUsername());
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const { login } = useAuth();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const response = await client.post('/auth/login', {
        username: username.trim(),
        password,
        rememberMe: remember,
      });

      const { token, user } = response.data;
      login(token, user, remember);
      session.setRememberedUsername(remember ? user.username : null);
      navigate(homePath(user.role), { replace: true });
    } catch (err) {
      setError(getErrorMessage(err, 'Login failed. Please try again.'));
      setPassword('');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={(theme) => ({
        minHeight: '100dvh',
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        p: { xs: 2, md: 4 },
        position: 'relative',
        overflow: 'hidden',
        bgcolor: '#facc15',
        ...theme.applyStyles('dark', { bgcolor: '#0a0a0a' }),
      })}
    >
      <BackdropArcs />

      <Box
        sx={(theme) => ({
          position: 'relative',
          width: '100%',
          maxWidth: 1040,
          minHeight: { md: 580 },
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
          bgcolor: 'background.paper',
          borderRadius: '16px',
          overflow: 'hidden',
          boxShadow: '0 30px 60px rgba(15, 23, 42, 0.28)',
          ...theme.applyStyles('dark', { boxShadow: '0 30px 60px rgba(0, 0, 0, 0.5)', border: '1px solid', borderColor: 'divider' }),
        })}
      >
        <Box sx={{ position: 'absolute', top: 12, right: 12, zIndex: 1 }}>
          <ThemeToggle />
        </Box>

        {/* form side */}
        <Box sx={{ px: { xs: 3, sm: 6 }, py: { xs: 4, sm: 6 }, display: 'flex', flexDirection: 'column' }}>
          <Box sx={{ mb: { xs: 1, md: 5 } }}>
            <BrandLogo height={34} />
          </Box>

          <Box sx={{ my: 'auto', pt: { xs: 4, md: 0 }, width: '100%', maxWidth: 380 }}>
            <Typography
              component="h1"
              sx={{ fontSize: { xs: '26px', sm: '30px' }, fontWeight: 700, lineHeight: 1.2, letterSpacing: '-0.5px', color: HEADLINE, mb: 4 }}
            >
              Integrated Management for Modern Flour Mills
            </Typography>

            {error && (
              <Alert severity="error" sx={{ mb: 3 }}>
                {error}
              </Alert>
            )}

            <Box component="form" onSubmit={handleSubmit} noValidate>
              <Box sx={{ mb: 2.5 }}>
                <Typography component="label" htmlFor="username" sx={labelSx}>
                  Username
                </Typography>
                <TextField
                  id="username"
                  fullWidth
                  size="small"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  disabled={loading}
                  required
                  autoFocus={!username}
                  autoComplete="username"
                  placeholder="Enter your username"
                  sx={inputSx}
                />
              </Box>

              <Box sx={{ mb: 1.5 }}>
                <Typography component="label" htmlFor="password" sx={labelSx}>
                  Password
                </Typography>
                <TextField
                  id="password"
                  fullWidth
                  size="small"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={loading}
                  required
                  autoFocus={!!username}
                  autoComplete="current-password"
                  placeholder="Enter your password"
                  sx={inputSx}
                />
              </Box>

              <FormControlLabel
                sx={{ mb: 2.5, ml: '-9px', '& .MuiFormControlLabel-label': { fontSize: '13px', color: 'text.secondary' } }}
                control={
                  <Checkbox
                    size="small"
                    checked={remember}
                    onChange={(e) => setRemember(e.target.checked)}
                    disabled={loading}
                    sx={{ '&.Mui-checked': { color: 'var(--accent-border)' } }}
                  />
                }
                label="Remember me for 7 days"
              />

              <Box>
                <Button
                  variant="contained"
                  disabled={loading || !username || !password}
                  type="submit"
                  sx={{ fontSize: '14px', px: 4, py: '9px', minWidth: 140, '&:hover': { bgcolor: 'primary.dark', transform: 'translateY(-1px)' } }}
                >
                  {loading ? <CircularProgress size={20} color="inherit" /> : 'Sign in'}
                </Button>
              </Box>
            </Box>
          </Box>

          {DEMO_ACCOUNTS.length > 0 && (
            <Box sx={{ mt: 5 }}>
              <Typography sx={{ fontSize: '12px', color: 'text.secondary', mb: 1 }}>
                Try a demo account (click to fill in):
              </Typography>
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                {DEMO_ACCOUNTS.map((acc) => (
                  <Tooltip key={acc.username} title={`${acc.access}. Password: ${acc.password}`}>
                    <Box
                      component="button"
                      type="button"
                      onClick={() => {
                        setUsername(acc.username);
                        setPassword(acc.password);
                        setError('');
                      }}
                      sx={{
                        all: 'unset',
                        cursor: 'pointer',
                        fontSize: '12px',
                        fontWeight: 600,
                        color: ACCENT_TEXT,
                        px: 1.25,
                        py: 0.5,
                        borderRadius: '6px',
                        bgcolor: 'color-mix(in srgb, var(--mui-palette-primary-main) 24%, transparent)',
                        transition: 'background-color 0.15s ease',
                        '&:hover': { bgcolor: 'color-mix(in srgb, var(--mui-palette-primary-main) 40%, transparent)' },
                        '&:focus-visible': { outline: '2px solid', outlineColor: 'var(--accent-text)', outlineOffset: '2px' },
                      }}
                    >
                      {acc.username}
                    </Box>
                  </Tooltip>
                ))}
              </Box>
            </Box>
          )}
        </Box>

        {/* illustration side */}
        <Box
          sx={(theme) => ({
            display: { xs: 'none', md: 'flex' },
            alignItems: 'center',
            justifyContent: 'center',
            p: 5,
            bgcolor: '#fefce8',
            ...theme.applyStyles('dark', { bgcolor: '#0f0f0f' }),
          })}
        >
          <LoginIllustration />
        </Box>
      </Box>
    </Box>
  );
};

export default Login;
