# Progress — Subscription Management (0016)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-18 |       |
| Plan      | ✅ Complete | 2026-05-18 |       |
| Implement | ✅ Complete | 2026-05-19 |       |
| Review    | ✅ Complete | 2026-05-19 |       |
| Test      | ✅ Complete | 2026-05-19 |       |

---

## Implementation Notes

35 tasks implemented across Domain, Infrastructure, Application, API, and Frontend layers. Key additions: `CancelledPendingExpiry`/`PaymentFailed` enum values, `UserSubscription` entity extended with payment fields, `IStripeService` and `IUserSubscriptionRepository` interfaces extended, `StripeService` implementations for portal and reactivation, cross-partition Cosmos queries, `BillingService` extended with portal session, reactivation, and two new webhook handlers (`customer.subscription.deleted`, `invoice.payment_failed`), `customer.subscription.updated` handler extended for cancel-at-period-end logic, 4 new frontend components (`SubscriptionDetailsSection`, `CancellationPendingBanner`, `ReactivateConfirmDialog`, `PaymentFailedBanner`) integrated into layout and account settings, unit/integration/component tests. E2E deferred to Test phase.

---

## Review

**Date:** 2026-05-19

**Issues found and fixed (7):**

1. **BLOCKER — `IStripeService` abstraction bypassed for invoice fetching.** `BillingService` instantiated `InvoiceService` directly (field `new InvoiceService()`), bypassing `IStripeService`. Added `ListInvoicesAsync(string, int, CancellationToken)` to `IStripeService` and `StripeInvoice` record to `Testurio.Core.Interfaces`; implemented in `StripeService`; updated `BillingService.FetchInvoicesAsync` to delegate through the abstraction. Updated unit and integration test default mocks.

2. **BLOCKER — Cross-partition Cosmos queries missing `EnableCrossPartitionQuery = true`.** `UserSubscriptionRepository.CrossPartitionLookupAsync` did not set `EnableCrossPartitionQuery = true` in `QueryRequestOptions`, causing a Cosmos SDK exception at runtime for cross-partition Stripe ID lookups. Fixed.

3. **BLOCKER — Null-forgiving operator on `StripeSubscriptionId` without prior null check.** `ReactivateSubscriptionAsync` used `subscription.StripeSubscriptionId!` before calling Stripe, which would throw `NullReferenceException` for any subscription lacking a Stripe ID. Added an explicit null guard that throws `NotFoundException`.

4. **WARNING — `IBillingService` embedded in `BillingService.cs` instead of its own file.** Project convention (all other services) is to keep the interface in `IServiceName.cs`. Extracted to `IBillingService.cs`.

5. **WARNING — Wrong i18n key used for manage-billing and payment-failed error Snackbars.** `SubscriptionDetailsSection` and `PaymentFailedBanner` used `reactivateDialog.errorMessage` for portal-session errors. Added dedicated keys `details.manageBillingError` and `paymentFailedBanner.errorMessage` to `subscriptionManagement.json` and updated both components.

6. **WARNING — Rules-of-hooks violation in `getStyles`.** `getStyles` was a module-level function that internally called `useMemo`, an unconditional hook call outside a component. Refactored to a pure style factory function; wrapped with `useMemo` inside the component body.

7. **SUGGESTION — Invoice table rows keyed by array index.** Changed `key={idx}` to `key={\`\${invoice.date}-\${idx}\`}` for a more stable key.

**Also fixed:** return URL in `CreatePortalSessionAsync` corrected from `/settings?tab=billing` to `/account/settings?tab=billing` per AC-012.

---

## Test Results

**Date:** 2026-05-19

**Verdict: GO — all tests passing**

| Suite | Count | Result |
| ----- | ----- | ------ |
| .NET unit tests (`Billing\|Subscription` filter) | 17 | ✅ 17 passed |
| .NET integration tests (`Billing\|Subscription` filter) | 15 | ✅ 15 passed |
| Frontend component tests (Subscription\|CancellationPending\|PaymentFailed) | 22 | ✅ 22 passed |
| **Total** | **54** | **✅ 54 passed, 0 failed** |

**TypeScript:** Production source files for feature 0016 are clean. Existing project-wide TS errors (1443 total) stem from missing `@types/jest` in `tsconfig.json` affecting all test files — a pre-existing configuration gap not introduced by this feature.

**Fixes made during test phase (7 issues):**

1. **`StripeService` — ambiguous `SessionService` reference.** `Stripe.Checkout.SessionService` and `Stripe.BillingPortal.SessionService` clashed in the `Testurio.Infrastructure.Stripe` namespace. Resolved with C# `using` type aliases (`CheckoutSessionService`, `PortalSessionService`, etc.).
2. **`UserSubscriptionRepository` — invalid `EnableCrossPartitionQuery` property.** Property does not exist in Cosmos SDK v3 (cross-partition queries are automatic). Removed.
3. **`StripeServiceTests` — structural bug in test file.** Missing closing brace caused all methods from line 57 onwards to be outside the class. Also removed a duplicate `ListInvoicesAsync` test. File rewritten to correct structure.
4. **`AccountControllerTests` — `DisplayName` used instead of `FirstName`/`LastName`.** Pre-existing mismatch against current `UserDocument` and `AccountProfileDto` shapes. Updated to use `FirstName`/`LastName`.
5. **`ExecutorsIntegrationTests` — missing `IApiTestAuthCredentialProvider` argument.** `HttpExecutor` constructor requires 4 arguments; test was passing 3. Added mock for `IApiTestAuthCredentialProvider`.
6. **`CosmosDbInitializer` / `PromptTemplateSeeder` / `PlanSeeder` — no testability hook.** Extracted `ICosmosDbInitializer`, `IPromptTemplateSeeder`, `IPlanSeeder` interfaces; registered via interface in `DependencyInjection.cs` and resolved via interface in `Program.cs`. Created shared `TestInfrastructure.cs` with no-op stubs; patched all 10 controller test factories to replace these services, preventing integration tests from crashing on startup when Cosmos is unavailable.
7. **`BillingControllerTests` — invalid base64 Cosmos key + enum deserialization.** `"dummykey=="` is not valid base64 (Cosmos SDK validates immediately). Replaced with the standard Cosmos emulator key across all 10 integration test factories. Added `JsonStringEnumConverter` to `ReadFromJsonAsync<SubscriptionStatusResponse>()` calls since the API serialises enums as strings.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
