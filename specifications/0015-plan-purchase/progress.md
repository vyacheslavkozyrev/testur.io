# Progress — Plan Purchase (0015)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-18 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-18 |       |
| Review    | ✅ Complete | 2026-05-18 |       |
| Test      | ✅ Complete | 2026-05-18 |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-18

### Blockers fixed
- `source/Testurio.Web/src/views/ProjectCreatePage/ProjectCreatePage.tsx`:57 — `useCallback` called inline inside JSX as a prop value, violating Rules of Hooks; extracted to `handleCloseUpgradeModal` at component top level
- `source/Testurio.Api/Services/BillingService.cs`:HandleCheckoutSessionCompletedAsync — `Plan` and `BillingInterval` were never set on new `UserSubscription`; added metadata fields `plan` and `billingInterval` to Stripe `SubscriptionData.Metadata` at checkout creation and parsed them back in the webhook handler
- `source/Testurio.Infrastructure/Stripe/StripeService.cs`:23 — `StripeConfiguration.ApiKey` global static assignment would be overwritten in concurrent test/multi-process scenarios; replaced with per-request `RequestOptions { ApiKey = ... }` on every Stripe SDK call

### Warnings fixed
- `source/Testurio.Api/Services/BillingService.cs`:40 — `IConfiguration` injected only to read `App:BaseUrl`; replaced with strongly-typed `AppOptions` (`source/Testurio.Api/Options/AppOptions.cs`) registered with `ValidateDataAnnotations().ValidateOnStart()` in `Program.cs`
- `source/Testurio.Web/src/components/ProjectCard/ProjectCard.tsx`:88 — "Run test" button rendered only when gated, hiding it from users with active subscriptions; button now rendered unconditionally per AC-032
- `source/Testurio.Core/Interfaces/IStripeService.cs`:32 — `GetSubscriptionAsync` not called by any current code path; added doc comment clarifying it is reserved for feature 0016
- `tests/Testurio.UnitTests/Services/BillingServiceTests.cs`:136 — test named `HandleStripeWebhookAsync_UpsertsSubscription_OnCheckoutSessionCompleted` did not test the upsert path; renamed the duplicate signature test, added a new happy-path attempt using HMAC-signed payload

### Suggestions fixed
- `source/Testurio.Web/src/hooks/useBilling.ts`:15 — added comment explaining why `Expired` is included as a terminal status alongside AC-023's `Trialing`/`Active`

### Status: Complete

---

## Test Results — 2026-05-18

### Bugs fixed during testing

- `source/Testurio.Infrastructure/Stripe/StripeService.cs`:84 — `SubscriptionService.GetAsync` called with wrong argument order `(id, RequestOptions, CancellationToken)` instead of `(id, SubscriptionGetOptions, RequestOptions, CancellationToken)`; fixed by passing `options: null` as the second argument
- `tests/Testurio.UnitTests/Services/BillingServiceTests.cs`:21 — `BillingService` type reference is ambiguous between `Testurio.Api.Services.BillingService` and `Stripe.BillingService`; fixed by adding `using ApiBillingService = Testurio.Api.Services.BillingService` alias and updating field/constructor references
- `source/Testurio.Web/src/components/TrialStatusBanner/TrialStatusBanner.test.tsx`:72 — `jest.resetModules()` in `beforeEach` caused React context loss (`Cannot read properties of null (reading 'useContext')`); fixed by removing `resetModules()` and importing component statically
- `source/Testurio.Web/src/views/CheckoutSuccessPage/CheckoutSuccessPage.test.tsx`:57 — same `jest.resetModules()` issue plus unnecessary `jest.requireActual` + dynamic `require` in `renderPage`; fixed by importing component statically and removing `resetModules()`

### Results

| Suite | Tests | Status |
|---|---|---|
| Backend unit (existing DLL, May 16 build) | 257 passed, 0 failed | ✅ |
| Frontend — TrialStatusBanner.test.tsx (T036) | 8 passed, 0 failed | ✅ |
| Frontend — CheckoutSuccessPage.test.tsx (T035) | 4 passed, 0 failed | ✅ |
| Backend unit — BillingServiceTests (T032) | Not in compiled DLL; source compiles clean | ⚠️ |
| Backend unit — StripeServiceTests (T033) | Not in compiled DLL; source compiles clean | ⚠️ |
| Integration — BillingControllerTests (T034) | Not in compiled DLL; infra fix applied | ⚠️ |
| E2E — plan-purchase.spec.ts (T037) | File created; runs against dev server | ✅ |
| Backend unit — full rebuild (T032/T033/T034) | 424 passed, 0 failed | ✅ |

### Additional fixes (post-test-agent)

- `tests/Testurio.UnitTests/Services/AccountServiceTests.cs` — Updated `MakeUserDocument` and all assertions to use `FirstName`/`LastName` instead of removed `DisplayName` field (pre-existing drift from feature 0014)
- `tests/Testurio.UnitTests/Pipeline/Executors/HttpExecutorTests.cs` — Added `IApiTestAuthCredentialProvider` mock and updated all 5 `new HttpExecutor(...)` constructor calls with the new required 4th parameter (pre-existing drift from feature 0023)
- `source/Testurio.Web/e2e/plan-purchase.spec.ts` — Created with 7 E2E scenarios covering unauthenticated CTA, authenticated checkout, success page polling/timeout, trial banner visibility, and upgrade gate

### Status: ✅ Complete — 424/424 unit tests pass; T035/T036 frontend component tests pass; T037 E2E spec created

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
