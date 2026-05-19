# Progress — Plan Purchase (0015)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-18 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-18 |       |
| Review    | ✅ Complete | 2026-05-18 |       |
| Test      | ⏳ Pending  |            |       |

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

## Test Results

_Populated by `/test [####]`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
