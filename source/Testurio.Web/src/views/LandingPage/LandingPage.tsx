'use client';

import PublicLayout from '@/components/PublicLayout/PublicLayout';
import HeroSection from '@/components/HeroSection/HeroSection';
import FeaturesSection from '@/components/FeaturesSection/FeaturesSection';
import HowItWorksSection from '@/components/HowItWorksSection/HowItWorksSection';
import PricingTeaserSection from '@/components/PricingTeaserSection/PricingTeaserSection';

/**
 * Landing page — public-facing marketing page accessible at `/`.
 * Composed of HeroSection → FeaturesSection → HowItWorksSection → PricingTeaserSection.
 */
export default function LandingPage() {
  return (
    <PublicLayout>
      <HeroSection />
      <FeaturesSection />
      <HowItWorksSection />
      <PricingTeaserSection />
    </PublicLayout>
  );
}
