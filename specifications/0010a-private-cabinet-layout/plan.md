# Implementation Plan — Private Cabinet Main Layout & Navigation (0010a)

## Tasks

- [x] T001 [UI] Add layout TypeScript types: `AuthUser` (`id`, `displayName`, `email`, `avatarUrl?`) and `SidebarState` (`collapsed: boolean`) — `source/Testurio.Web/src/types/layout.types.ts`
- [x] T002 [UI] Add `useAuthUser` hook: reads the signed-in user identity from the Azure AD B2C session (via MSAL or the session cookie exposed to the client); returns `AuthUser | null` — `source/Testurio.Web/src/hooks/useAuthUser.ts`
- [x] T003 [UI] Add `useSidebarState` hook: reads/writes `testurio.sidebarCollapsed` from `localStorage`, returns `[collapsed: boolean, toggle: () => void]`, defaults to `false` when key is absent or `localStorage` is unavailable — `source/Testurio.Web/src/hooks/useSidebarState.ts`
- [x] T004 [UI] Add MSW mock handler for auth user: returns a mock `AuthUser` so the layout renders correctly in Storybook and component tests — `source/Testurio.Web/src/mocks/handlers/auth.ts`
- [x] T005 [UI] Create `AppHeader` component: renders the Testurio logo (link to `/dashboard`) on the left and the signed-in user's avatar + display name on the right; accepts `user: AuthUser | null`; uses `useAuthUser` internally — `source/Testurio.Web/src/components/AppHeader/AppHeader.tsx`
- [x] T006 [UI] Create `NavItem` component: renders a single MUI `ListItemButton` with an icon and an optional label; accepts `icon`, `label`, `href`, `active`, `collapsed`, `tooltip` props; uses Next.js `Link`; shows MUI `Tooltip` on hover when `collapsed` is true — `source/Testurio.Web/src/components/NavItem/NavItem.tsx`
- [x] T007 [UI] Create `AppSidebar` component: renders the collapsible sidebar; uses `useSidebarState` for collapsed state; renders `NavItem` for Dashboard, Projects, Settings with active-link detection via `usePathname`; renders Sign Out `ListItemButton` pinned at the bottom; handles logout redirect to the B2C logout endpoint; renders the sidebar toggle chevron button — `source/Testurio.Web/src/components/AppSidebar/AppSidebar.tsx`
- [x] T008 [UI] Create `PrivateCabinetLayout` component: assembles `AppHeader` and `AppSidebar` into the full shell; renders `children` in the main content area; uses MUI `Box` for the flex layout — `source/Testurio.Web/src/components/PrivateCabinetLayout/PrivateCabinetLayout.tsx`
- [ ] T009 [UI] Create the authenticated Next.js route group: add `source/Testurio.Web/src/app/(authenticated)/layout.tsx` that wraps `children` in `PrivateCabinetLayout` and includes an auth guard (redirect to `/sign-in` with `returnUrl` query param when no valid session) — `source/Testurio.Web/src/app/(authenticated)/layout.tsx`
- [ ] T010 [UI] Move existing authenticated pages into the route group: move `source/Testurio.Web/src/app/projects/` to `source/Testurio.Web/src/app/(authenticated)/projects/` so they inherit the shell layout — `source/Testurio.Web/src/app/(authenticated)/projects/`
- [ ] T011 [UI] Add root redirect: update `source/Testurio.Web/src/app/page.tsx` to redirect authenticated users to `/dashboard` and unauthenticated users to `/sign-in` — `source/Testurio.Web/src/app/page.tsx`
- [ ] T012 [UI] Add dashboard route placeholder inside the authenticated group: create `source/Testurio.Web/src/app/(authenticated)/dashboard/page.tsx` as a minimal placeholder (renders `null` or a `<DashboardPage />` import stub) so the `/dashboard` route exists for feature 0010 to register its page component — `source/Testurio.Web/src/app/(authenticated)/dashboard/page.tsx`
- [ ] T013 [UI] Add settings route placeholder inside the authenticated group: create `source/Testurio.Web/src/app/(authenticated)/settings/page.tsx` as a minimal placeholder so the `/settings` route exists for feature 0014 — `source/Testurio.Web/src/app/(authenticated)/settings/page.tsx`
- [ ] T014 [UI] Register route constants: export `DASHBOARD_ROUTE`, `PROJECTS_ROUTE`, `SETTINGS_ROUTE`, `PROJECT_HISTORY_ROUTE`, and `PROJECT_SETTINGS_ROUTE` as named constants; `PROJECT_HISTORY_ROUTE` and `PROJECT_SETTINGS_ROUTE` are builder functions `(id: string) => string` — `source/Testurio.Web/src/routes/routes.tsx`
- [ ] T015 [UI] Add layout translation keys for all user-visible strings in the header, sidebar, and tooltips — `source/Testurio.Web/src/locales/en/layout.json`
- [ ] T016 [Test] Frontend component tests for `AppHeader`: logo renders with link to `/dashboard`, avatar renders with user initials when no picture URL, display name truncated at 24 chars, fallback to email prefix when `displayName` is null — `source/Testurio.Web/src/components/AppHeader/AppHeader.test.tsx`
- [ ] T017 [Test] Frontend component tests for `NavItem`: renders icon and label when expanded, renders icon only and tooltip when collapsed, active state applies correct styles, Next.js Link is used — `source/Testurio.Web/src/components/NavItem/NavItem.test.tsx`
- [ ] T018 [Test] Frontend component tests for `AppSidebar`: Dashboard link active when path is `/dashboard`, Projects link active when path starts with `/projects`, sidebar toggle switches between expanded and collapsed, collapsed state persists to localStorage, Sign Out button disables on click — `source/Testurio.Web/src/components/AppSidebar/AppSidebar.test.tsx`
- [ ] T019 [Test] Frontend component tests for `PrivateCabinetLayout`: children rendered in main content area, `AppHeader` and `AppSidebar` both present in the DOM — `source/Testurio.Web/src/components/PrivateCabinetLayout/PrivateCabinetLayout.test.tsx`
- [ ] T020 [Test] E2E tests for the shell layout: authenticated user sees header logo, avatar, sidebar links; active link highlighted on Dashboard and Projects routes; sidebar collapses and expands; Sign Out navigates to the B2C logout endpoint — `source/Testurio.Web/e2e/private-cabinet-layout.spec.ts`

