'use client';

import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'testurio.sidebarCollapsed';

function readFromStorage(): boolean {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value === 'true';
  } catch {
    return false;
  }
}

function writeToStorage(collapsed: boolean): void {
  try {
    localStorage.setItem(STORAGE_KEY, String(collapsed));
  } catch {
    // localStorage unavailable — ignore
  }
}

/**
 * Reads and writes the sidebar collapsed state from localStorage.
 * Defaults to `false` (expanded) when the key is absent or localStorage is unavailable.
 *
 * @returns `[collapsed, toggle]`
 */
export function useSidebarState(): [boolean, () => void] {
  const [collapsed, setCollapsed] = useState<boolean>(false);

  useEffect(() => {
    setCollapsed(readFromStorage());
  }, []);

  const toggle = useCallback(() => {
    setCollapsed((prev) => {
      const next = !prev;
      writeToStorage(next);
      return next;
    });
  }, []);

  return [collapsed, toggle];
}
