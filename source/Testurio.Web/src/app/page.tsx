import LandingPage from '@/views/LandingPage/LandingPage';

/**
 * Root page: public landing page (feature 0012).
 *
 * Rendered for all visitors regardless of auth state.
 * Authenticated users see a "Go to Dashboard" button in the PublicHeader.
 */
export default function RootPage() {
  return <LandingPage />;
}
