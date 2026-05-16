import { createTheme } from '@mui/material/styles';

export const CHROME_BG = '#1a1d2e';
export const SIDEBAR_BG = '#eef0f4';
export const SIDEBAR_TEXT = '#374151';
export const SIDEBAR_MUTED = '#9ca3af';
export const SIDEBAR_DIVIDER = 'rgba(0,0,0,0.08)';
export const SIDEBAR_HOVER = 'rgba(0,0,0,0.05)';
export const SIDEBAR_ACTIVE_BG = 'rgba(59,130,246,0.1)';
export const SIDEBAR_ACTIVE_TEXT = '#2563eb';
export const CHROME_TEXT = 'rgba(255,255,255,0.87)';
export const CHROME_MUTED = 'rgba(255,255,255,0.45)';
export const CHROME_DIVIDER = 'rgba(255,255,255,0.08)';
export const CHROME_HOVER = 'rgba(255,255,255,0.06)';
export const CHROME_ACTIVE_BG = 'rgba(96,165,250,0.18)';
export const CHROME_ACTIVE_TEXT = '#93c5fd';

export const theme = createTheme({
  palette: {
    primary: { main: '#3b82f6' },
    background: {
      default: '#f4f6fb',
      paper: '#ffffff',
    },
  },
  shape: {
    borderRadius: 5,
  },
  typography: {
    fontFamily: 'Inter, sans-serif',
  },
});
