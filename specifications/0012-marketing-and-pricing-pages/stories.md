# User Stories — Marketing & Pricing Pages (0012)

## Out of Scope

The following are explicitly **not** part of this feature:

- Stripe Checkout flow and subscription creation (feature 0015)
- Authenticated billing interval toggle with live plan selection (feature 0015)
- Trial-status banners and upgrade modals inside the portal (feature 0015)
- Account settings, dashboard, or any authenticated portal pages
- Blog, changelog, or documentation pages
- Contact form with backend submission
- Cookie consent banner
- A/B testing or analytics event tracking

---

## Stories

### US-001: Landing Page — Hero Section

**As a** first-time visitor
**I want to** immediately understand what Testurio does and who it is for
**So that** I can decide whether to explore further without scrolling past the fold

#### Acceptance Criteria

- [ ] AC-001: The landing page (`/`) renders a full-width hero section as the first visible element with no vertical scroll required on a 1440 px wide viewport.
- [ ] AC-002: The hero contains a headline (≤ 12 words), a subheadline (≤ 30 words), a primary CTA button ("Get started free" → `/auth/register`), and a secondary CTA button ("See how it works" → scrolls to the How It Works section).
- [ ] AC-003: The hero section renders correctly on viewport widths 375 px, 768 px, and 1440 px with no horizontal overflow.

---

### US-002: Landing Page — Key Features Section

**As a** visitor evaluating Testurio
**I want to** see a concise overview of its core capabilities
**So that** I can assess fit without reading long documentation

#### Acceptance Criteria

- [ ] AC-004: The landing page contains a Features section below the hero, presenting exactly six feature highlights in a responsive grid (3-column on desktop, 2-column on tablet, 1-column on mobile).
- [ ] AC-005: Each feature highlight displays an icon, a short title (≤ 5 words), and a description (≤ 20 words).
- [ ] AC-006: Feature highlights covered: AI Test Generation, Automatic Triggering, API Testing, UI End-to-End Testing, PM Tool Integration, Test Memory Layer.

---

### US-003: Landing Page — How It Works Section

**As a** visitor who wants to understand the workflow
**I want to** see a step-by-step explanation of how Testurio fits into my process
**So that** I can evaluate the integration effort before signing up

#### Acceptance Criteria

- [ ] AC-007: The landing page contains a "How It Works" section with exactly five numbered steps in a vertical or horizontal timeline layout.
- [ ] AC-008: Steps are: (1) Connect your PM tool, (2) Move a story to "In Testing", (3) AI reads the story and generates scenarios, (4) Tests run automatically, (5) Results posted back to your ticket.
- [ ] AC-009: Each step has a short title and a single sentence description.
- [ ] AC-010: The section has an `id="how-it-works"` attribute so the hero secondary CTA can scroll to it.

---

### US-004: Landing Page — Pricing Teaser Section

**As a** visitor considering Testurio
**I want to** see a brief preview of pricing before deciding to explore further
**So that** I know whether the product is within my budget without navigating away

#### Acceptance Criteria

- [ ] AC-011: The landing page contains a Pricing section with a headline, a one-sentence summary of the pricing model ("Simple, transparent pricing. Start free, scale as you grow."), and a "See all plans" button that navigates to `/pricing`.
- [ ] AC-012: The Pricing section displays the four plan names (Test Junior, Test Pro, Team, Centurio) with their monthly starting price and a single highlighted feature each.
- [ ] AC-013: The teaser does not replicate the full feature comparison table; it only surfaces the plan names, entry prices, and one key differentiator per plan.

---

### US-005: Landing Page — Footer

**As a** visitor who has scrolled to the bottom of the landing page
**I want to** find navigation links and company information
**So that** I can reach other public pages or understand who runs Testurio

#### Acceptance Criteria

- [ ] AC-014: The footer appears at the bottom of every public page (landing and pricing).
- [ ] AC-015: The footer contains: Testurio logo, nav links (Home, Pricing), a copyright notice ("© 2026 Testurio. All rights reserved."), and placeholder links for Privacy Policy and Terms of Service (links are `#` in this feature; real pages are out of scope).
- [ ] AC-016: The footer renders correctly at viewport widths 375 px, 768 px, and 1440 px.

---

### US-006: Public Site Header

**As a** visitor on any public page
**I want to** see a clear, persistent navigation header with sign-in access
**So that** I can navigate and reach the sign-up or sign-in page at any time

#### Acceptance Criteria

