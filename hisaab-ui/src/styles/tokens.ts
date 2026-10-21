// Design Tokens - taste-skill compliant
// Professional B2B ERP dashboard aesthetic

export const TOKENS = {
  // Colour reference. The live palettes are in App.tsx (createTheme colorSchemes).
  // Hisaab brand (from the logo): deep green, cream, gold sparkle, terracotta line.
  // Light theme = warm off-white + green, dark theme = green-black + light green.
  colors: {
    accent: '#0e5a3a',
    accentHover: '#0a4530',
    accentTextLight: '#0e5a3a',
    cream: '#fbf6ea',
    gold: '#e3a72f',
    terracotta: '#c8553d',
    neutralLight: { background: '#f8f6f0', paper: '#ffffff', text: '#1b2420', textMuted: '#66706a', divider: '#e6e2d8' },
    neutralDark: { background: '#0b1410', paper: '#121d18', text: '#f3efe4', textMuted: '#a3b0a8', divider: '#223029' },
    success: '#16a34a',
    warning: '#ea580c',
    error: '#dc2626',
    info: '#0891b2',
  },

  // Typography
  typography: {
    fontFamily: '"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
    monoFamily: '"Fira Code", "Courier New", monospace',

    // Size scale (in pixels)
    sizes: {
      xs: '11px',
      sm: '12px',
      base: '13px',
      md: '14px',
      lg: '16px',
      xl: '18px',
      '2xl': '20px',
      '3xl': '24px',
      '4xl': '28px',
      '5xl': '32px',
      '6xl': '40px',
    },

    // Font weights
    weights: {
      normal: 400,
      medium: 500,
      semibold: 600,
      bold: 700,
    },

    // Line heights
    lineHeights: {
      tight: 1.2,
      normal: 1.5,
      relaxed: 1.75,
    },
  },

  // Spacing Scale (8px base)
  spacing: {
    0: '0px',
    1: '4px',
    2: '8px',
    3: '12px',
    4: '16px',
    5: '20px',
    6: '24px',
    8: '32px',
    10: '40px',
    12: '48px',
    16: '64px',
    20: '80px',
    24: '96px',
  },

  // Corner Radius - Shape Consistency Lock (8px system)
  radius: {
    none: '0px',
    sm: '4px',
    base: '8px',
    md: '12px',
    lg: '16px',
    full: '999px',
  },

  // Shadows
  shadows: {
    none: 'none',
    sm: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
    base: '0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06)',
    md: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)',
    lg: '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)',
    xl: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
  },

  // Transitions
  transitions: {
    fast: '0.15s ease',
    normal: '0.2s ease',
    slow: '0.3s ease',
  },

  // Z-Index Scale
  zIndex: {
    hide: -1,
    base: 0,
    dropdown: 1000,
    sticky: 1100,
    fixed: 1200,
    modalBackdrop: 1300,
    modal: 1400,
    popover: 1500,
    tooltip: 1600,
  },

  // Breakpoints
  breakpoints: {
    xs: 0,
    sm: 640,
    md: 768,
    lg: 1024,
    xl: 1280,
    '2xl': 1536,
  },

  // Motion Intensity (3-4 for business app)
  motion: {
    // MOTION_INTENSITY: 3-4 (fluid transitions, micro-interactions)
    microInteraction: {
      duration: 150,
      easing: 'cubic-bezier(0.16, 1, 0.3, 1)',
    },
    transition: {
      duration: 200,
      easing: 'cubic-bezier(0.16, 1, 0.3, 1)',
    },
    slowTransition: {
      duration: 300,
      easing: 'cubic-bezier(0.16, 1, 0.3, 1)',
    },
  },
};

// Component-specific token sets
export const COMPONENT_TOKENS = {
  button: {
    height: {
      sm: '32px',
      base: '40px',
      lg: '48px',
    },
    padding: {
      sm: '6px 12px',
      base: '10px 16px',
      lg: '12px 20px',
    },
    fontSize: {
      sm: '12px',
      base: '13px',
      lg: '14px',
    },
  },

  input: {
    height: '40px',
    padding: '10px 12px',
    fontSize: '13px',
    borderRadius: '8px',
    borderColor: '#e4e4e7',
    focusBorderColor: '#0e5a3a',
    focusBoxShadow: '0 0 0 3px rgb(59 130 246 / 0.1)',
  },

  card: {
    padding: '20px',
    borderRadius: '12px',
    borderColor: '#e4e4e7',
    backgroundColor: '#ffffff',
    boxShadow: TOKENS.shadows.sm,
  },
};

// Alias for consistency
export const DESIGN_DIALECT = {
  variance: 5, // DESIGN_VARIANCE: 5-6 (minimal asymmetry, professional)
  motion: 3,  // MOTION_INTENSITY: 3-4 (fluid transitions)
  density: 5, // VISUAL_DENSITY: 5-6 (business app density)
};
