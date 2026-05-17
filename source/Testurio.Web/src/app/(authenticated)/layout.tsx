import { redirect } from 'next/navigation';
import { cookies, headers } from 'next/headers';
import { decodeAndValidateIdToken } from '@/services/auth/tokenValidator';
import PrivateCabinetLayout from '@/components/PrivateCabinetLayout/PrivateCabinetLayout';
import { SIGN_IN_ROUTE } from '@/routes/routes';

/**
 * Validates the `testurio_session` cookie and returns the user ID, or null if
 * the session is absent, expired, or malformed.
 */
async function getSessionUserId(): Promise<string | null> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');
  if (!sessionCookie?.value) return null;

  const user = await decodeAndValidateIdToken(sessionCookie.value);
  return user?.id ?? null;
}

/**
 * Auth-guarded layout for all authenticated pages.
 *
 * Server-side session check fires before any page content renders:
 * - Valid session  → render the private cabinet shell with the page content.
 * - No/invalid session → redirect to /sign-in?returnUrl=<requested-path>
 *   so the user is returned to their original destination after sign-in.
 */
export default async function AuthenticatedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const userId = await getSessionUserId();

  if (!userId) {
    const headerStore = await headers();
    // x-invoke-path is the internal Next.js header that carries the requested path.
    // Fall back to '/' if not present.
    const requestedPath = headerStore.get('x-invoke-path') ?? '/';
    const returnUrl = encodeURIComponent(requestedPath);
    redirect(`${SIGN_IN_ROUTE}?returnUrl=${returnUrl}`);
  }

  return <PrivateCabinetLayout>{children}</PrivateCabinetLayout>;
}
