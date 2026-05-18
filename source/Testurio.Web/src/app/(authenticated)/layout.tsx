import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';
import { getSessionStore } from '@/app/api/auth/session/route';
import PrivateCabinetLayout from '@/components/PrivateCabinetLayout/PrivateCabinetLayout';
import { SIGN_IN_ROUTE } from '@/routes/routes';

/**
 * Auth-guarded layout for all authenticated pages.
 *
 * The primary guard is src/middleware.ts which runs on the Edge.
 * This secondary check validates session expiry against the server-side store,
 * ensuring expired cookies are rejected before any authenticated page renders.
 * If the store is empty (e.g. process restart in development), the layout
 * redirects to sign-in rather than allowing access.
 */
export default async function AuthenticatedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');

  if (!sessionCookie?.value) {
    redirect(SIGN_IN_ROUTE);
  }

  const session = getSessionStore().get(sessionCookie.value);
  if (!session) {
    redirect(SIGN_IN_ROUTE);
  }

  const nowSec = Math.floor(Date.now() / 1000);
  if (session.exp < nowSec) {
    getSessionStore().delete(sessionCookie.value);
    redirect(SIGN_IN_ROUTE);
  }

  return <PrivateCabinetLayout>{children}</PrivateCabinetLayout>;
}
