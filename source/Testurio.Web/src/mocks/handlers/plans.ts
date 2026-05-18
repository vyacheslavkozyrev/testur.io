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
    features: [
      'Up to 3 projects',
      '50 automated test runs / day',
      'API test execution',
      'Basic test reports',
      'Community support',
    ],
  },
  {
    id: 'test-pro',
    name: 'Test Pro',
    monthlyPrice: 49,
    annualPrice: 470,
    annualDiscountPercent: 20,
    isPopular: true,
    features: [
      'Up to 10 projects',
      'Unlimited test runs',
      'API & UI end-to-end testing',
      'AI memory layer for smarter scenarios',
      'ADO & Jira report post-back',
      'Email support',
    ],
  },
  {
    id: 'team',
    name: 'Team',
    monthlyPrice: 149,
    annualPrice: 1430,
    annualDiscountPercent: 20,
    isPopular: false,
    features: [
      'Unlimited projects',
      'Unlimited test runs',
      'API & UI end-to-end testing',
      'AI memory layer with cross-project sharing',
      'ADO & Jira report post-back',
      'Custom test generation prompts',
      'Priority support',
    ],
  },
  {
    id: 'centurio',
    name: 'Centurio',
    monthlyPrice: 399,
    annualPrice: 3830,
    annualDiscountPercent: 20,
    isPopular: false,
    features: [
      'Unlimited projects',
      'Unlimited test runs',
      'All test types including smoke, a11y, visual',
      'Full AI memory layer with global anonymised sharing',
      'All PM tool integrations',
      'Dedicated egress IP range',
      'SLA guarantee',
      'Dedicated support engineer',
    ],
  },
];

export const plansHandlers = [
  http.get('/v1/plans', () => HttpResponse.json(mockPlans)),
];
