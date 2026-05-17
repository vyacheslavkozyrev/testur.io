# Implementation Plan — Marketing & Pricing Pages (0012)

## Tasks

### Backend

- [x] T001 [App] Create `PlanDefinitionDto` DTO (id, name, monthlyPrice, annualPrice, annualDiscountPercent, isPopular, features) — `source/Testurio.Api/DTOs/Plans/PlanDefinitionDto.cs`
- [x] T002 [Config] Define `PlanCatalog` static class with four plan constants (Test Junior / Test Pro / Team / Centurio) — `source/Testurio.Api/Configuration/PlanCatalog.cs`
- [x] T003 [API] Add `GET /v1/plans` public endpoint (no auth; returns `PlanCatalog.All`; sets `Cache-Control: public, max-age=3600`) — `source/Testurio.Api/Endpoints/PlanEndpoints.cs`
- [x] T004 [API] Register `PlanEndpoints` in `Program.cs` — `source/Testurio.Api/Program.cs`

### Frontend — Shared Public Layout

- [x] T005 [UI] Add plan types — `source/Testurio.Web/src/types/plan.types.ts`
  - `PlanDefinition` (id, name, monthlyPrice, annualPrice, annualDiscountPercent, isPopular, features)
  - `BillingInterval` enum (`monthly | annual`)
- [x] T006 [UI] Add plans API client — `source/Testurio.Web/src/services/plans/plansService.ts`
  - `list(): Promise<PlanDefinition[]>`
- [x] T007 [UI] Add `usePlans` React Query hook — `source/Testurio.Web/src/hooks/usePlans.ts`
  - `queryKey: ['plans']`; `staleTime: 60 * 60 * 1000` (matches server `max-age`)
- [x] T008 [UI] Add MSW mock handler for `GET /v1/plans` — `source/Testurio.Web/src/mocks/handlers/plans.ts`
- [x] T009 [UI] Create `PublicHeader` component (sticky; logo left; nav links centre; auth-aware action area right: Sign In + Get Started for guests, Go to Dashboard for signed-in users; hamburger collapse on mobile) — `source/Testurio.Web/src/components/PublicHeader/PublicHeader.tsx`
- [ ] T010 [UI] Create `PublicFooter` component (logo, nav links, copyright, Privacy Policy and Terms of Service placeholder links) — `source/Testurio.Web/src/components/PublicFooter/PublicFooter.tsx`
- [ ] T011 [UI] Create `PublicLayout` wrapper (renders `PublicHeader` + `{children}` + `PublicFooter`; no sidebar) — `source/Testurio.Web/src/components/PublicLayout/PublicLayout.tsx`

### Frontend — Landing Page

- [ ] T012 [UI] Create `HeroSection` component (headline, subheadline, primary CTA → `/auth/register`, secondary CTA scroll-to `#how-it-works`) — `source/Testurio.Web/src/components/HeroSection/HeroSection.tsx`
- [ ] T013 [UI] Create `FeaturesSection` component (responsive 3-2-1 column grid; six feature tiles each with icon, title, description) — `source/Testurio.Web/src/components/FeaturesSection/FeaturesSection.tsx`
- [ ] T014 [UI] Create `HowItWorksSection` component (`id="how-it-works"`; five numbered steps in timeline layout; each with title + one-sentence description) — `source/Testurio.Web/src/components/HowItWorksSection/HowItWorksSection.tsx`
- [ ] T015 [UI] Create `PricingTeaserSection` component (headline; summary text; four plan name + entry price + one key feature; "See all plans" button → `/pricing`) — `source/Testurio.Web/src/components/PricingTeaserSection/PricingTeaserSection.tsx`
- [ ] T016 [UI] Create `LandingPage` page (wraps with `PublicLayout`; composes `HeroSection`, `FeaturesSection`, `HowItWorksSection`, `PricingTeaserSection` in order) — `source/Testurio.Web/src/pages/LandingPage/LandingPage.tsx`

### Frontend — Pricing Page

- [ ] T017 [UI] Create `BillingIntervalToggle` component (Monthly / Annual toggle; Monthly selected by default; emits `onChange(interval: BillingInterval)`) — `source/Testurio.Web/src/components/BillingIntervalToggle/BillingIntervalToggle.tsx`
- [ ] T018 [UI] Create `PlanCard` component (plan name, feature checklist, monthly/annual price, annual discount badge when interval=annual, "Most popular" badge when `isPopular`, CTA button — label and href determined by auth state: "Get started free" → `/auth/register?plan=<id>&interval=<interval>` for guests; "Upgrade" → `/billing?plan=<id>&interval=<interval>` for authenticated users) — `source/Testurio.Web/src/components/PlanCard/PlanCard.tsx`
- [ ] T019 [UI] Create `PricingPage` page (wraps with `PublicLayout`; renders `BillingIntervalToggle`; calls `usePlans`; shows skeleton placeholders while loading; shows inline error with retry on failure; renders four `PlanCard` components in a responsive 4-2-1 column grid) — `source/Testurio.Web/src/pages/PricingPage/PricingPage.tsx`

