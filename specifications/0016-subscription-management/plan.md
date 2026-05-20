# Implementation Plan — Subscription Management (0016)

## Tasks

### Backend — Domain

- [x] T001 [Domain] Extend `SubscriptionStatus` enum — add `CancelledPendingExpiry` and `PaymentFailed` values — `source/Testurio.Core/Enums/SubscriptionStatus.cs`
- [x] T002 [Domain] Extend `UserSubscription` entity — add `CancelledAt` (`DateTimeOffset?`), `CurrentPeriodEnd` (`DateTimeOffset`), `PaymentMethodLast4` (`string?`), `PaymentMethodExpMonth` (`int?`), `PaymentMethodExpYear` (`int?`) — `source/Testurio.Core/Entities/UserSubscription.cs`
- [x] T003 [Domain] Extend `IStripeService` interface — add `CreatePortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct)` returning `string` (portal URL); add `ReactivateSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)` — `source/Testurio.Core/Interfaces/IStripeService.cs`
- [x] T004 [Domain] Extend `IUserSubscriptionRepository` interface — add `GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken ct)` and `GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct)` — `source/Testurio.Core/Repositories/IUserSubscriptionRepository.cs`

### Backend — Infrastructure

- [x] T005 [Infra] Implement `CreatePortalSessionAsync` in `StripeService` — calls `stripe.BillingPortal.Sessions.CreateAsync` with `stripeCustomerId` and `returnUrl`; uses per-request `RequestOptions { ApiKey = ... }` (same pattern as existing methods) — `source/Testurio.Infrastructure/Stripe/StripeService.cs`
- [x] T006 [Infra] Implement `ReactivateSubscriptionAsync` in `StripeService` — calls `stripe.Subscriptions.UpdateAsync(id, new SubscriptionUpdateOptions { CancelAtPeriodEnd = false })` with per-request `RequestOptions` — `source/Testurio.Infrastructure/Stripe/StripeService.cs`
- [x] T007 [Infra] Implement `GetByStripeSubscriptionIdAsync` and `GetByStripeCustomerIdAsync` in `UserSubscriptionRepository` — cross-partition queries scoped to the relevant Stripe ID fields; use `QueryDefinition` with `EnableCrossPartitionQuery = false` where possible; fall back to cross-partition only when no `userId` is available — `source/Testurio.Infrastructure/Cosmos/UserSubscriptionRepository.cs`

### Backend — Application

- [x] T008 [App] Create `PortalSessionResponse` DTO (`portalUrl: string`) — `source/Testurio.Api/DTOs/Billing/PortalSessionResponse.cs`
- [x] T009 [App] Extend `SubscriptionStatusResponse` DTO — add `currentPeriodEnd` (`DateTimeOffset`), `cancelledAt` (`DateTimeOffset?`), `paymentMethodLast4` (`string?`), `paymentMethodExpMonth` (`int?`), `paymentMethodExpYear` (`int?`), `invoices` (`InvoiceDto[]`) — `source/Testurio.Api/DTOs/Billing/SubscriptionStatusResponse.cs`
- [x] T010 [App] Create `InvoiceDto` — fields: `date` (`DateTimeOffset`), `amount` (`decimal`), `currency` (`string`), `status` (`string`), `pdfUrl` (`string?`) — `source/Testurio.Api/DTOs/Billing/InvoiceDto.cs`
- [x] T011 [App] Extend `BillingService.GetSubscriptionStatusAsync` — after reading `UserSubscription` from Cosmos, call Stripe to fetch the live subscription object and populate `currentPeriodEnd`, `paymentMethodLast4`, `paymentMethodExpMonth`, `paymentMethodExpYear`; call `stripe.Invoices.ListAsync` (scoped to `stripeCustomerId`, limit 20) and map to `InvoiceDto[]` — `source/Testurio.Api/Services/BillingService.cs`
- [x] T012 [App] Add `CreatePortalSessionAsync` to `BillingService` — retrieves `UserSubscription` by `userId`, delegates to `IStripeService.CreatePortalSessionAsync`, returns portal URL; throws `NotFoundException` if no subscription record exists — `source/Testurio.Api/Services/BillingService.cs`
- [x] T013 [App] Add `ReactivateSubscriptionAsync` to `BillingService` — retrieves `UserSubscription` by `userId`; throws `ConflictException` if status is not `CancelledPendingExpiry`; calls `IStripeService.ReactivateSubscriptionAsync`; sets `Status = Active`, clears `CancelledAt`, persists via `UpsertAsync` — `source/Testurio.Api/Services/BillingService.cs`
- [x] T014 [App] Extend `BillingService.HandleStripeWebhookAsync` — add handlers for two new event types:
  - `customer.subscription.deleted` → locate record by `stripeSubscriptionId`, set `Status = Expired`, upsert; log warning and return if not found
  - `invoice.payment_failed` → locate record by `stripeCustomerId` via `GetByStripeCustomerIdAsync`, set `Status = PaymentFailed`, upsert; skip if `subscription` field is null on the invoice — `source/Testurio.Api/Services/BillingService.cs`
