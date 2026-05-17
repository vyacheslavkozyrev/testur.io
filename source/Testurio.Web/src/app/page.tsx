import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';
import { decodeAndValidateIdToken } from '@/services/auth/tokenValidator';
import { DASHBOARD_ROUTE, SIGN_IN_ROUTE } from '@/routes/routes';

/**
 * Root page: server-side redirect based on real session validation.
 *
 * Authenticated users → /dashboard
 * Unauthenticated users → /sign-in
 *
 * Token validation is done via `decodeAndValidateIdToken` (same logic used
 * by the `(authenticated)` layout guard) so both redirect sources behave
 * identically.
 */
export default async function RootPage() {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');

  if (sessionCookie?.value) {
    const user = await decodeAndValidateIdToken(sessionCookie.value);
    if (user) {
      redirect(DASHBOARD_ROUTE);
    }
  }

  redirect(SIGN_IN_ROUTE);
}
