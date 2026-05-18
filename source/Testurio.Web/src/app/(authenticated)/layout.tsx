import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';
import PrivateCabinetLayout from '@/components/PrivateCabinetLayout/PrivateCabinetLayout';
import { SIGN_IN_ROUTE } from '@/routes/routes';

/**
 * Auth-guarded layout for all authenticated pages.
 *
 * The primary guard is src/middleware.ts which runs on the Edge before this
 * layout is reached. This secondary check only verifies cookie presence as a
 * defence-in-depth safeguard — it avoids hitting the in-memory session store
 * which can be cleared on server restarts in development.
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

  return <PrivateCabinetLayout>{children}</PrivateCabinetLayout>;
}
