import { Suspense } from 'react';
import CheckoutSuccessPage from '@/views/CheckoutSuccessPage/CheckoutSuccessPage';

export default function Page() {
  return (
    <Suspense>
      <CheckoutSuccessPage />
    </Suspense>
  );
}
