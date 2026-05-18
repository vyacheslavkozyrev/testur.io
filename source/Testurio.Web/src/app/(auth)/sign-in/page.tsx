import { Suspense } from 'react';
import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import SignInPage from '@/views/SignInPage/SignInPage';
import { DASHBOARD_ROUTE } from '@/routes/routes';

/**
 * /sign-in page — renders the custom sign-in view.
 *
 * Performs a server-side redirect to /dashboard when a session cookie is already
 * present, avoiding a client-side flash. The client component no longer needs a
 * useEffect for this check.
 */
export default async function Page() {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');
  if (sessionCookie?.value) {
    redirect(DASHBOARD_ROUTE);
  }

  return (
    <Suspense>
      <SignInPage />
    </Suspense>
  );
}
