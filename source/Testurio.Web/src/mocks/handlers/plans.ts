import { http, HttpResponse } from 'msw';
import type { PlanDefinition } from '@/types/plan.types';

const mockPlans: PlanDefinition[] = [
  {
    id: 'test-junior',
    name: 'Test Junior',
    monthlyPrice: 0,
    annualPrice: 0,
    annualDiscountPercent: 0,
    isPopular: false,
    displayFeatures: [
      'Up to 3 projects',
      '50 automated test runs / month',
      'API test execution',
      'Basic test reports',
      'Community support',
    ],
    limits: { maxProjects: 3, maxTestRunsPerMonth: 50 },
    features: { apiTesting: true, uiE2eTesting: false, aiMemory: false, pmReportPostBack: true },
  },
  {
    id: 'test-pro',
    name: 'Test Pro',
    monthlyPrice: 49,
    annualPrice: 470,
    annualDiscountPercent: 20,
    isPopular: true,
    displayFeatures: [
      'Up to 10 projects',
      'Unlimited test runs',
      'API & UI end-to-end testing',
      'AI memory layer for smarter scenarios',
      'ADO & Jira report post-back',
      'Email support',
    ],
    limits: { maxProjects: 10, maxTestRunsPerMonth: -1 },
    features: { apiTesting: true, uiE2eTesting: true, aiMemory: true, pmReportPostBack: true },
  },
  {
    id: 'team',
    name: 'Team',
    monthlyPrice: 149,
    annualPrice: 1430,
    annualDiscountPercent: 20,
    isPopular: false,
    displayFeatures: [
      'Unlimited projects',
      'Unlimited test runs',
      'API & UI end-to-end testing',
      'AI memory layer with cross-project sharing',
      'ADO & Jira report post-back',
      'Custom test generation prompts',
      'Priority support',
    ],
    limits: { maxProjects: -1, maxTestRunsPerMonth: -1 },
    features: { apiTesting: true, uiE2eTesting: true, aiMemory: true, pmReportPostBack: true },
  },
  {
    id: 'centurio',
    name: 'Centurio',
    monthlyPrice: 399,
    annualPrice: 3830,
    annualDiscountPercent: 20,
    isPopular: false,
    displayFeatures: [
      'Unlimited projects',
      'Unlimited test runs',
      'All test types including smoke, a11y, visual',
      'Full AI memory layer with global anonymised sharing',
      'All PM tool integrations',
      'Dedicated egress IP range',
      'SLA guarantee',
      'Dedicated support engineer',
    ],
    limits: { maxProjects: -1, maxTestRunsPerMonth: -1 },
    features: { apiTesting: true, uiE2eTesting: true, aiMemory: true, pmReportPostBack: true },
  },
];

export const plansHandlers = [
  http.get('/v1/plans', () => HttpResponse.json(mockPlans)),
];
