# Implementation Plan — Plan Purchase (0015)

## Tasks

### Backend — Domain

- [x] T001 [Domain] Create `SubscriptionPlan` enum (TestJunior / TestPro / Team / Centurio) — `source/Testurio.Core/Enums/SubscriptionPlan.cs`
- [ ] T002 [Domain] Create `BillingInterval` enum (Monthly / Annual) — `source/Testurio.Core/Enums/BillingInterval.cs`
- [ ] T003 [Domain] Create `SubscriptionStatus` enum (None / Trialing / Active / Expired) — `source/Testurio.Core/Enums/SubscriptionStatus.cs`
- [ ] T004 [Domain] Create `UserSubscription` entity (userId, plan, interval, status, trialEnd, stripeCustomerId, stripeSubscriptionId) — `source/Testurio.Core/Entities/UserSubscription.cs`
- [ ] T005 [Domain] Add `IUserSubscriptionRepository` interface (GetByUserIdAsync, UpsertAsync) — `source/Testurio.Core/Repositories/IUserSubscriptionRepository.cs`
- [ ] T006 [Domain] Add `IStripeService` interface (CreateCheckoutSessionAsync, GetSubscriptionStatusAsync) — `source/Testurio.Core/Interfaces/IStripeService.cs`

### Backend — Infrastructure

