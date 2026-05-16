import { http, HttpResponse } from 'msw';
import type { DashboardResponse } from '@/types/dashboard.types';

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

export const dashboardHandlers = [
  http.get('/v1/stats/dashboard', () => HttpResponse.json(mockDashboardResponse)),
];
