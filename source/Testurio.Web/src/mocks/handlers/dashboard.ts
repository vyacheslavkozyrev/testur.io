import { http, HttpResponse } from 'msw';
import type { DashboardResponse, DashboardUpdatedEvent } from '@/types/dashboard.types';

const mockDashboardResponse: DashboardResponse = {
  projects: [
    {
      projectId: '00000000-0000-0000-0000-000000000001',
      name: 'Demo Project',
      productUrl: 'https://example.com',
      testingStrategy: 'Focus on API contracts and key user flows.',
      latestRun: {
        runId: '00000000-0000-0000-0000-000000000011',
        status: 'Passed',
        startedAt: '2026-05-15T10:00:00Z',
        completedAt: '2026-05-15T10:05:00Z',
      },
    },
    {
      projectId: '00000000-0000-0000-0000-000000000002',
      name: 'Alpha App',
      productUrl: 'https://alpha.example.com',
      testingStrategy: 'End-to-end user journey testing.',
      latestRun: {
        runId: '00000000-0000-0000-0000-000000000012',
        status: 'Failed',
        startedAt: '2026-05-14T08:30:00Z',
        completedAt: '2026-05-14T08:35:00Z',
      },
    },
    {
      projectId: '00000000-0000-0000-0000-000000000003',
      name: 'Beta Service',
      productUrl: 'https://beta.example.com',
      testingStrategy: 'Smoke tests only.',
      latestRun: null,
    },
  ],
  quotaUsage: {
    usedToday: 3,
    dailyLimit: 50,
    resetsAt: '2026-05-17T00:00:00Z',
  },
};

/**
 * Mock SSE event emitted for the first project in the snapshot.
 * Tests that exercise live-update behaviour can dispatch a custom 'sse-event' on
 * the window to inject additional events without replacing this handler.
 */
const mockSseEvent: DashboardUpdatedEvent = {
  projectId: '00000000-0000-0000-0000-000000000001',
  latestRun: {
    runId: '00000000-0000-0000-0000-000000000099',
    status: 'Running',
    startedAt: new Date().toISOString(),
    completedAt: null,
  },
  quotaUsage: null,
};

export const dashboardHandlers = [
  http.get('/v1/stats/dashboard', () => HttpResponse.json(mockDashboardResponse)),

  // Feature 0043: SSE stream handler.
  // Returns a ReadableStream that immediately pushes one mock event and then stays open.
  // In tests that need to inject additional events, use the streamManager or a custom handler.
  http.get('/v1/stats/dashboard/stream', () => {
    const encoder = new TextEncoder();
    const stream = new ReadableStream({
      start(controller) {
        const data = `data: ${JSON.stringify(mockSseEvent)}\n\n`;
        controller.enqueue(encoder.encode(data));
        // Leave the stream open — MSW keeps the response alive until the test tears down.
      },
    });

    return new HttpResponse(stream, {
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
      },
    });
  }),
];