- [x] T015 [App] Extend `customer.subscription.updated` handler in `BillingService` — detect `cancel_at_period_end = true` and set `Status = CancelledPendingExpiry` + `CancelledAt = now`; detect `cancel_at_period_end = false` (reactivation via portal) and set `Status = Active` + clear `CancelledAt`; ensure plan and interval metadata are read and written on every update (verify existing 0015 logic covers plan-change case) — `source/Testurio.Api/Services/BillingService.cs`
- [x] T016 [App] Add `IBillingService` interface extensions — declare `CreatePortalSessionAsync` and `ReactivateSubscriptionAsync` on the interface — `source/Testurio.Api/Services/IBillingService.cs`

### Backend — API

- [x] T017 [API] Add two new routes to `BillingEndpoints`:
  - `POST /v1/billing/portal-session` — authenticated; calls `BillingService.CreatePortalSessionAsync`; returns `TypedResults.Ok(new PortalSessionResponse(portalUrl))`; returns 404 if no subscription exists
  - `POST /v1/billing/reactivate` — authenticated; calls `BillingService.ReactivateSubscriptionAsync`; returns `TypedResults.NoContent()`; returns 409 if status is not `CancelledPendingExpiry` — `source/Testurio.Api/Endpoints/BillingEndpoints.cs`

### Frontend — Types and API Layer

- [x] T018 [UI] Extend `plan.types.ts` — add `CancelledPendingExpiry` and `PaymentFailed` to `SubscriptionStatus` enum; add `InvoiceDto` type; extend `SubscriptionStatusResponse` with `currentPeriodEnd`, `cancelledAt`, `paymentMethodLast4`, `paymentMethodExpMonth`, `paymentMethodExpYear`, `invoices`; add `PortalSessionResponse` type — `source/Testurio.Web/src/types/plan.types.ts`
- [x] T019 [UI] Extend `billingService` — add `createPortalSession(): Promise<PortalSessionResponse>` and `reactivateSubscription(): Promise<void>` — `source/Testurio.Web/src/services/billing/billingService.ts`
- [x] T020 [UI] Extend `useBilling.ts` — add `useCreatePortalSession` mutation hook (on success, redirects browser to `portalUrl`; on error, returns error for caller to display); add `useReactivateSubscription` mutation hook (on success, invalidates `useSubscriptionStatus` query cache); update `useSubscriptionStatus` to refetch on window focus (`refetchOnWindowFocus: true`) — `source/Testurio.Web/src/hooks/useBilling.ts`
- [x] T021 [UI] Extend MSW mock handlers — add `POST /v1/billing/portal-session` returning `{ portalUrl: 'https://billing.stripe.com/mock' }` and `POST /v1/billing/reactivate` returning 204; update `GET /v1/billing/subscription` mock to include new fields — `source/Testurio.Web/src/mocks/handlers/billing.ts`

### Frontend — Subscription Management UI