- [ ] T007 [Infra] Implement `UserSubscriptionRepository` (Cosmos DB, partitioned by userId) — `source/Testurio.Infrastructure/Cosmos/UserSubscriptionRepository.cs`
- [ ] T008 [Infra] Add `StripeOptions` configuration class (SecretKey, WebhookSecret, Price ID map keyed by plan+interval) — `source/Testurio.Infrastructure/Stripe/StripeOptions.cs`
- [ ] T009 [Infra] Implement `StripeService` (Stripe.net SDK; CreateCheckoutSession with trial_period_days=14, customer_email, correct cancel/success URLs; GetSubscriptionStatus from Stripe API) — `source/Testurio.Infrastructure/Stripe/StripeService.cs`
- [ ] T010 [Infra] Register `UserSubscriptionRepository`, `StripeService`, and `StripeOptions` in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`

### Backend — Application

- [ ] T011 [App] Create `CreateCheckoutSessionRequest` DTO (plan, billingInterval) — `source/Testurio.Api/DTOs/Billing/CreateCheckoutSessionRequest.cs`
- [ ] T012 [App] Create `CheckoutSessionResponse` DTO (checkoutUrl) — `source/Testurio.Api/DTOs/Billing/CheckoutSessionResponse.cs`
- [ ] T013 [App] Create `SubscriptionStatusResponse` DTO (status, plan, billingInterval, trialEndsAt) — `source/Testurio.Api/DTOs/Billing/SubscriptionStatusResponse.cs`
- [ ] T014 [App] Implement `BillingService` (CreateCheckoutSessionAsync delegates to IStripeService; GetSubscriptionStatusAsync reads UserSubscription from Cosmos; HandleStripeWebhookAsync upserts UserSubscription on checkout.session.completed and customer.subscription.updated events) — `source/Testurio.Api/Services/BillingService.cs`

### Backend — API

- [ ] T015 [API] Add `/api/billing` endpoint group with three routes — `source/Testurio.Api/Endpoints/BillingEndpoints.cs`
  - `POST /api/billing/checkout` — authenticated (B2C JWT required); calls BillingService.CreateCheckoutSessionAsync; returns `CheckoutSessionResponse`
  - `GET /api/billing/subscription` — authenticated; calls BillingService.GetSubscriptionStatusAsync; returns `SubscriptionStatusResponse`
  - `POST /webhooks/stripe` — unauthenticated; validates Stripe-Signature header using WebhookSecret; dispatches to BillingService.HandleStripeWebhookAsync; returns 400 on invalid signature
- [ ] T016 [API] Register BillingEndpoints in `Program.cs` — `source/Testurio.Api/Program.cs`

### Frontend — Pricing Page (public)

- [ ] T017 [UI] Add billing API types — `source/Testurio.Web/src/types/billing.types.ts`
  - `SubscriptionPlan`, `BillingInterval`, `SubscriptionStatus` enums
  - `PlanDefinition` (id, name, features, monthlyPrice, annualPrice, annualDiscountPercent)
  - `CreateCheckoutSessionRequest`, `CheckoutSessionResponse`, `SubscriptionStatusResponse`
- [ ] T018 [UI] Add billing API client — `source/Testurio.Web/src/services/billing/billingService.ts`
  - `createCheckoutSession(plan, interval): Promise<CheckoutSessionResponse>`
  - `getSubscriptionStatus(): Promise<SubscriptionStatusResponse>`
- [ ] T019 [UI] Add React Query hooks — `source/Testurio.Web/src/hooks/useBilling.ts`
  - `useSubscriptionStatus()` — polls GET /api/billing/subscription; exposes status and trialDaysRemaining; stops polling once terminal status reached
  - `useCreateCheckoutSession()` — useMutation wrapping billingService.createCheckoutSession; on success redirects browser to checkoutUrl
- [ ] T020 [UI] Add MSW mock handlers for billing endpoints — `source/Testurio.Web/src/mocks/handlers/billing.ts`
- [ ] T021 [UI] Create `PlanCard` component (name, feature highlights, monthly/annual price, annual discount badge, "Start free trial" CTA button) — `source/Testurio.Web/src/components/PlanCard/PlanCard.tsx`
- [ ] T022 [UI] Create `BillingIntervalToggle` component (Monthly / Annual switch; switches all PlanCard prices simultaneously) — `source/Testurio.Web/src/components/BillingIntervalToggle/BillingIntervalToggle.tsx`
- [ ] T023 [UI] Create `PricingPage` page (four-plan grid in order: Test Junior, Test Pro, Team, Centurio; interval toggle; trial callout in header; unauthenticated CTA preserves plan+interval as query params for post-auth redirect; authenticated CTA calls useCreateCheckoutSession) — `source/Testurio.Web/src/pages/PricingPage/PricingPage.tsx`
- [ ] T024 [UI] Add pricing translation keys — `source/Testurio.Web/src/locales/en/pricing.json`
- [ ] T025 [UI] Register `/pricing` public route — `source/Testurio.Web/src/routes/routes.tsx`

### Frontend — Checkout Success Page

- [ ] T026 [UI] Create `CheckoutSuccessPage` page (reads session_id query param; redirects to /pricing if absent; calls useSubscriptionStatus polling every 3 s; shows loading state → confirmation + "Create your first project" CTA on trialing status → support message after 30 s timeout) — `source/Testurio.Web/src/pages/CheckoutSuccessPage/CheckoutSuccessPage.tsx`
- [ ] T027 [UI] Add checkout success translation keys — `source/Testurio.Web/src/locales/en/checkoutSuccess.json`
- [ ] T028 [UI] Register `/billing/success` route — `source/Testurio.Web/src/routes/routes.tsx`

### Frontend — Portal Banners and Upgrade Gate

- [ ] T029 [UI] Create `TrialStatusBanner` component (shows "X days remaining" banner on dashboard; amber MUI Alert style when ≤3 days remain; "Upgrade now" CTA links to /pricing; "Trial expired" variant for expired state; hidden when subscription is active) — `source/Testurio.Web/src/components/TrialStatusBanner/TrialStatusBanner.tsx`
- [ ] T030 [UI] Create `UpgradeModal` component (shown when a gated action is attempted with no active plan or expired trial; links to /pricing?interval=monthly) — `source/Testurio.Web/src/components/UpgradeModal/UpgradeModal.tsx`
- [ ] T031 [UI] Integrate `TrialStatusBanner` into the authenticated portal layout so it appears on every portal page — `source/Testurio.Web/src/components/AppLayout/AppLayout.tsx`
- [ ] T032 [UI] Gate "Create project" action with `UpgradeModal` when subscription status is None or Expired — project creation component/page
- [ ] T033 [UI] Gate "Trigger test run" action with `UpgradeModal` when subscription status is None or Expired — test-run trigger component/page
- [ ] T034 [UI] Add portal billing translation keys — `source/Testurio.Web/src/locales/en/billing.json`

### Tests

- [ ] T035 [Test] Unit tests for `BillingService` (CreateCheckoutSession maps each plan+interval to the correct Price ID; GetSubscriptionStatus returns correct DTO; HandleStripeWebhookAsync upserts subscription on checkout.session.completed; updates status on customer.subscription.updated) — `tests/Testurio.UnitTests/Services/BillingServiceTests.cs`
- [ ] T036 [Test] Unit tests for `StripeService` (CheckoutSession created with trial_period_days=14, customer_email, correct cancel_url and success_url) — `tests/Testurio.UnitTests/Infrastructure/StripeServiceTests.cs`
- [ ] T037 [Test] Integration tests for billing endpoints (POST /api/billing/checkout returns checkoutUrl; GET /api/billing/subscription returns correct status; POST /webhooks/stripe with invalid Stripe-Signature returns 400; valid webhook upserts UserSubscription in Cosmos) — `tests/Testurio.IntegrationTests/Controllers/BillingControllerTests.cs`
- [ ] T038 [Test] Frontend component tests for `PlanCard` (renders plan name, features, price; CTA calls onSelect; annual discount badge visible when interval is Annual; hidden when interval is Monthly) — `source/Testurio.Web/src/components/PlanCard/PlanCard.test.tsx`
- [ ] T039 [Test] Frontend component tests for `TrialStatusBanner` (shows correct days remaining; applies amber style at ≤3 days; renders expired variant when status is Expired; does not render when status is Active) — `source/Testurio.Web/src/components/TrialStatusBanner/TrialStatusBanner.test.tsx`
- [ ] T040 [Test] Frontend component tests for `CheckoutSuccessPage` (redirects to /pricing when session_id is absent; shows loading spinner initially; shows confirmation and CTA when status becomes trialing; shows timeout support message after 30 s) — `source/Testurio.Web/src/pages/CheckoutSuccessPage/CheckoutSuccessPage.test.tsx`
- [ ] T041 [Test] E2E tests — `source/Testurio.Web/e2e/plan-purchase.spec.ts`
  - Visitor clicks "Start free trial", is redirected to sign-in, and is forwarded to Stripe Checkout after auth with plan+interval preserved
  - Authenticated user selects a plan on /pricing and is taken to Stripe Checkout
  - /billing/success polls and shows confirmation when subscription status flips to trialing
  - /billing/success shows support message after 30 s without status resolution
  - Trial banner appears on dashboard; turns amber at ≤3 days; disappears after plan is purchased
  - Attempting to create a project with no active plan shows UpgradeModal
  - Abandoned Stripe Checkout returns user to /pricing with account state unchanged

---

## Rationale

**Domain enums before entity.** `SubscriptionPlan`, `BillingInterval`, and `SubscriptionStatus` (T001–T003) define the shared vocabulary used by `UserSubscription` (T004), `IUserSubscriptionRepository` (T005), and `IStripeService` (T006). Enums must exist before anything that references them.

**`IStripeService` in Core, implementation in Infrastructure.** The interface (T006) belongs in `Testurio.Core` so `BillingService` in `Testurio.Api` can depend on an abstraction. The concrete `StripeService` (T009) lives in `Testurio.Infrastructure` alongside the Jira and Key Vault clients, keeping third-party SDK references isolated from the domain and application layers — consistent with the existing pattern.

**`StripeOptions` before `StripeService`.** The options class (T008) defines the configuration shape (Price ID map, SecretKey, WebhookSecret) that `StripeService` reads at startup. Declaring it first allows `ValidateDataAnnotations().ValidateOnStart()` to catch misconfiguration before the first request.

**`BillingService` owns all Stripe event dispatch.** Rather than a separate webhook service, `BillingService` (T014) handles the two relevant Stripe event types (`checkout.session.completed`, `customer.subscription.updated`). Both map directly to UserSubscription upsert operations that BillingService already manages, keeping all billing state transitions in one place without an extra indirection layer.

**Stripe webhook endpoint is unauthenticated by design.** Stripe does not present a B2C JWT; it authenticates via the `Stripe-Signature` HMAC header instead. The webhook route (T015) sits under `/webhooks/stripe` alongside the existing `/webhooks/ado` and `/webhooks/jira` routes, verified against `StripeOptions.WebhookSecret`. The other two billing endpoints (`/api/billing/checkout`, `/api/billing/subscription`) require a valid B2C JWT and scope all Cosmos reads to the authenticated `userId`, consistent with the multi-tenancy model.

**Plan+interval preserved across the auth redirect.** `PricingPage` (T023) encodes `?plan=<id>&interval=<monthly|annual>` on the sign-in redirect URL. After successful B2C authentication, the redirect lands back on `/pricing` with those params intact, and the CTA immediately calls `POST /api/billing/checkout` — satisfying AC-012 and AC-013 without a server-side session.

**`CheckoutSuccessPage` polling strategy.** `useSubscriptionStatus` (T019) is configured with a 3-second `refetchInterval`. `CheckoutSuccessPage` (T026) starts a 30-second timer on mount; if status is not `Trialing` or `Active` by expiry, it renders the support message and disables further polling — no redirect loop (AC-023). Once a terminal status is reached, `refetchInterval` is set to `false` to stop unnecessary requests.

**`UpgradeModal` and `TrialStatusBanner` are standalone components.** `UpgradeModal` (T030) is imported by both the project-creation gate (T032) and the test-run gate (T033) without duplicating logic. `TrialStatusBanner` (T029) is wired once into `AppLayout` (T031) so it appears on every authenticated portal page automatically — covering AC-007, AC-025, AC-027, AC-028, AC-029, AC-033.

**Tests last.** All test tasks (T035–T041) follow every implementation task, consistent with the `[Test]` layer rule from the QA rules. The E2E suite (T041) is last because it exercises the full stack — live backend endpoints, Stripe mock or sandbox, and all portal UI components — and depends on everything preceding it being complete.

---

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, enums, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Stripe client, options, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services — `Testurio.Api` |
| `[API]` | Minimal API endpoint groups, webhook handler, route registration — `Testurio.Api` |
| `[UI]` | Types, API clients, React Query hooks, MSW handlers, components, pages, i18n keys, route registration — `Testurio.Web` |
| `[Test]` | Unit, integration, frontend component, and E2E test files — `tests/` and `source/Testurio.Web/` |
