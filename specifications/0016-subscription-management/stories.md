# User Stories — Subscription Management (0016)

## Out of Scope

The following are explicitly **not** part of this feature:

- Downgrading to a lower plan — upgrade only is supported in v1
- Team or multi-user billing accounts
- Invoice line-item breakdown or tax detail views
- Prorated credit calculations displayed to the user (Stripe handles proration silently)
- Physical per-tenant infrastructure changes on plan change

---

## Stories

### US-001: View active subscription details

**As an** authenticated user with an active or trialing subscription
**I want to** see my current subscription details on the account settings page
**So that** I always know what plan I am on, when I will be billed next, and how to manage my billing

#### Acceptance Criteria

- [ ] AC-001: The subscription section in Account Settings displays: plan name, billing interval (Monthly / Annual), current period end date, next billing date, payment method last 4 digits, payment method expiry month and year, and a list of past invoices
- [ ] AC-002: Invoice history shows invoice date, amount, currency, status (paid / open / void), and a link to the PDF receipt hosted on Stripe
- [ ] AC-003: All dates are formatted in the user's locale using the browser's `Intl.DateTimeFormat`
- [ ] AC-004: Subscription details are fetched via `GET /v1/billing/subscription` and rendered by React Query (`useSubscriptionStatus`); no raw fetch inside the component
- [ ] AC-005: If no active subscription exists (status is `None`), the section displays a CTA linking to `/pricing` instead of subscription details
- [ ] AC-006: If subscription status is `Expired`, the section displays a "Your plan has expired" message and a CTA linking to `/pricing`

---

### US-002: Open Stripe Customer Portal

**As an** authenticated user
**I want to** access the Stripe Customer Portal from Account Settings
**So that** I can update my payment method, view billing history, upgrade my plan, or cancel my subscription — all in one place managed by Stripe

#### Acceptance Criteria

- [ ] AC-007: The Account Settings subscription section includes an "Manage billing" button that is visible to users with status `Active`, `Trialing`, or `CancelledPendingExpiry`
- [ ] AC-008: Clicking "Manage billing" calls `POST /v1/billing/portal-session` and redirects the browser to the returned `portalUrl` in the same tab
- [ ] AC-009: While the portal session is being created, the "Manage billing" button shows a loading spinner and is disabled to prevent duplicate requests
- [ ] AC-010: If `POST /v1/billing/portal-session` returns an error, an MUI Snackbar error toast is shown and the button returns to its enabled state
- [ ] AC-011: `POST /v1/billing/portal-session` is authenticated (requires a valid B2C JWT); unauthenticated requests receive 401
- [ ] AC-012: The backend creates the portal session by calling `stripe.billingPortal.sessions.create` with the user's `stripeCustomerId` and a `return_url` pointing back to `/account/settings?tab=billing`

---

### US-003: Upgrade subscription

**As an** authenticated user on a lower plan
**I want to** upgrade my subscription to a higher plan directly from the Stripe Customer Portal
**So that** my account is immediately upgraded and I am charged the prorated amount for the remaining billing period

#### Acceptance Criteria

- [ ] AC-013: Plan upgrade is performed entirely within the Stripe Customer Portal (no custom upgrade UI required)
- [ ] AC-014: When the user completes an upgrade in the Stripe Customer Portal and returns to the app, `GET /v1/billing/subscription` reflects the new plan within 5 seconds (webhook updates the record; frontend refetches on page focus)
- [ ] AC-015: The prorated amount for the upgrade is charged immediately by Stripe using its default proration behaviour — no custom proration logic in the Testurio backend
- [ ] AC-016: The backend handles the `customer.subscription.updated` webhook event by reading the new `plan` from subscription metadata and updating `UserSubscription.Plan` and `UserSubscription.BillingInterval` in Cosmos DB; this event handler already exists in `BillingService` from feature 0015 and must be verified to cover the plan-change case

---

### US-004: Cancel subscription with confirmation dialog

**As an** authenticated user with an active subscription
**I want to** cancel my subscription from the Stripe Customer Portal, preceded by a confirmation step in the Testurio UI
**So that** I do not accidentally lose access and I understand that my access continues until the current period ends

#### Acceptance Criteria

- [ ] AC-017: Cancellation is initiated via the Stripe Customer Portal; the Testurio UI does not show a "Cancel" button — it only shows the "Manage billing" portal button (US-002)
- [ ] AC-018: When the user initiates cancellation inside the Stripe Customer Portal, Stripe sets `cancel_at_period_end = true` on the subscription; the backend's `customer.subscription.updated` handler detects this and sets `UserSubscription.Status = CancelledPendingExpiry` and `UserSubscription.CancelledAt = now`
- [ ] AC-019: After cancellation is confirmed in the Stripe Portal and the user returns to the app, `GET /v1/billing/subscription` returns `status: CancelledPendingExpiry` and `currentPeriodEnd` within 5 seconds
- [ ] AC-020: The frontend refetches subscription status on window focus so the cancellation state is reflected without a manual page reload

---

### US-005: Cancellation pending banner

**As an** authenticated user who has cancelled their subscription but whose access has not yet expired
**I want to** see a persistent banner informing me that my cancellation is scheduled
**So that** I am always aware that my access ends on a specific date and can choose to reactivate if I change my mind

