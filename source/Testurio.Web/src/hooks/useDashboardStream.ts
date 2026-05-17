import { useCallback, useEffect, useRef } from 'react';
import type { DashboardUpdatedEvent } from '@/types/dashboard.types';

export interface UseDashboardStreamOptions {
  /** Invoked with each parsed DashboardUpdatedEvent received from the SSE stream. */
  onUpdate: (event: DashboardUpdatedEvent) => void;
  /**
   * Invoked after all reconnect attempts are exhausted.
   * The caller should fall back to a one-time re-fetch and stop expecting live data.
   */
  onFallback: () => void;
  /**
   * Invoked when the connection drops and a reconnect attempt is scheduled.
   * Called with `true` when reconnect back-off begins, `false` when the connection is restored.
   * Not called if the connection is cleanly closed (e.g. on unmount).
   */
  onReconnecting?: (reconnecting: boolean) => void;
  /** Set to true to open the SSE connection (e.g. after the snapshot has loaded). */
  enabled: boolean;
}

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL ?? '').replace(/\/$/, '');
const SSE_URL = `${API_BASE}/v1/stats/dashboard/stream`;
const INITIAL_DELAY_MS = 1000;
const MAX_DELAY_MS = 30_000;
const MAX_ATTEMPTS = 5;

/**
 * Opens an EventSource to the dashboard SSE stream.
 * Implements exponential back-off reconnect (1 s initial, 30 s cap, 5 attempts max).
 * Calls `onFallback` when all attempts are exhausted.
 * Closes the EventSource on unmount or when `enabled` turns false.
 */
export function useDashboardStream({
  onUpdate,
  onFallback,
  onReconnecting,
  enabled,
}: UseDashboardStreamOptions): void {
  const onUpdateRef = useRef(onUpdate);
  const onFallbackRef = useRef(onFallback);
  const onReconnectingRef = useRef(onReconnecting);
  onUpdateRef.current = onUpdate;
  onFallbackRef.current = onFallback;
  onReconnectingRef.current = onReconnecting;

  const attemptsRef = useRef(0);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const esRef = useRef<EventSource | null>(null);

  const cleanup = useCallback(() => {
    if (timeoutRef.current !== null) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
    if (esRef.current !== null) {
      esRef.current.close();
      esRef.current = null;
    }
  }, []);

  const connect = useCallback(() => {
    cleanup();

    const es = new EventSource(SSE_URL);
    esRef.current = es;

    es.onopen = () => {
      // Reset attempt counter on a successful connection.
      attemptsRef.current = 0;
      // Notify caller that the connection is healthy (clears any reconnecting indicator).
      onReconnectingRef.current?.(false);
    };

    es.onmessage = (evt: MessageEvent<string>) => {
      try {
        const parsed = JSON.parse(evt.data) as DashboardUpdatedEvent;
        onUpdateRef.current(parsed);
      } catch {
        // Silently ignore unparseable frames — they may be keep-alive comments.
      }
    };

    es.onerror = () => {
      // EventSource closes itself after an error; we handle reconnect manually.
      es.close();
      esRef.current = null;

      attemptsRef.current += 1;
      if (attemptsRef.current > MAX_ATTEMPTS) {
        onFallbackRef.current();
        return;
      }

      // Signal the caller that reconnect back-off is in progress.
      onReconnectingRef.current?.(true);

      const delay = Math.min(
        INITIAL_DELAY_MS * Math.pow(2, attemptsRef.current - 1),
        MAX_DELAY_MS,
      );

      timeoutRef.current = setTimeout(() => {
        connect();
      }, delay);
    };
  }, [cleanup]);

  useEffect(() => {
    if (!enabled) {
      cleanup();
      return;
    }

    connect();

    return cleanup;
  }, [enabled, connect, cleanup]);
}