## Rationale

**This feature is purely a UI concern — no backend changes are required.** The shell layout reads user identity from the existing Azure AD B2C session (already established by feature 0013's auth flow). Sign Out delegates entirely to the B2C logout endpoint — no API call is made to `Testurio.Api`. There are no new domain models, repositories, services, or API endpoints introduced by this feature.

**Types and hooks first (T001–T004).** `AuthUser` and `SidebarState` types (T001) must exist before any hook or component can reference them. `useAuthUser` (T002) and `useSidebarState` (T003) are leaf hooks with no component dependencies; they can be compiled and tested independently. The MSW auth mock handler (T004) is added before any component is built so tests can render the layout with a fake authenticated user.

**Leaf components before composite components (T005–T008).** `AppHeader` (T005) depends only on `AuthUser` and the theme. `NavItem` (T006) is the smallest composable unit of sidebar navigation. `AppSidebar` (T007) composes multiple `NavItem` instances and the Sign Out action; it depends on T006 being in place. `PrivateCabinetLayout` (T008) is the outermost shell that assembles T005 and T007; it must be the last component created.

**Next.js route group and page moves after components are built (T009–T013).** The `(authenticated)/layout.tsx` (T009) imports `PrivateCabinetLayout`; that component must exist first. Moving `projects/` into the route group (T010) is a filesystem operation that should happen once and only after the layout is in place to avoid a period where pages render without the shell. The root redirect (T011) and placeholder pages (T012–T013) require the authenticated route group to exist.

**Route constants after page structure is finalised (T014).** Route strings must reflect the final directory structure. Defining constants before the pages are moved could result in constants that reference incorrect paths. `PROJECT_HISTORY_ROUTE` and `PROJECT_SETTINGS_ROUTE` builder functions are defined here so feature 0010 (dashboard) can import them directly without owning the definition.

**Translations last among UI tasks (T015).** All user-visible strings are identified only once components are written; adding them last avoids defining keys that components do not end up using.

**Tests last (T016–T020).** Component tests (T016–T019) require the MSW auth handler (T004) and the finished components. E2E tests (T020) require the Next.js dev server with the full route group and real auth session simulation in place.

**Cross-feature dependencies.**

- **Feature 0013 (Registration & Sign-In)** must be implemented before feature 0010a can establish a real auth session. However, the layout shell and its component tests are independent of 0013 — the MSW auth handler (T004) stubs the session for testing. At runtime, feature 0013's authentication establishes the session that `useAuthUser` reads.
- **Feature 0010 (Project Dashboard)** imports `DashboardPage` into the `/dashboard` placeholder created in T012 and imports route constants from T014. Feature 0010 cannot be implemented before this feature is complete.
- **Feature 0014 (Account Settings)** populates the `/settings` placeholder created in T013. Feature 0014 cannot be implemented before this feature is complete.
- **Feature 0006 (Project Creation)** already uses `source/Testurio.Web/src/app/projects/new/page.tsx`. Moving it into the authenticated route group (T010) means feature 0006's page automatically inherits the shell layout without any changes to the page component itself.
- **Feature 0011 (Per-Project Test History)** and **Feature 0043 (Dashboard Real-Time Updates)** both import `PROJECT_HISTORY_ROUTE` from `routes.tsx` (T014); this feature must be complete before those features are implemented.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
