import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';

/**
 * Root page: redirects authenticated users to /dashboard,
 * unauthenticated users to /sign-in.
 *
 * Feature 0013 will replace the session detection with a real MSAL token check.
 */
export default async function RootPage() {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');

  if (sessionCookie?.value) {
    redirect('/dashboard');
  } else {
    redirect('/sign-in');
  }
}