- [x] T022 [UI] Create `SubscriptionDetailsSection` component — displays plan name, billing interval, current period end, next billing date, payment method last 4 + expiry; shows invoice history table (date, amount, status, PDF link); includes "Manage billing" button that calls `useCreatePortalSession` with loading/disabled state; shows CTA to `/pricing` when status is `None` or `Expired` — `source/Testurio.Web/src/components/SubscriptionDetailsSection/SubscriptionDetailsSection.tsx`
- [x] T023 [UI] Create `CancellationPendingBanner` component — MUI Alert (severity="warning"); displays period end date; includes "Reactivate" CTA button that opens `ReactivateConfirmDialog`; rendered in `PrivateCabinetLayout` when status is `CancelledPendingExpiry` — `source/Testurio.Web/src/components/CancellationPendingBanner/CancellationPendingBanner.tsx`
- [x] T024 [UI] Create `ReactivateConfirmDialog` component — MUI Dialog; displays plan name, next billing date, and charge amount; "Confirm reactivation" button calls `useReactivateSubscription` with loading/disabled state; on success closes dialog; on error shows MUI Snackbar toast; "Cancel" button closes dialog without action — `source/Testurio.Web/src/components/ReactivateConfirmDialog/ReactivateConfirmDialog.tsx`
- [x] T025 [UI] Create `PaymentFailedBanner` component — MUI Alert (severity="error"); includes "Update payment method" CTA that calls `useCreatePortalSession`; rendered in `PrivateCabinetLayout` when status is `PaymentFailed` — `source/Testurio.Web/src/components/PaymentFailedBanner/PaymentFailedBanner.tsx`
- [x] T026 [UI] Integrate `CancellationPendingBanner` and `PaymentFailedBanner` into `PrivateCabinetLayout` — placed alongside existing `TrialStatusBanner`; each conditionally rendered based on `useSubscriptionStatus` result — `source/Testurio.Web/src/components/PrivateCabinetLayout/PrivateCabinetLayout.tsx`
- [x] T027 [UI] Integrate `SubscriptionDetailsSection` into the Account Settings billing tab — replaces any stub/placeholder currently in the billing tab — `source/Testurio.Web/src/views/AccountSettingsPage/AccountSettingsPage.tsx`
- [x] T028 [UI] Add subscription management translation keys — `source/Testurio.Web/src/locales/en/subscriptionManagement.json`

### Tests

- [x] T029 [Test] Unit tests for `BillingService` additions — `CreatePortalSessionAsync` returns portal URL; `ReactivateSubscriptionAsync` sets `Status = Active` and clears `CancelledAt`; `ReactivateSubscriptionAsync` throws `ConflictException` when status is not `CancelledPendingExpiry`; `HandleStripeWebhookAsync` with `customer.subscription.deleted` sets `Status = Expired`; with `invoice.payment_failed` sets `Status = PaymentFailed`; with `customer.subscription.updated` and `cancel_at_period_end = true` sets `Status = CancelledPendingExpiry` — `tests/Testurio.UnitTests/Services/BillingServiceTests.cs`
- [x] T030 [Test] Unit tests for `StripeService` additions — `CreatePortalSessionAsync` calls `BillingPortal.Sessions.CreateAsync` with correct `customerId` and `returnUrl`; `ReactivateSubscriptionAsync` calls `Subscriptions.UpdateAsync` with `CancelAtPeriodEnd = false` — `tests/Testurio.UnitTests/Infrastructure/StripeServiceTests.cs`
- [x] T031 [Test] Integration tests for new billing endpoints — `POST /v1/billing/portal-session` returns `portalUrl`; `POST /v1/billing/portal-session` returns 404 when no subscription exists; `POST /v1/billing/reactivate` returns 204 on success; `POST /v1/billing/reactivate` returns 409 when status is not `CancelledPendingExpiry`; `POST /webhooks/stripe` with `customer.subscription.deleted` sets `Status = Expired`; with `invoice.payment_failed` sets `Status = PaymentFailed` — `tests/Testurio.IntegrationTests/Controllers/BillingControllerTests.cs`
- [x] T032 [Test] Component tests for `SubscriptionDetailsSection` — renders plan name, billing interval, period end, payment method; renders invoice table with PDF links; "Manage billing" button disabled while portal session loads; shows `/pricing` CTA when status is `None` — `source/Testurio.Web/src/components/SubscriptionDetailsSection/SubscriptionDetailsSection.test.tsx`
- [x] T033 [Test] Component tests for `CancellationPendingBanner` — renders when status is `CancelledPendingExpiry`; shows correct period end date; "Reactivate" opens `ReactivateConfirmDialog`; not rendered for other statuses — `source/Testurio.Web/src/components/CancellationPendingBanner/CancellationPendingBanner.test.tsx`
- [x] T034 [Test] Component tests for `PaymentFailedBanner` — renders when status is `PaymentFailed`; "Update payment method" calls portal session mutation; not rendered for other statuses — `source/Testurio.Web/src/components/PaymentFailedBanner/PaymentFailedBanner.test.tsx`
- [x] T035 [Test] E2E tests — `source/Testurio.Web/e2e/subscription-management.spec.ts`
  - Authenticated user on Active plan sees subscription details, payment method, and invoice list on Account Settings billing tab
  - "Manage billing" button triggers redirect to Stripe Customer Portal URL
  - User returning from portal with `CancelledPendingExpiry` status sees cancellation pending banner with correct end date
  - "Reactivate" CTA opens confirmation dialog; confirming calls reactivate endpoint and banner disappears
  - User with `PaymentFailed` status sees payment failed banner with "Update payment method" CTA
  - Webhook `customer.subscription.deleted` results in `Expired` status on next `GET /v1/billing/subscription`
  - Webhook `invoice.payment_failed` results in `PaymentFailed` status on next `GET /v1/billing/subscription`

