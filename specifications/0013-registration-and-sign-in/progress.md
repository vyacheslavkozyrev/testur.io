# Progress — Registration & Sign-In (0013)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-17 |       |
| Plan      | ✅ Complete | 2026-05-17 |       |
| Implement | ✅ Complete | 2026-05-17 |       |
| Review    | ⏳ Pending  |            |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

### 2026-05-17

- All 29 implementation tasks complete (T001–T029). T030 (E2E tests) deferred — requires a live or mocked B2C instance at the Next.js dev server level.
- `@azure/msal-browser` v4.25.1 installed with `--legacy-peer-deps` due to MUI v6 peer dep conflict with React 19.
- `CustomAuthPublicClientApplication` from MSAL v4 used for native email/password flows (sign-in, sign-up, SSPR). Requires `NEXT_PUBLIC_B2C_NATIVE_AUTH_URL` pointing to an Entra External ID (CIAM) tenant.
- `tokenValidator.ts` performs JWT payload decoding and expiry/issuer checks without a crypto library. Signature verification deferred to post-MVP hardening.
- Auth guard in `(authenticated)/layout.tsx` uses `x-invoke-path` header for returnUrl; falls back to `/` if header absent.
- Sign-out in `AppSidebar` now uses `useSignOut` hook (calls `POST /api/auth/sign-out`) instead of constructing the B2C logout URL client-side.

---

## Review

_Populated by `/review [####]`_

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