#### Acceptance Criteria

- [ ] AC-021: When subscription status is `CancelledPendingExpiry`, a "Cancellation pending" MUI Alert banner is rendered on every authenticated portal page (wired into `PrivateCabinetLayout` alongside the existing `TrialStatusBanner`)
- [ ] AC-022: The banner displays the exact date on which access will end, derived from `currentPeriodEnd` in the subscription status response
- [ ] AC-023: The banner includes a "Reactivate" CTA button; clicking it opens the reactivation confirmation dialog (US-006)
- [ ] AC-024: The banner is not rendered when subscription status is `Active`, `Trialing`, `None`, or `Expired`
- [ ] AC-025: Full product access is retained while status is `CancelledPendingExpiry` — no feature gates are applied

---

### US-006: Reactivate a cancelled subscription

**As an** authenticated user whose subscription is cancelled but not yet expired
**I want to** reactivate my subscription with a single confirmation
**So that** I can continue using Testurio without losing my billing details or starting a new checkout

#### Acceptance Criteria

- [ ] AC-026: The "Reactivate" CTA in the cancellation pending banner (AC-023) opens a confirmation dialog asking the user to confirm reactivation before any API call is made
- [ ] AC-027: The confirmation dialog states the plan name, next billing date, and the amount that will be charged on that date
- [ ] AC-028: Confirming reactivation calls `POST /v1/billing/reactivate`; the backend calls the Stripe API to set `cancel_at_period_end = false` on the subscription, then updates `UserSubscription.Status = Active` and clears `UserSubscription.CancelledAt` in Cosmos DB
- [ ] AC-029: While the reactivation request is in flight, the confirmation button shows a loading spinner and is disabled
- [ ] AC-030: On successful reactivation, the dialog closes, the cancellation pending banner disappears, and the subscription section reflects `Active` status — achieved by invalidating the `useSubscriptionStatus` query cache on mutation success
- [ ] AC-031: If `POST /v1/billing/reactivate` returns an error, an MUI Snackbar error toast is shown and the dialog remains open
- [ ] AC-032: `POST /v1/billing/reactivate` is authenticated (requires a valid B2C JWT); unauthenticated requests receive 401
- [ ] AC-033: If subscription status is not `CancelledPendingExpiry` when the endpoint is called, the backend returns 409 Conflict with a descriptive problem details body

---

### US-007: Failed payment notification

**As an** authenticated user whose subscription payment has failed
**I want to** see a clear warning about the payment failure
**So that** I know to update my payment method before losing access

#### Acceptance Criteria

- [ ] AC-034: When the backend receives `invoice.payment_failed` from Stripe, it sets `UserSubscription.Status = PaymentFailed` in Cosmos DB
- [ ] AC-035: When subscription status is `PaymentFailed`, a "Payment failed" MUI Alert error banner is rendered on every authenticated portal page (wired into `PrivateCabinetLayout`)
- [ ] AC-036: The banner includes an "Update payment method" CTA that triggers the Stripe Customer Portal flow (same as "Manage billing" in US-002)
- [ ] AC-037: The banner is not rendered when subscription status is anything other than `PaymentFailed`
- [ ] AC-038: `GET /v1/billing/subscription` returns `status: PaymentFailed` so the frontend can display the correct UI state

---

### US-008: Webhook handling — subscription deleted

**As the** Testurio system
**I want to** receive and process the `customer.subscription.deleted` Stripe webhook event
**So that** a user's subscription record accurately reflects `Expired` status when Stripe has fully terminated the subscription

#### Acceptance Criteria

- [ ] AC-039: The `POST /webhooks/stripe` endpoint handles the `customer.subscription.deleted` event by setting `UserSubscription.Status = Expired` and persisting the updated record to Cosmos DB
- [ ] AC-040: The handler looks up the `UserSubscription` record by `stripeSubscriptionId`; if no matching record is found, it returns 200 and logs a warning (idempotent, no error thrown)
- [ ] AC-041: Duplicate `customer.subscription.deleted` deliveries (same event ID) are handled idempotently — processing the same event twice produces the same final state with no error
- [ ] AC-042: The webhook endpoint returns 200 for all recognised Stripe events (including `customer.subscription.deleted`) even when the business-logic update is a no-op; it returns 400 only for invalid signatures

---

### US-009: Webhook handling — payment failed

**As the** Testurio system
**I want to** receive and process the `invoice.payment_failed` Stripe webhook event
**So that** a user's subscription is promptly marked as `PaymentFailed` and they see the warning banner on next login

#### Acceptance Criteria

- [ ] AC-043: The `POST /webhooks/stripe` endpoint handles the `invoice.payment_failed` event by reading the `customer` field to locate the `UserSubscription` record and setting `Status = PaymentFailed`
- [ ] AC-044: If the invoice is not for a subscription (i.e., `subscription` field is null), the handler logs and returns 200 without updating any record
- [ ] AC-045: The update is persisted to Cosmos DB via `IUserSubscriptionRepository.UpsertAsync` — the same pattern used by other webhook handlers
- [ ] AC-046: Duplicate `invoice.payment_failed` deliveries are handled idempotently
