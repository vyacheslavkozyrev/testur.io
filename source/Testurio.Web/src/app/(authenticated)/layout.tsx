import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';
import PrivateCabinetLayout from '@/components/PrivateCabinetLayout/PrivateCabinetLayout';

/**
 * Checks whether a valid B2C session cookie exists server-side.
 * Returns the authenticated user ID from the session, or null if unauthenticated.
 *
 * Feature 0013 will replace this stub with a real MSAL token validation.
 */
async function getSessionUserId(): Promise<string | null> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');
  return sessionCookie?.value ?? null;
}

export default async function AuthenticatedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const userId = await getSessionUserId();

  if (!userId) {
    // Feature 0013 will supply the real returnUrl via Next.js middleware.
    // For now we redirect to sign-in; middleware will capture the original URL.
    redirect('/sign-in');
  }

  return <PrivateCabinetLayout>{children}</PrivateCabinetLayout>;
}
