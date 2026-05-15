# User Stories — Private Cabinet Main Layout & Navigation (0010a)

## Out of Scope

The following are explicitly **not** part of this feature:

- The Dashboard page content (project cards, quota bar) — covered by feature 0010
- The Account Settings page content — covered by feature 0014
- Authentication flow (sign-in, registration, token refresh) — covered by feature 0013
- Sign Out implementation details beyond triggering the Azure AD B2C logout redirect
- Any per-project pages or their content
- Mobile responsive layout or hamburger menu — the sidebar is collapsible but not hidden on mobile (mobile-first layout is post-MVP)
- Notification badges or alert indicators on navigation links
- Multi-user / team-level role-based navigation items
- Dark mode toggle (preference is stored and applied by feature 0014)

---

## Stories

### US-001: Authenticated Shell Layout

**As a** QA lead
**I want to** see a consistent header and sidebar on every authenticated page
**So that** I can orient myself and navigate between sections without friction regardless of which page I am on

#### Acceptance Criteria

- [ ] AC-001: Every authenticated page is rendered inside a shared shell layout that provides the top header and left sidebar; unauthenticated pages (marketing, sign-in) do not use this layout
- [ ] AC-002: The shell layout is implemented as a Next.js layout segment (`layout.tsx`) scoped to the authenticated route group so it applies automatically to every page inside the group
- [ ] AC-003: The header spans the full width of the viewport at the top; the sidebar is fixed to the left edge and runs the full remaining height below the header
- [ ] AC-004: The main content area fills the remaining space to the right of the sidebar and below the header; content within it scrolls independently of the header and sidebar
- [ ] AC-005: The shell layout renders correctly at viewport widths from 1024 px upward; behaviour below 1024 px is undefined for v1

#### Edge Cases

- If the user navigates directly to an authenticated URL while unauthenticated, the layout must not render — the auth guard redirects before the layout mounts
- Layout must not cause cumulative layout shift (CLS) on initial load

---

### US-002: Header — Logo and User Identity

**As a** QA lead
**I want to** see the Testurio logo and my display name and avatar in the header at all times
**So that** I know I am in the right product and signed in as the correct user

#### Acceptance Criteria

- [ ] AC-006: The header displays the Testurio wordmark / logo on the left side; clicking the logo navigates to `/dashboard`
- [ ] AC-007: The header displays the signed-in user's avatar on the right side; the avatar renders as an MUI `Avatar` component using the user's profile picture URL if available, falling back to the user's initials derived from `displayName` if no picture URL is set
- [ ] AC-008: The header displays the signed-in user's `displayName` to the left of the avatar on the right side; the display name is truncated with an ellipsis if it exceeds 24 characters
- [ ] AC-009: The user identity section (avatar + display name) is read from the auth session; it is not fetched via a separate API call on every render
- [ ] AC-010: The header has a fixed height of 64 px and uses `position: sticky; top: 0` so it remains visible when the main content scrolls
- [ ] AC-011: The header includes a bottom border separator to visually separate it from the content area below

#### Edge Cases

- If `displayName` is empty or null, fall back to the user's email address prefix (the part before `@`)
- Avatar initial must use only the first letter of the first word of `displayName`; multi-word names do not produce two initials in this v1

---

### US-003: Sidebar — Primary Navigation Links

**As a** QA lead
**I want to** see navigation links for Dashboard and Projects in the left sidebar
**So that** I can switch between the main sections of the portal in one click from any authenticated page

#### Acceptance Criteria

- [ ] AC-012: The sidebar contains exactly two primary navigation links in this order: **Dashboard** (`/dashboard`) and **Projects** (`/projects`)
- [ ] AC-013: Each navigation link renders as an MUI `ListItemButton` with an icon on the left and the link label to the right
- [ ] AC-014: **Dashboard** uses the MUI `DashboardOutlined` icon; **Projects** uses the MUI `FolderOutlined` icon
- [ ] AC-015: The active link — the one whose route matches the current URL path (prefix match) — is highlighted with the theme's `primary.main` background and `primary.contrastText` foreground; all other links render with the default `text.primary` foreground and no background fill
- [ ] AC-016: Navigation links use Next.js `Link` for client-side routing; they do not cause a full page reload
- [ ] AC-017: The sidebar includes a **Settings** link below the primary links, visually separated by a `Divider`; it navigates to `/settings`; it uses the MUI `SettingsOutlined` icon

#### Edge Cases

- If the current path is `/projects/abc123/history`, the **Projects** link is treated as active (prefix match on `/projects`)
- If the current path is `/settings`, only the Settings link is active — neither Dashboard nor Projects is highlighted

---

### US-004: Sidebar — Collapse / Expand

