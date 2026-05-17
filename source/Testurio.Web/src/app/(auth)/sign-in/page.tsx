import { Suspense } from 'react';
import SignInPage from '@/views/SignInPage/SignInPage';

/**
 * /sign-in page — renders the custom sign-in view.
 * Wrapped in Suspense because SignInPage uses `useSearchParams`.
 */
export default function Page() {
  return (
    <Suspense>
      <SignInPage />
    </Suspense>
  );
}
