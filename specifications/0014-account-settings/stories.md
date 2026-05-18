# User Stories — Account Settings (0014)

## Out of Scope

The following are explicitly **not** part of this feature:

- Plan purchase or subscription management — covered by features 0015 and 0016
- Password change — this is handled entirely by Azure AD B2C's SSPR flow; no UI is needed inside the portal
- Email address change — B2C owns the identity record; email changes are out of scope for v1
- Avatar / profile picture upload — B2C provides the avatar URL; upload is not supported at MVP
- Multi-user / team accounts — single-user accounts only at v1
- Notification preferences — no in-app or email notification system exists at MVP
- Dark mode implementation details beyond persisting and applying the preference — the CSS/theme toggle mechanism is owned by this feature but the full theming system is not changed
- Social login linking — B2C manages linked identity providers; no portal UI for this at v1

---

## Stories

### US-001: Update Personal Information

**As a** QA lead
**I want to** update my display name from the Account Settings page
**So that** the header always shows the correct name and my account feels personalised

#### Acceptance Criteria

- [ ] AC-001: A `/settings` page exists inside the authenticated shell and is reachable from the sidebar **Settings** link implemented in feature 0010a
- [ ] AC-002: The page renders a **Personal Information** section containing a **Display Name** text field pre-populated with the current value from `GET /api/auth/me`
- [ ] AC-003: Submitting the form with a non-empty display name calls `PATCH /v1/account/profile` with `{ displayName: string }`; on success, the React Query cache for `AUTH_KEYS.me` is invalidated so the header refreshes automatically
- [ ] AC-004: Display name is required; an inline validation error _"Display name is required"_ appears if the field is blank when the user attempts to save
- [ ] AC-005: Display name is limited to 100 characters; an inline validation error _"Display name must be 100 characters or fewer"_ appears when the limit is exceeded
- [ ] AC-006: While the save request is in flight, the **Save** button is disabled and shows a loading indicator; the field is also disabled to prevent edits mid-request
- [ ] AC-007: On a successful save, a success snackbar _"Settings saved"_ is shown
- [ ] AC-008: On a server error, an inline error banner _"Failed to save settings. Please try again."_ is shown and the button returns to its enabled state
- [ ] AC-009: All strings on the Account Settings page go through `i18next` with keys under the `settings` namespace

#### Edge Cases

- Leading/trailing whitespace in the display name is trimmed before submission
- If `displayName` from the session is `null` or empty, the field is rendered empty (not pre-filled with the email prefix used by the header)

---

### US-002: Choose Display Language

**As a** QA lead
**I want to** select the display language for the portal
**So that** all labels, messages, and UI strings are shown in the language I am most comfortable with

#### Acceptance Criteria

- [ ] AC-010: The Account Settings page contains a **Preferences** section with a **Language** dropdown (MUI `Select`)
- [ ] AC-011: The dropdown lists exactly the supported locales at MVP: **English (en)** and **Ukrainian (uk)**
- [ ] AC-012: The dropdown is pre-populated with the user's current language preference loaded from `GET /v1/account/preferences`
- [ ] AC-013: If no preference exists yet (first visit), the dropdown defaults to the browser's detected locale (`navigator.language`), falling back to `en` if the detected locale is not supported
- [ ] AC-014: Selecting a new language and clicking **Save** calls `PATCH /v1/account/preferences` with `{ language: "en" | "uk" }`
- [ ] AC-015: On a successful save, `i18next.changeLanguage(selectedLanguage)` is called immediately so the entire portal switches language without a page reload; the success snackbar also updates to the new language
- [ ] AC-016: The selected language is persisted in `localStorage` under the key `testurio.language` so the preference is applied on the next page load before the API response arrives (optimistic restore)
- [ ] AC-017: The language preference is also written to `PATCH /v1/account/preferences` so it is available server-side for future SSR and email rendering

#### Edge Cases

- If `localStorage` is unavailable (private browsing), the in-memory i18next language still changes; the API preference is still saved; no error is thrown
- If `PATCH /v1/account/preferences` fails, the language change applied via `i18next.changeLanguage` is rolled back to the previous value

---

### US-003: Set Appearance Theme

**As a** QA lead
**I want to** switch between light and dark mode
**So that** the portal is comfortable to use in different lighting conditions and matches my personal preference

#### Acceptance Criteria

