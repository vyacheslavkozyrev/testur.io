export interface PlanDefinition {
  id: string;
  name: string;
  monthlyPrice: number;
  annualPrice: number;
  annualDiscountPercent: number;
  isPopular: boolean;
  features: string[];
}

export type BillingInterval = 'monthly' | 'annual';
