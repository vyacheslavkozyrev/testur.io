# Local Testing Guide — Plan Purchase (0015)

## 1. Get Stripe test keys

From the Stripe Dashboard (test mode) → **Developers → API keys**:

- Copy **Secret key** (`sk_test_...`)
- Paste into `source/Testurio.Api/appsettings.Development.json` under `Stripe.SecretKey`

## 2. Create Stripe test prices

In the Stripe Dashboard (test mode) → **Products** → create a product with 8 prices (4 plans × 2 intervals). Paste the `price_...` IDs into `Stripe.PriceIds` in `appsettings.Development.json`:

```json
"PriceIds": {
  "TestJunior_Monthly": "price_...",
  "TestJunior_Annual":  "price_...",
  "TestPro_Monthly":    "price_...",
  "TestPro_Annual":     "price_...",
  "Team_Monthly":       "price_...",
  "Team_Annual":        "price_...",
  "Centurio_Monthly":   "price_...",
  "Centurio_Annual":    "price_..."
}
```

## 3. Forward Stripe webhooks locally

Install the [Stripe CLI](https://stripe.com/docs/stripe-cli), then run:

```bash
stripe listen --forward-to https://localhost:5001/webhooks/stripe
```

Copy the printed `whsec_...` value into `Stripe.WebhookSecret` in `appsettings.Development.json`.

## 4. Test the full checkout flow

1. Start the API: `dotnet run` in `source/Testurio.Api`
2. Start the frontend: `npm run dev` in `source/Testurio.Web`
3. Navigate to `/pricing` → click **Start free trial** on any plan
4. `/billing` calls `POST /v1/billing/checkout` and redirects to Stripe Checkout
5. Use test card `4242 4242 4242 4242`, any future expiry, any CVC
6. Stripe redirects to `/billing/success` → polls subscription status every 3 s → shows confirmation when status becomes `Trialing`

## 5. Trigger a webhook manually

```bash
stripe trigger checkout.session.completed
```

The API logs should show `BillingService.HandleStripeWebhookAsync` firing and upserting a `UserSubscription` document in Cosmos DB.

## 6. Verify the trial banner

After checkout, sign in to the portal — `TrialStatusBanner` should appear on every page showing days remaining. To test the amber (≤3 days) state, set `trialEndsAt` to 2 days from now directly in the Cosmos DB document via the Azure Portal or Cosmos DB Explorer.