---

## Rationale

**Enum extensions before entity extensions.** `CancelledPendingExpiry` and `PaymentFailed` (T001) are referenced by `UserSubscription` fields (T002), `BillingService` logic (T013–T015), and the frontend `SubscriptionStatus` enum (T018). They must be defined first to avoid compilation errors across layers.

**New entity fields before repository extensions.** `CancelledAt`, `CurrentPeriodEnd`, and payment method fields (T002) are persisted by the extended repository methods (T007) and read by `GetSubscriptionStatusAsync` (T011). Domain entities are always extended before infrastructure picks them up.

**Interface additions before implementations.** `IStripeService` (T003) and `IUserSubscriptionRepository` (T004) define the contracts; `StripeService` (T005–T006) and `UserSubscriptionRepository` (T007) implement them; `BillingService` (T011–T016) depends on both. Following Core → Infrastructure → App avoids forward references.

**`GetByStripeSubscriptionIdAsync` and `GetByStripeCustomerIdAsync` in repository.** Webhook handlers receive Stripe IDs, not Testurio `userId`. These cross-partition lookups are necessary for the `customer.subscription.deleted` and `invoice.payment_failed` handlers. They are scoped to the minimum field set required and kept out of the hot read path.

**`GetSubscriptionStatusAsync` extended rather than split.** The subscription status endpoint (T011) already exists and is polled by the frontend. Adding live Stripe data (period end, payment method, invoices) enriches the single response object rather than adding new endpoints, keeping the frontend's data model simple.

**`customer.subscription.updated` handler extended in T015.** This handler was created in feature 0015 to track plan changes. It must also cover the `cancel_at_period_end` flag change produced by cancellation and reactivation in the Stripe Portal. Extending the existing handler keeps all subscription state transitions in one method in `BillingService`.

**Stripe Customer Portal for cancellation — no custom cancel endpoint.** Cancellation is handled entirely within the Stripe Portal. The backend only reacts to the resulting `customer.subscription.updated` webhook. This eliminates a redundant API endpoint and avoids re-implementing Stripe's cancellation UX.

**`POST /v1/billing/reactivate` as a distinct endpoint.** Reactivation is a Testurio-side action (user clicks within the app, not the portal), so a dedicated endpoint with explicit 409 guarding is appropriate. The portal's own reactivation path (if used) also triggers `customer.subscription.updated` with `cancel_at_period_end = false`, which T015 handles.

**Banners wired into `PrivateCabinetLayout`.** Following the pattern established by `TrialStatusBanner` in feature 0015, `CancellationPendingBanner` and `PaymentFailedBanner` are integrated at the layout level (T026) so they appear on every authenticated page without per-page additions.

**Tests last.** All test tasks (T029–T035) follow every implementation task, consistent with the `[Test]` layer rule. The E2E suite (T035) is last because it exercises the full stack and depends on all prior layers being in place.

---

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Enums, entities, interfaces — `Testurio.Core` |
| `[Infra]` | Cosmos DB repository, Stripe client — `Testurio.Infrastructure` |
| `[App]` | DTOs, services — `Testurio.Api` |
| `[API]` | Minimal API endpoint groups, route registration — `Testurio.Api` |
| `[UI]` | Types, API clients, React Query hooks, MSW handlers, components, views, i18n keys — `Testurio.Web` |
| `[Test]` | Unit, integration, frontend component, and E2E test files — `tests/` and `source/Testurio.Web/` |