**As a** QA lead
**I want to** collapse the sidebar to icon-only width and expand it back
**So that** I can reclaim horizontal space for the main content when working on wide pages

#### Acceptance Criteria

- [ ] AC-018: The sidebar has two visual states: **expanded** (full width, showing icons and labels) and **collapsed** (icon-only width, showing icons only)
- [ ] AC-019: A toggle button (chevron icon) is visible at the top of the sidebar; clicking it switches between expanded and collapsed states
- [ ] AC-020: Expanded sidebar width is 240 px; collapsed sidebar width is 64 px; both values come from theme spacing or named constants — not magic numbers inline
- [ ] AC-021: The main content area reflows smoothly when the sidebar width changes; the transition uses CSS `transition: width 200ms ease`
- [ ] AC-022: The collapsed state is persisted in `localStorage` under the key `testurio.sidebarCollapsed` so that the user's preference survives a page refresh
- [ ] AC-023: When collapsed, navigation link labels are hidden and a tooltip showing the link label appears on hover over each icon button
- [ ] AC-024: The toggle chevron icon rotates 180° when transitioning between states using CSS transition, matching the sidebar width transition duration

#### Edge Cases

- On first visit with no `localStorage` key set, the sidebar defaults to expanded
- If `localStorage` is unavailable (private browsing restriction), the sidebar defaults to expanded and does not throw

---

### US-005: Sidebar — Sign Out

**As a** QA lead
**I want to** sign out from within the sidebar at any time
**So that** I can end my session securely without navigating to a separate settings page

#### Acceptance Criteria

- [ ] AC-025: A **Sign Out** action is pinned at the bottom of the sidebar, below the Settings link, separated by a `Divider`
- [ ] AC-026: Sign Out renders as an MUI `ListItemButton` with the MUI `LogoutOutlined` icon and the label "Sign Out"
- [ ] AC-027: Clicking Sign Out triggers the Azure AD B2C logout redirect; the user is sent to the B2C logout endpoint and then redirected to the public landing page (`/`)
- [ ] AC-028: While the logout redirect is initiating, the Sign Out button is disabled and shows a loading spinner in place of the icon to prevent double-clicks
- [ ] AC-029: Sign Out is always visible regardless of the sidebar's expanded or collapsed state; in collapsed state it shows the icon only with a tooltip "Sign Out"

#### Edge Cases

- If the logout redirect fails or times out, the user sees an error toast ("Sign out failed — please try again") and the button returns to its normal state
- Sign Out must call the B2C logout endpoint, not merely clear the local session cookie; calling only the local clear would leave the B2C session active

---

### US-006: Auth Guard — Redirect Unauthenticated Users

**As an** unauthenticated visitor
**I want to** be redirected to the sign-in page when I access a protected URL
**So that** authenticated content is never visible without a valid session

#### Acceptance Criteria

- [ ] AC-030: Every route inside the authenticated layout group is protected by an auth guard; accessing any such route without a valid session redirects the user to the sign-in page (feature 0013's route)
- [ ] AC-031: The original URL the user attempted to visit is preserved as a `returnUrl` query parameter on the sign-in redirect so the user can be returned there after authentication
- [ ] AC-032: The auth guard runs server-side (Next.js middleware or layout-level redirect) — the protected page content is never sent to the client for unauthenticated requests
- [ ] AC-033: Authenticated users who visit the root `/` are redirected to `/dashboard`

#### Edge Cases

- A session that expires mid-visit causes the next authenticated API call to return `401`; the API client intercepts the `401` and triggers a re-authentication redirect (handled by the global `apiClient` — not duplicated per page)
- Direct navigation to `/dashboard` with an expired token must redirect to sign-in, not render a broken dashboard

---

### US-007: Layout Accessibility

**As a** QA lead using a keyboard or screen reader
**I want to** navigate the shell layout using standard keyboard interactions
**So that** I can access all sections of the portal without requiring a mouse

#### Acceptance Criteria

- [ ] AC-034: All navigation links and interactive elements in the header and sidebar are reachable via `Tab` key in logical reading order (header left-to-right, then sidebar top-to-bottom)
- [ ] AC-035: The active navigation link has a visible focus ring that meets WCAG 2.1 AA contrast requirements
- [ ] AC-036: Screen readers announce each navigation link label correctly; icon-only elements (e.g. collapsed sidebar icons) have `aria-label` attributes
- [ ] AC-037: The sidebar toggle button has `aria-label="Collapse sidebar"` when expanded and `aria-label="Expand sidebar"` when collapsed
- [ ] AC-038: The Sign Out button has `aria-label="Sign out"` and is announced as a button, not a link

#### Edge Cases

- Keyboard navigation must not become trapped inside the sidebar; pressing `Tab` from the last sidebar item must move focus to the main content area
