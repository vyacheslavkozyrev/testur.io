import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';
import { getSessionStore } from '@/app/api/auth/session/store';
import PrivateCabinetLayout from '@/components/PrivateCabinetLayout/PrivateCabinetLayout';
import { ThemeContextProvider } from '@/theme/ThemeContext';

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
    redirect('/api/auth/sign-out');
  }

  const session = getSessionStore().get(sessionCookie.value);
  if (!session) {
    redirect('/api/auth/sign-out');
  }

  const nowSec = Math.floor(Date.now() / 1000);
  if (session.exp < nowSec) {
    redirect('/api/auth/sign-out');
  }

  return (
    <ThemeContextProvider>
      <PrivateCabinetLayout>{children}</PrivateCabinetLayout>
    </ThemeContextProvider>
  );
}
