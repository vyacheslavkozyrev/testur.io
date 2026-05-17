import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';
import { getSessionStore } from '@/app/api/auth/session/route';
import PrivateCabinetLayout from '@/components/PrivateCabinetLayout/PrivateCabinetLayout';
import { SIGN_IN_ROUTE } from '@/routes/routes';

/**
 * Secondary server-side session guard for the authenticated layout.
 *
 * The primary guard is the middleware (src/middleware.ts) which runs on the
 * Edge and redirects unauthenticated requests before they reach this layout.
 * This check provides defence-in-depth: it validates the session against the
 * server-side store and rejects expired or invalid sessions.
 */
async function validateSession(): Promise<boolean> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');
  if (!sessionCookie?.value) return false;

  const sessionStore = getSessionStore();
  const session = sessionStore.get(sessionCookie.value);
  if (!session) return false;

  const nowSec = Math.floor(Date.now() / 1000);
  return session.exp >= nowSec;
}

/**
 * Auth-guarded layout for all authenticated pages.
 *
 * Primary auth check is performed by middleware (src/middleware.ts).
 * This layout provides a secondary defence-in-depth validation.
 */
export default async function AuthenticatedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const valid = await validateSession();

  if (!valid) {
    redirect(SIGN_IN_ROUTE);
  }

  return <PrivateCabinetLayout>{children}</PrivateCabinetLayout>;
}
