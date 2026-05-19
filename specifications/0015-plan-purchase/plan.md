# Implementation Plan — Plan Purchase (0015)

## Tasks

### Backend — Domain

- [x] T001 [Domain] Create `SubscriptionPlan` enum (TestJunior / TestPro / Team / Centurio) — `source/Testurio.Core/Enums/SubscriptionPlan.cs`
- [x] T002 [Domain] Create `BillingInterval` enum (Monthly / Annual) — `source/Testurio.Core/Enums/BillingInterval.cs`
- [x] T003 [Domain] Create `SubscriptionStatus` enum (None / Trialing / Active / Expired) — `source/Testurio.Core/Enums/SubscriptionStatus.cs`
- [x] T004 [Domain] Create `UserSubscription` entity (userId, plan, billingInterval, status, trialEndsAt, stripeCustomerId, stripeSubscriptionId) — `source/Testurio.Core/Entities/UserSubscription.cs`
- [x] T005 [Domain] Add `IUserSubscriptionRepository` interface (GetByUserIdAsync, UpsertAsync) — `source/Testurio.Core/Repositories/IUserSubscriptionRepository.cs`
- [x] T006 [Domain] Add `IStripeService` interface (CreateCheckoutSessionAsync, GetSubscriptionAsync) — `source/Testurio.Core/Interfaces/IStripeService.cs`

### Backend — Infrastructure

