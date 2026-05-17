/**
 * Auth route group layout.
 *
 * Public pages (sign-in, sign-up, forgot-password) do not use the authenticated
 * shell (no AppHeader, no AppSidebar). This layout renders only the page content,
 * inheriting only the root layout's Providers wrapper.
 */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
