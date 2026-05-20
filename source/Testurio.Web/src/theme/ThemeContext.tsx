'use client';

import { createContext, useContext, useState, useMemo, useCallback, type ReactNode } from 'react';
import { ThemeProvider } from '@mui/material/styles';
import { createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import type { ThemeMode } from '@/types/account.types';

const THEME_STORAGE_KEY = 'testurio.theme';

function readStoredTheme(): ThemeMode {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'dark' || stored === 'light') return stored;
  } catch {
    // localStorage unavailable (e.g. private browsing) — fall through to default
  }
  return 'light';
}

function writeStoredTheme(mode: ThemeMode): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, mode);
  } catch {
    // localStorage unavailable — silently ignore
  }
}

interface ThemeContextValue {
  themeMode: ThemeMode;
  setThemeMode: (mode: ThemeMode) => void;
}

const ThemeContext = createContext<ThemeContextValue>({
  themeMode: 'light',
  setThemeMode: () => undefined,
});

export function useThemeMode(): ThemeContextValue {
  return useContext(ThemeContext);
}

interface ThemeContextProviderProps {
  children: ReactNode;
}

export function ThemeContextProvider({ children }: ThemeContextProviderProps) {
  const [themeMode, setThemeModeState] = useState<ThemeMode>(readStoredTheme);

  const setThemeMode = useCallback((mode: ThemeMode) => {
    setThemeModeState(mode);
    writeStoredTheme(mode);
  }, []);

  const muiTheme = useMemo(
    () =>
      createTheme({
        palette: {
          mode: themeMode,
          primary: { main: '#3b82f6' },
          ...(themeMode === 'light'
            ? {
                background: {
                  default: '#f4f6fb',
                  paper: '#ffffff',
                },
              }
            : {
                background: {
                  default: '#111827',
                  paper: '#1f2937',
                },
              }),
        },
        shape: { borderRadius: 5 },
        typography: { fontFamily: 'Inter, sans-serif' },
      }),
    [themeMode],
  );

  const contextValue = useMemo<ThemeContextValue>(
    () => ({ themeMode, setThemeMode }),
    [themeMode, setThemeMode],
  );

  return (
    <ThemeContext.Provider value={contextValue}>
      <ThemeProvider theme={muiTheme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ThemeContext.Provider>
  );
}
