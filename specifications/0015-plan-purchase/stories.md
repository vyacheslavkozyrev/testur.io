# User Stories — Plan Purchase (0015)

## Out of Scope

The following are explicitly **not** part of this feature:

- Subscription management after purchase (upgrade, downgrade, cancellation) — covered by feature 0016
- Payment method updates after the initial purchase — covered by feature 0016
- Invoice history and PDF receipts
- Team or multi-user billing accounts
- Proration calculations when switching plans
- Reactivation flow for cancelled subscriptions

---

## Stories

### US-001: View and compare subscription plans

**As a** visitor
**I want to** see all available subscription plans with their prices and features side by side
**So that** I can choose the plan that best fits my needs before committing to a purchase

#### Acceptance Criteria

- [ ] AC-001: The pricing page at `/pricing` displays four plans in order: Test Junior, Test Pro, Team, Centurio
- [ ] AC-002: Each plan card shows the plan name, a feature highlight list, and the price for the currently selected billing interval
- [ ] AC-003: When annual billing is selected, each plan card displays an annual-discount badge showing the percentage saved versus monthly
- [ ] AC-004: The billing interval toggle (Monthly / Annual) updates all plan card prices simultaneously without a page reload
- [ ] AC-005: Each plan card includes a "Start free trial" CTA button
- [ ] AC-006: The pricing page is publicly accessible without authentication

---

### US-002: Trial status visible across the portal

**As an** authenticated user on a free trial
**I want to** always know how much trial time I have remaining
**So that** I can plan when to purchase a plan and avoid unexpected loss of access

#### Acceptance Criteria

- [ ] AC-007: The `TrialStatusBanner` is rendered on every authenticated portal page via `AppLayout` — not individually per page
- [ ] AC-008: The banner displays the number of days remaining in the trial, derived from `trialEndsAt` returned by `GET /api/billing/subscription`
- [ ] AC-009: The banner includes an "Upgrade now" CTA that links to `/pricing`
- [ ] AC-010: The banner is not rendered when subscription status is `Active`
- [ ] AC-011: The banner is not rendered when subscription status is `None` (user has never started a trial)

---

### US-003: Start a free trial as an unauthenticated visitor

**As a** visitor who is not signed in
**I want to** start a free trial by selecting a plan
**So that** I am guided through sign-up and taken directly to checkout with my selection preserved

#### Acceptance Criteria

- [ ] AC-012: Clicking "Start free trial" on `/pricing` while unauthenticated redirects to the Azure AD B2C sign-in page with the selected plan and billing interval encoded as query parameters in the return URL
- [ ] AC-013: After successful authentication, the return URL restores `/pricing` with the plan and interval query parameters intact, and the CTA immediately initiates Stripe Checkout without requiring the user to click again
- [ ] AC-014: If the user closes or cancels the authentication flow, they remain on the B2C sign-in page with no subscription or checkout session created
- [ ] AC-015: Abandoning Stripe Checkout (clicking "Back" or closing the tab) returns the user to `/pricing` with the account state unchanged

---

### US-004: Start a free trial as an authenticated user

**As an** authenticated user with no active subscription
**I want to** click "Start free trial" on the pricing page and proceed directly to checkout
**So that** I can activate my account without unnecessary steps

#### Acceptance Criteria

- [ ] AC-016: An authenticated user clicking "Start free trial" on `/pricing` is taken directly to Stripe Checkout without an intermediate authentication step
- [ ] AC-017: The Stripe Checkout session is created via `POST /api/billing/checkout` with the selected plan and billing interval
- [ ] AC-018: The Stripe Checkout session includes a 14-day free trial period (`trial_period_days = 14`)
- [ ] AC-019: The Stripe Checkout session is pre-populated with the user's email address
- [ ] AC-020: The Stripe Checkout session receives the correct Price ID for the selected plan and billing interval combination
- [ ] AC-021: Abandoning Stripe Checkout returns the user to `/pricing` with account state unchanged

---

### US-005: Subscription activated after completing checkout

**As an** authenticated user who has just completed Stripe Checkout
**I want to** see confirmation that my trial has started
**So that** I know my account is active and I can start using the product

#### Acceptance Criteria

- [ ] AC-022: After Stripe Checkout completes, the user is redirected to `/billing/success?session_id=<id>`
- [ ] AC-023: `/billing/success` polls `GET /api/billing/subscription` every 3 seconds; if subscription status does not reach `Trialing` or `Active` within 30 seconds, a support message is shown and polling stops — no redirect loop occurs
- [ ] AC-024: Once subscription status is `Trialing`, the page shows a confirmation message and a "Create your first project" CTA
- [ ] AC-025: Navigating to `/billing/success` without a `session_id` query parameter immediately redirects to `/pricing`

---

### US-006: Trial expiry warning

**As an** authenticated user whose trial is approaching expiry or has expired
**I want to** receive clear visual warnings about my trial status
**So that** I know when I need to purchase a plan to maintain access

#### Acceptance Criteria

- [ ] AC-026: When the trial has more than 3 days remaining, the `TrialStatusBanner` renders in its default (non-amber) style
- [ ] AC-027: When the trial has 3 or fewer days remaining, the `TrialStatusBanner` renders in amber MUI Alert style
- [ ] AC-028: When subscription status is `Expired`, the `TrialStatusBanner` renders the "Trial expired" variant instead of the day-count message
- [ ] AC-029: When subscription status is `Active`, the `TrialStatusBanner` is not rendered
- [ ] AC-030: The day count shown in the banner is calculated from `trialEndsAt` in the subscription status response

---

### US-007: Access gate for subscription-required actions

**As an** authenticated user with no active plan or expired trial
**I want to** be prompted to upgrade when I try to use a premium feature
**So that** I understand why the action is unavailable and how to unlock it

#### Acceptance Criteria

- [ ] AC-031: Attempting to create a project when subscription status is `None` or `Expired` displays the `UpgradeModal` instead of the creation form
- [ ] AC-032: Attempting to trigger a test run when subscription status is `None` or `Expired` displays the `UpgradeModal`
- [ ] AC-033: The `UpgradeModal` contains a link to `/pricing?interval=monthly`
- [ ] AC-034: The `UpgradeModal` can be dismissed without navigating away from the current page
- [ ] AC-035: The `UpgradeModal` is not shown when subscription status is `Active` or `Trialing`