- [ ] AC-018: The **Preferences** section contains an **Appearance** toggle with two options: **Light** and **Dark** (rendered as an MUI `ToggleButtonGroup` with two `ToggleButton` elements)
- [ ] AC-019: The current selection is loaded from `GET /v1/account/preferences`; if no preference exists, the default is **Light**
- [ ] AC-020: Clicking **Dark** or **Light** immediately applies the chosen theme to the portal without requiring the user to click **Save** (the change is applied optimistically on click)
- [ ] AC-021: Saving calls `PATCH /v1/account/preferences` with `{ theme: "light" | "dark" }` (merged with any other preference fields being saved in the same request)
- [ ] AC-022: The theme preference is persisted in `localStorage` under the key `testurio.theme` and applied on every page load before the API response arrives
- [ ] AC-023: The theme toggle controls the MUI `createTheme` `palette.mode` via a React context provider (`ThemeContext`) that wraps the authenticated layout; toggling switches the CSS palette globally
- [ ] AC-024: The `ThemeContext` is initialised from `localStorage` before the first render to avoid a flash of unstyled content (FOUC)

#### Edge Cases

- If `localStorage` is unavailable, the toggle still works in-memory; it does not throw
- If `PATCH /v1/account/preferences` fails after the optimistic apply, the theme is rolled back to the previous value and an error banner is shown

---

### US-004: Load and Display Current Settings

**As a** QA lead
**I want to** see my current display name, language, and appearance preference already filled in when I open Account Settings
**So that** I can see what is currently saved and only change what I want to update

#### Acceptance Criteria

- [ ] AC-025: On page load, `GET /api/auth/me` and `GET /v1/account/preferences` are fetched in parallel using React Query; all form fields are populated from the responses before the user can interact with them
- [ ] AC-026: While either request is pending, the Account Settings page shows a skeleton / loading state for each section (MUI `Skeleton` components for each form field)
- [ ] AC-027: If `GET /v1/account/preferences` returns `404` (no preferences saved yet), the form uses the defaults described in AC-013 and AC-019; this is not treated as an error
- [ ] AC-028: If `GET /v1/account/preferences` returns any other error, an error banner _"Failed to load preferences. Please refresh the page."_ is shown; the personal information section (display name) still renders using data from `GET /api/auth/me`

#### Edge Cases

- If the user navigates away and back to the settings page, React Query returns cached data immediately while re-validating in the background (`staleTime: 5 * 60 * 1000`)

---

### US-005: Backend — Account Profile Endpoint

**As a** backend system
**I want to** expose a `PATCH /v1/account/profile` endpoint
**So that** the frontend can persist display name changes against the authenticated user's record

#### Acceptance Criteria

- [ ] AC-029: `PATCH /v1/account/profile` accepts `{ displayName: string }` in the request body; requires a valid JWT
- [ ] AC-030: `displayName` must be between 1 and 100 characters; a `400 Bad Request` with a `ProblemDetails` body is returned if the constraint is violated
- [ ] AC-031: The endpoint extracts `userId` from the JWT `oid` claim and updates the `displayName` field in the `Users` Cosmos container (partition key: `userId`)
- [ ] AC-032: On success the endpoint returns `200 OK` with the updated `AccountProfileDto` (`{ userId, displayName }`)
- [ ] AC-033: If no user document exists for the `userId`, the endpoint creates a new document with the provided `displayName` (upsert semantics)

---

### US-006: Backend — Account Preferences Endpoint

**As a** backend system
**I want to** expose `GET` and `PATCH` endpoints for account preferences
**So that** the frontend can load and persist language and theme choices per user

#### Acceptance Criteria

- [ ] AC-034: `GET /v1/account/preferences` requires a valid JWT and returns `AccountPreferencesDto` (`{ language: string, theme: string }`) for the authenticated user; returns `404` if no preferences document exists
- [ ] AC-035: `PATCH /v1/account/preferences` accepts a partial `{ language?: string, theme?: string }` body; merges the provided fields with the existing document (if any) and upserts to Cosmos
- [ ] AC-036: `language` must be one of `["en", "uk"]`; a `400 Bad Request` is returned otherwise
- [ ] AC-037: `theme` must be one of `["light", "dark"]`; a `400 Bad Request` is returned otherwise
- [ ] AC-038: Both endpoints extract `userId` from the JWT `oid` claim and use it as the Cosmos partition key so preferences are strictly per-user
- [ ] AC-039: On a successful `PATCH`, `200 OK` is returned with the full merged `AccountPreferencesDto`

---

### US-007: Backend — User Document in Cosmos

**As a** backend system
**I want to** store user profile and preferences in a dedicated `Users` Cosmos container
**So that** user-level data is isolated from project-level data and scoped by `userId`

#### Acceptance Criteria

- [ ] AC-040: A `Users` Cosmos container exists with partition key `/userId`
- [ ] AC-041: The `UserDocument` schema holds: `id` (= `userId`), `userId`, `displayName` (nullable), `language` (nullable), `theme` (nullable), `createdAt`, `updatedAt`
- [ ] AC-042: The container is created by `CosmosDbInitializer` on Worker/API startup if it does not already exist
- [ ] AC-043: All reads and writes use `userId` as both the document `id` and the partition key, making every operation a single-partition point read/write (no cross-partition queries)
