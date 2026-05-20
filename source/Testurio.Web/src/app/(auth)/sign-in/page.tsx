import { Suspense } from 'react';
import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import SignInPage from '@/views/SignInPage/SignInPage';
import { getSessionStore } from '@/app/api/auth/session/route';
import { DASHBOARD_ROUTE } from '@/routes/routes';

export default async function Page() {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');
  const nowSec = Math.floor(Date.now() / 1000);

  if (sessionCookie?.value) {
    const session = getSessionStore().get(sessionCookie.value);
    if (session && session.exp > nowSec) {
      redirect(DASHBOARD_ROUTE);
    }
  }

  return (
    <Suspense>
      <SignInPage />
    </Suspense>
  );
}