- [x] T007 [Infra] Add `StripeOptions` configuration class (SecretKey, WebhookSecret, Price ID map keyed by plan+interval) — `source/Testurio.Infrastructure/Stripe/StripeOptions.cs`
- [x] T008 [Infra] Implement `StripeService` (Stripe.net SDK; CreateCheckoutSession with trial_period_days=14, customer_email, correct success_url and cancel_url; GetSubscription reads from Stripe API) — `source/Testurio.Infrastructure/Stripe/StripeService.cs`
- [x] T009 [Infra] Implement `UserSubscriptionRepository` (Cosmos DB, partitioned by userId) — `source/Testurio.Infrastructure/Cosmos/UserSubscriptionRepository.cs`
- [x] T010 [Infra] Register `StripeOptions`, `StripeService`, and `UserSubscriptionRepository` in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`

### Backend — Application

- [x] T011 [App] Create `CreateCheckoutSessionRequest` DTO (plan, billingInterval) — `source/Testurio.Api/DTOs/Billing/CreateCheckoutSessionRequest.cs`
- [x] T012 [App] Create `CheckoutSessionResponse` DTO (checkoutUrl) — `source/Testurio.Api/DTOs/Billing/CheckoutSessionResponse.cs`
- [x] T013 [App] Create `SubscriptionStatusResponse` DTO (status, plan, billingInterval, trialEndsAt) — `source/Testurio.Api/DTOs/Billing/SubscriptionStatusResponse.cs`
- [x] T014 [App] Implement `BillingService` (CreateCheckoutSessionAsync delegates to IStripeService; GetSubscriptionStatusAsync reads from IUserSubscriptionRepository; HandleStripeWebhookAsync upserts UserSubscription on checkout.session.completed and customer.subscription.updated) — `source/Testurio.Api/Services/BillingService.cs`

### Backend — API

- [x] T015 [API] Add `BillingEndpoints` with three routes — `source/Testurio.Api/Endpoints/BillingEndpoints.cs`
  - `POST /v1/billing/checkout` — authenticated (B2C JWT); calls BillingService.CreateCheckoutSessionAsync; returns `CheckoutSessionResponse`
  - `GET /v1/billing/subscription` — authenticated; calls BillingService.GetSubscriptionStatusAsync; returns `SubscriptionStatusResponse`
  - `POST /webhooks/stripe` — unauthenticated; validates Stripe-Signature header using WebhookSecret; dispatches to BillingService.HandleStripeWebhookAsync; returns 400 on invalid signature
- [x] T016 [API] Register `BillingEndpoints` and `IBillingService` → `BillingService` in `Program.cs` — `source/Testurio.Api/Program.cs`

### Frontend — Billing Types and API Layer

- [x] T017 [UI] Extend `plan.types.ts` with `SubscriptionStatus` enum, `CreateCheckoutSessionRequest`, `CheckoutSessionResponse`, and `SubscriptionStatusResponse` types (note: `BillingInterval` and `PlanDefinition` already exist in this file) — `source/Testurio.Web/src/types/plan.types.ts`
- [x] T018 [UI] Add `billingService` (createCheckoutSession, getSubscriptionStatus) — `source/Testurio.Web/src/services/billing/billingService.ts`
- [x] T019 [UI] Add React Query hooks `useSubscriptionStatus` (polls GET /v1/billing/subscription; stops on terminal status) and `useCreateCheckoutSession` (useMutation; on success redirects to checkoutUrl) — `source/Testurio.Web/src/hooks/useBilling.ts`
- [x] T020 [UI] Add MSW mock handlers for billing endpoints (POST /v1/billing/checkout, GET /v1/billing/subscription) — `source/Testurio.Web/src/mocks/handlers/billing.ts`

### Frontend — Billing Entry and Checkout Success Pages

- [x] T021 [UI] Create `BillingPage` view (reads plan+interval from query params; redirects to /pricing if absent; calls useCreateCheckoutSession on mount; shows loading state while redirecting to Stripe Checkout URL) — `source/Testurio.Web/src/views/BillingPage/BillingPage.tsx`
- [x] T022 [UI] Register `/billing` authenticated route — `source/Testurio.Web/src/app/(authenticated)/billing/page.tsx`
- [x] T023 [UI] Create `CheckoutSuccessPage` view (reads session_id query param; redirects to /pricing if absent; polls useSubscriptionStatus every 3 s; shows loading state → confirmation + "Create your first project" CTA on Trialing status → support message after 30 s timeout with polling stopped) — `source/Testurio.Web/src/views/CheckoutSuccessPage/CheckoutSuccessPage.tsx`
- [x] T024 [UI] Add checkout success translation keys — `source/Testurio.Web/src/locales/en/checkoutSuccess.json`
- [x] T025 [UI] Register `/billing/success` authenticated route — `source/Testurio.Web/src/app/(authenticated)/billing/success/page.tsx`

### Frontend — Portal Banners and Upgrade Gate

- [x] T026 [UI] Create `TrialStatusBanner` component (shows "X days remaining" banner; amber MUI Alert style when ≤3 days remain; "Trial expired" variant; "Upgrade now" CTA → /pricing; hidden when status is Active or None) — `source/Testurio.Web/src/components/TrialStatusBanner/TrialStatusBanner.tsx`
- [ ] T027 [UI] Create `UpgradeModal` component (shown when a gated action is attempted with status None or Expired; links to /pricing?interval=monthly; dismissible) — `source/Testurio.Web/src/components/UpgradeModal/UpgradeModal.tsx`
- [ ] T028 [UI] Integrate `TrialStatusBanner` into `PrivateCabinetLayout` so it renders on every authenticated portal page — `source/Testurio.Web/src/components/PrivateCabinetLayout/PrivateCabinetLayout.tsx`
- [ ] T029 [UI] Gate "Create project" action with `UpgradeModal` when subscription status is None or Expired — `source/Testurio.Web/src/views/ProjectCreatePage/ProjectCreatePage.tsx`
- [ ] T030 [UI] Gate test-run trigger with `UpgradeModal` when subscription status is None or Expired — `source/Testurio.Web/src/components/ProjectCard/ProjectCard.tsx`
- [ ] T031 [UI] Add portal billing translation keys (trial banner, upgrade modal strings) — `source/Testurio.Web/src/locales/en/billing.json`

### Tests

- [ ] T032 [Test] Unit tests for `BillingService` (CreateCheckoutSession maps each plan+interval to the correct Price ID; GetSubscriptionStatus returns correct DTO; HandleStripeWebhookAsync upserts subscription on checkout.session.completed; updates status on customer.subscription.updated) — `tests/Testurio.UnitTests/Services/BillingServiceTests.cs`
- [ ] T033 [Test] Unit tests for `StripeService` (CheckoutSession created with trial_period_days=14, customer_email, correct cancel_url and success_url) — `tests/Testurio.UnitTests/Infrastructure/StripeServiceTests.cs`
- [ ] T034 [Test] Integration tests for billing endpoints (POST /v1/billing/checkout returns checkoutUrl; GET /v1/billing/subscription returns correct status; POST /webhooks/stripe with invalid Stripe-Signature returns 400; valid webhook upserts UserSubscription) — `tests/Testurio.IntegrationTests/Controllers/BillingControllerTests.cs`
- [ ] T035 [Test] Component tests for `CheckoutSuccessPage` (redirects to /pricing when session_id absent; shows loading initially; shows confirmation when status becomes Trialing; shows support message after 30 s) — `source/Testurio.Web/src/views/CheckoutSuccessPage/CheckoutSuccessPage.test.tsx`
- [ ] T036 [Test] Component tests for `TrialStatusBanner` (correct day count; amber style at ≤3 days; expired variant when Expired; hidden when Active; hidden when None) — `source/Testurio.Web/src/components/TrialStatusBanner/TrialStatusBanner.test.tsx`
- [ ] T037 [Test] E2E tests — `source/Testurio.Web/e2e/plan-purchase.spec.ts`
  - Unauthenticated visitor clicks CTA, is redirected to sign-in, forwarded to /billing with plan+interval preserved after auth
  - Authenticated user selects a plan on /pricing and is taken to Stripe Checkout via /billing
  - /billing/success polls and shows confirmation when subscription status becomes Trialing
  - /billing/success shows support message after 30 s without status resolution
  - Trial banner appears on dashboard; turns amber at ≤3 days; hidden when Active
  - Attempting to create a project with no active plan shows UpgradeModal
  - Abandoned Stripe Checkout returns user to /pricing with account state unchanged

---

## Rationale

**Enums before entity.** `SubscriptionPlan`, `BillingInterval`, and `SubscriptionStatus` (T001–T003) define the vocabulary used by `UserSubscription` (T004), which in turn is referenced by `IUserSubscriptionRepository` (T005) and `IStripeService` (T006).

**`IStripeService` in Core, implementation in Infrastructure.** The interface (T006) belongs in `Testurio.Core` so `BillingService` in `Testurio.Api` depends on an abstraction rather than the Stripe SDK. The concrete `StripeService` (T008) lives in `Testurio.Infrastructure`, isolating the third-party SDK reference — consistent with the existing ADO/Jira client pattern.

**`StripeOptions` before `StripeService`.** The options class (T007) defines the Price ID map and secret fields that `StripeService` reads at startup. Declaring it first enables `ValidateDataAnnotations().ValidateOnStart()` to catch misconfiguration before the first request.

**`BillingService` owns all Stripe event handling.** Rather than splitting webhook logic into a separate class, `BillingService` (T014) handles `checkout.session.completed` and `customer.subscription.updated` — both are simple upserts on `UserSubscription`, keeping all subscription state transitions in one place.

**Stripe webhook endpoint is unauthenticated by design.** Stripe does not present a B2C JWT; it authenticates via the `Stripe-Signature` HMAC header. The route sits under `/webhooks/stripe` alongside the existing `/webhooks/ado` and `/webhooks/jira`, verified against `StripeOptions.WebhookSecret`.

**`BillingPage` view as a redirect gateway.** The `PricingPage` CTA already links authenticated users to `/billing?plan=...&interval=...`. `BillingPage` (T021) reads those params, calls `POST /v1/billing/checkout`, and immediately redirects to the Stripe URL. This avoids rebuilding plan-selection UI and keeps the checkout initiation on an authenticated route, without a server-side session for the plan+interval state.

**`plan.types.ts` extended rather than replaced.** `BillingInterval` and `PlanDefinition` already exist in `plan.types.ts` and are used by `PricingPage`, `PlanCard`, and `usePlans`. Adding the new subscription types to the same file (T017) avoids a redundant file and a re-export layer.

**`CheckoutSuccessPage` polling strategy.** `useSubscriptionStatus` (T019) is configured with a 3-second `refetchInterval`. `CheckoutSuccessPage` (T023) starts a 30-second timer on mount; if status has not reached `Trialing` or `Active` by expiry, it renders the support message and sets `refetchInterval: false` — no redirect loop.

**`TrialStatusBanner` wired once in `PrivateCabinetLayout`.** Placing the banner in the shared layout (T028) means it appears on every authenticated portal page automatically, covering dashboard, projects, settings, and history without per-page additions.

**Tests last.** All test tasks (T032–T037) follow every implementation task, consistent with the `[Test]` layer rule. The E2E suite (T037) is last because it exercises the full stack.

---

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, enums, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Stripe client, options, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services — `Testurio.Api` |
| `[API]` | Minimal API endpoint groups, webhook handler, route registration — `Testurio.Api` |
| `[UI]` | Types, API clients, React Query hooks, MSW handlers, components, views, i18n keys, route registration — `Testurio.Web` |
| `[Test]` | Unit, integration, frontend component, and E2E test files — `tests/` and `source/Testurio.Web/` |