### Frontend — i18n & Routing

- [ ] T020 [UI] Add landing page translation keys — `source/Testurio.Web/src/locales/en/landing.json`
- [ ] T021 [UI] Add pricing page translation keys — `source/Testurio.Web/src/locales/en/pricing.json`
- [ ] T022 [UI] Register `/` route (→ `LandingPage`) and `/pricing` route (→ `PricingPage`) as public routes — `source/Testurio.Web/src/routes/routes.tsx`

### Tests

- [ ] T023 [Test] Frontend component tests for `PublicHeader` (renders Sign In and Get Started for unauthenticated; renders Go to Dashboard for authenticated; active link is highlighted; hamburger visible below 768 px) — `source/Testurio.Web/src/components/PublicHeader/PublicHeader.test.tsx`
- [ ] T024 [Test] Frontend component tests for `PlanCard` (renders plan name, features, monthly price; renders annual price and discount badge when interval=annual; renders "Most popular" badge when isPopular; CTA href contains plan id and interval; authenticated variant shows "Upgrade" label and billing href) — `source/Testurio.Web/src/components/PlanCard/PlanCard.test.tsx`
- [ ] T025 [Test] Frontend component tests for `PricingPage` (shows skeletons while loading; renders four plan cards on success; shows error state with retry button on failure; toggling to Annual updates all card prices simultaneously) — `source/Testurio.Web/src/pages/PricingPage/PricingPage.test.tsx`
- [ ] T026 [Test] E2E tests — `source/Testurio.Web/e2e/marketing.spec.ts`
  - Landing page loads at `/` with hero, features, how-it-works, pricing teaser, and footer all visible
  - "See how it works" CTA scrolls to the How It Works section
  - "See all plans" teaser button navigates to `/pricing`
  - Pricing page loads four plan cards; Test Pro card has "Most popular" badge
  - Annual toggle updates all card prices and shows discount badges
  - Clicking a plan CTA redirects unauthenticated visitor to `/auth/register` with `plan` and `interval` query params
  - Authenticated user visiting `/pricing` sees CTA links pointing to `/billing`
  - Public header shows "Go to Dashboard" for authenticated users
  - All pages render without horizontal overflow at 375 px viewport

---

## Rationale

**Backend before frontend.** `PlanCatalog` (T002) and the `GET /v1/plans` endpoint (T003) are implemented first so the `usePlans` hook and mock handler are built against a known response shape. The DTO (T001) defines that shape precisely; frontend types in `plan.types.ts` (T005) mirror it exactly, keeping serialisation implicit.

**No database read for plans.** Plan definitions are business constants that change only on deliberate pricing decisions, not per-request data. Defining them as a static `PlanCatalog` class (T002) avoids a Cosmos read on every page load, aligns with the `Cache-Control: public, max-age=3600` cache strategy, and removes a Cosmos dependency from an otherwise unauthenticated hot path.

**`PublicLayout` before page components.** `PublicHeader`, `PublicFooter`, and `PublicLayout` (T009–T011) are shared by both `LandingPage` and `PricingPage`. Building them first means each page can be assembled by composition without duplicating header/footer markup.

**`usePlans` stale time matches server cache TTL.** Setting `staleTime: 3600000` in the React Query hook prevents redundant refetches within the same browser session and aligns with the API's `max-age=3600`, so the plan grid never flickers between navigations.

**`BillingIntervalToggle` and `PlanCard` before `PricingPage`.** The page component is a composition of its children. Both leaf components must be complete and testable in isolation before the page assembles them, following the bottom-up component build order required by the UI layer rules.

**CTA href strategy for 0015 compatibility.** `PlanCard` encodes `plan` and `interval` as query params on the sign-up redirect URL (AC-033). Feature 0015 reads those params after authentication to pre-select the plan and trigger checkout without an extra pricing-page visit. This is a deliberate handoff point — 0012 writes the params, 0015 consumes them — so the param names must not change between features.

**`PricingPage` from 0012 is the foundation for 0015.** Feature 0015 (T021–T025 in its own plan) creates a `PricingPage` with billing interval toggle and checkout CTAs. Because 0015 is implemented after 0012, 0015's `PricingPage` task should replace the 0012 version entirely rather than extend it — the component is simple enough that a full replacement is cleaner than partial augmentation. The `PlanCard` and `BillingIntervalToggle` components from 0012 are reused by 0015.

**Tests last.** All test tasks (T023–T026) follow every implementation task, consistent with the `[Test]` layer rule. The E2E suite (T026) is last because it exercises the full stack and depends on all components and routes being complete.

---

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, enums, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, external clients, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services — `Testurio.Api` |
| `[API]` | Minimal API endpoint groups, route registration — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |
| `[UI]` | Types, API clients, React Query hooks, MSW handlers, components, pages, i18n keys, route registration — `Testurio.Web` |
| `[Test]` | Unit, integration, frontend component, and E2E test files |