- [ ] AC-017: Every public page renders a sticky header containing: Testurio logo (left), nav links (Home, Pricing) in the centre, and an action area (right).
- [ ] AC-018: For unauthenticated visitors, the action area shows a "Sign In" link (→ `/auth/login`) and a "Get Started" button (→ `/auth/register`).
- [ ] AC-019: For authenticated users visiting public pages, the action area shows a single "Go to Dashboard" button (→ `/dashboard`) and the Sign In / Get Started elements are hidden.
- [ ] AC-020: The header is visually distinct from the authenticated portal's `AppLayout` sidebar shell — it is a horizontal top bar only with no sidebar.
- [ ] AC-021: The active nav link is visually highlighted.
- [ ] AC-022: On viewports narrower than 768 px the nav links collapse into a hamburger menu; the action buttons remain visible.

---

### US-007: Pricing Page — Plan Comparison

**As a** visitor ready to evaluate plans
**I want to** compare available subscription tiers side by side
**So that** I can choose the plan that fits my team's needs and budget

#### Acceptance Criteria

- [ ] AC-023: The pricing page (`/pricing`) renders four plan cards in order: Test Junior, Test Pro, Team, Centurio.
- [ ] AC-024: Each plan card displays: plan name, monthly price, annual price (when annual billing is selected), a feature checklist (≥ 5 items), and a "Get started free" CTA button.
- [ ] AC-025: Plan definitions (name, monthly price, annual price, annual discount percent, feature list) are fetched from `GET /v1/plans` and rendered dynamically — prices are not hardcoded in the component.
- [ ] AC-026: While plan data is loading, skeleton placeholders are shown in place of the plan cards (no layout shift).
- [ ] AC-027: If the API call fails, an inline error message is displayed with a "Try again" retry button; the page does not crash.
- [ ] AC-028: The Test Pro card is visually highlighted as "Most popular" with a badge or elevated card style.

---

### US-008: Pricing Page — Billing Interval Toggle

**As a** visitor comparing plans
**I want to** switch between monthly and annual pricing views
**So that** I can see the cost saving before choosing a plan

#### Acceptance Criteria

- [ ] AC-029: The pricing page displays a Monthly / Annual toggle above the plan grid; Monthly is selected by default.
- [ ] AC-030: Switching to Annual updates all four plan cards simultaneously to show the annual monthly-equivalent price and the discount badge (e.g. "Save 20%").
- [ ] AC-031: The annual price shown per card is the monthly-equivalent (annual total ÷ 12, rounded to the nearest dollar), not the annual lump sum.
- [ ] AC-032: The discount percentage displayed on the toggle matches the `annualDiscountPercent` value returned by the API.

---

### US-009: Pricing Page — Call to Action

**As a** visitor who has chosen a plan
**I want to** start the sign-up process directly from the pricing page
**So that** I can begin my free trial without navigating back to the landing page

#### Acceptance Criteria

- [ ] AC-033: Clicking a plan card's CTA button navigates an unauthenticated visitor to `/auth/register?plan=<planId>&interval=<monthly|annual>` so the chosen plan is preserved across authentication.
- [ ] AC-034: For an authenticated user visiting `/pricing`, the CTA button label changes to "Upgrade" and the button links to `/billing?plan=<planId>&interval=<monthly|annual>` (the full checkout flow handled by feature 0015).
- [ ] AC-035: The plan ID embedded in the query param matches the `id` field returned by `GET /v1/plans`.

---

### US-010: Public Pages — Responsiveness

**As a** visitor on a mobile device
**I want to** use the marketing site without horizontal scrolling or broken layouts
**So that** I can evaluate Testurio from any device

#### Acceptance Criteria

- [ ] AC-036: All public pages (landing and pricing) render without horizontal overflow at viewport widths 375 px, 768 px, and 1440 px.
- [ ] AC-037: On the pricing page, the four plan cards stack in a single column on viewports narrower than 900 px and display in a 4-column grid on viewports 1200 px and wider; 2-column layout is used between those breakpoints.
- [ ] AC-038: Touch targets (buttons, links) are at minimum 44 × 44 px on mobile.

---

### US-011: Backend — Public Plans Endpoint

**As a** frontend page rendering plan information
**I want to** fetch authoritative plan definitions from the API
**So that** prices and features can be updated centrally without a frontend deployment

#### Acceptance Criteria

- [ ] AC-039: `GET /v1/plans` returns an array of plan definition objects in order: Test Junior, Test Pro, Team, Centurio.
- [ ] AC-040: Each object includes: `id` (string slug), `name` (display name), `monthlyPrice` (integer, USD), `annualPrice` (integer, annual total USD), `annualDiscountPercent` (integer), `isPopular` (boolean), and `features` (string array, ≥ 5 items per plan).
- [ ] AC-041: The endpoint requires no authentication — it must be accessible without a JWT.
- [ ] AC-042: The endpoint returns HTTP 200 with `Content-Type: application/json` and a `Cache-Control: public, max-age=3600` header.
- [ ] AC-043: Plan data is defined as a configuration constant in the API — no database read is required.
