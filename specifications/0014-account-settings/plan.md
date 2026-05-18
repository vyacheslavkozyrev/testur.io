# Implementation Plan — Account Settings (0014)

## Tasks

- [ ] T001 [Domain] Add `UserDocument` record — `source/Testurio.Core/Entities/UserDocument.cs`
- [ ] T002 [Domain] Add `IUserRepository` interface — `source/Testurio.Core/Repositories/IUserRepository.cs`
- [ ] T003 [Infra] Implement `UserRepository` (Cosmos upsert/read by `userId`) — `source/Testurio.Infrastructure/Cosmos/UserRepository.cs`
- [ ] T004 [Infra] Add `Users` container to `CosmosDbInitializer` — `source/Testurio.Infrastructure/Cosmos/CosmosDbInitializer.cs`
- [ ] T005 [Infra] Register `IUserRepository` in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T006 [App] Create `AccountProfileDto` and `AccountPreferencesDto` — `source/Testurio.Api/DTOs/AccountDtos.cs`
- [ ] T007 [App] Create `UpdateProfileRequest` and `UpdatePreferencesRequest` with data annotations — `source/Testurio.Api/DTOs/AccountDtos.cs`
- [ ] T008 [App] Implement `IAccountService` interface — `source/Testurio.Api/Services/IAccountService.cs`
- [ ] T009 [App] Implement `AccountService` (`GetProfileAsync`, `UpdateProfileAsync`, `GetPreferencesAsync`, `UpdatePreferencesAsync`) — `source/Testurio.Api/Services/AccountService.cs`
- [ ] T010 [App] Register `AccountService` in DI (scoped) — `source/Testurio.Api/Program.cs`
- [ ] T011 [API] Create `AccountEndpoints` with four routes: `GET /v1/account/profile`, `PATCH /v1/account/profile`, `GET /v1/account/preferences`, `PATCH /v1/account/preferences`; wire validation filter on PATCH routes — `source/Testurio.Api/Endpoints/AccountEndpoints.cs`
- [ ] T012 [API] Register `AccountEndpoints` in `Program.cs` — `source/Testurio.Api/Program.cs`
- [ ] T013 [UI] Add `AccountPreferencesDto`, `UpdateProfileRequest`, `UpdatePreferencesRequest` TypeScript types — `source/Testurio.Web/src/types/account.types.ts`
- [ ] T014 [UI] Implement `accountService` with `getPreferences`, `updateProfile`, `updatePreferences` methods — `source/Testurio.Web/src/services/account/accountService.ts`
- [ ] T015 [UI] Add `useAccountPreferences`, `useUpdateProfile`, `useUpdatePreferences` React Query hooks — `source/Testurio.Web/src/hooks/useAccount.ts`
- [ ] T016 [UI] Add MSW mock handlers for all four account endpoints — `source/Testurio.Web/src/mocks/handlers/account.ts`
- [ ] T017 [UI] Register mock handlers in the MSW setup — `source/Testurio.Web/src/mocks/handlers/index.ts`
- [ ] T018 [UI] Create `ThemeContext` provider that reads `localStorage` (`testurio.theme`) at init, applies MUI `palette.mode`, exposes `theme` and `setTheme` — `source/Testurio.Web/src/theme/ThemeContext.tsx`
- [ ] T019 [UI] Wrap authenticated layout with `ThemeContext.Provider` so the theme applies to all portal pages — `source/Testurio.Web/src/app/(authenticated)/layout.tsx`
- [ ] T020 [UI] Create `PersonalInfoSection` component (display name field + save button with loading/error states) — `source/Testurio.Web/src/components/PersonalInfoSection/PersonalInfoSection.tsx`
- [ ] T021 [UI] Create `PreferencesSection` component (language `Select` + appearance `ToggleButtonGroup`) — `source/Testurio.Web/src/components/PreferencesSection/PreferencesSection.tsx`
- [ ] T022 [UI] Create `AccountSettingsPage` view that composes `PersonalInfoSection` and `PreferencesSection`; orchestrates parallel data loading (skeleton states) and shows success snackbar — `source/Testurio.Web/src/views/AccountSettingsPage/AccountSettingsPage.tsx`
- [ ] T023 [UI] Replace the `/settings` page stub with `AccountSettingsPage` — `source/Testurio.Web/src/app/(authenticated)/settings/page.tsx`
- [ ] T024 [UI] Add `settings` translation keys (all user-visible strings) — `source/Testurio.Web/src/locales/en/settings.json`
- [ ] T025 [UI] Add Ukrainian translation file (mirrors `settings.json` structure) — `source/Testurio.Web/src/locales/uk/settings.json`
- [ ] T026 [Test] Backend unit tests for `AccountService` (profile update, preferences merge, 404 on missing prefs, validation errors) — `tests/Testurio.UnitTests/Services/AccountServiceTests.cs`
- [ ] T027 [Test] Backend integration tests for `AccountEndpoints` (GET profile, PATCH profile 200/400, GET preferences 200/404, PATCH preferences 200/400) — `tests/Testurio.IntegrationTests/Controllers/AccountControllerTests.cs`
- [ ] T028 [Test] Frontend component tests for `PersonalInfoSection` (pre-population, required validation, char-limit validation, loading state, success snackbar, error banner) — `source/Testurio.Web/src/components/PersonalInfoSection/PersonalInfoSection.test.tsx`
- [ ] T029 [Test] Frontend component tests for `PreferencesSection` (language dropdown defaults, immediate theme apply, save calls patch, rollback on error) — `source/Testurio.Web/src/components/PreferencesSection/PreferencesSection.test.tsx`
- [ ] T030 [Test] Frontend component tests for `AccountSettingsPage` (skeleton while loading, error banner on prefs fetch failure, both sections rendered after load) — `source/Testurio.Web/src/views/AccountSettingsPage/AccountSettingsPage.test.tsx`
- [ ] T031 [Test] E2E tests: navigate to `/settings`, update display name, header refreshes; change language, portal re-renders in new locale; toggle dark mode, portal switches palette — `source/Testurio.Web/e2e/account-settings.spec.ts`

## Rationale

**Domain first (T001–T002).** `UserDocument` defines the Cosmos schema and `IUserRepository` the persistence contract. All higher layers depend on these abstractions; defining them first lets the infrastructure and service layers reference stable interfaces without forward-coupling.

**Infrastructure before application (T003–T005).** `UserRepository` implements `IUserRepository` against the Cosmos SDK. `CosmosDbInitializer` must be updated (T004) to create the `Users` container on startup so development and integration-test environments have the container before any endpoint can reach it. DI registration (T005) wires the concrete class to the interface so `AccountService` can receive it.

**DTOs and service before endpoints (T006–T010).** `AccountProfileDto`, `AccountPreferencesDto`, and the two request types (T006–T007) are the data contracts used by both the service and the endpoint layer. `AccountService` (T008–T009) owns the business logic (upsert semantics, preference merge, validation) and is kept testable by depending only on `IUserRepository`. DI registration (T010) is a prerequisite before the endpoint layer can resolve the service.

**Endpoints after service (T011–T012).** `AccountEndpoints` is thin — it extracts `userId` from the JWT, delegates to `AccountService`, and returns `TypedResults`. It cannot be written until the service and DTOs exist. Registering endpoints in `Program.cs` (T012) is the final backend step.

**Frontend types before service (T013–T014).** TypeScript types must match the API contracts defined in T006–T007 before `accountService` can be typed. `accountService` is a leaf module with no component dependencies — it wraps the `apiClient` and exposes typed async functions for the hooks.

**Hooks after service (T015).** `useAccountPreferences`, `useUpdateProfile`, and `useUpdatePreferences` call `accountService`; they must be written after T014. They follow the React Query pattern already established across the codebase.

**MSW handlers before components (T016–T017).** Components and their tests use MSW to intercept API calls. All four account endpoint mocks must be registered before any component test runs. Updating `handlers/index.ts` (T017) ensures the mock server includes them automatically.

**ThemeContext before layout update (T018–T019).** The `ThemeContext` provider must exist before the authenticated layout can reference it. Initialising from `localStorage` at provider creation (T018) prevents a flash of unstyled content. Wrapping the authenticated layout (T019) makes the theme apply globally to all portal pages, which is a prerequisite for `PreferencesSection` to control the palette.

**Section components before page (T020–T022).** `PersonalInfoSection` and `PreferencesSection` are self-contained form sections. They are composed inside `AccountSettingsPage`, so the sections must be ready before the page assembly task. `AccountSettingsPage` itself (T022) orchestrates the parallel data load, skeleton states, and the success snackbar.

**Page stub replacement (T023).** The `/settings` page stub already exists from feature 0010a and simply returns `null`. T023 replaces it with `AccountSettingsPage`; this task is intentionally last in the UI sequence so it only runs once the view is complete.

**Translations after components (T024–T025).** All user-visible strings are identified once the components are finalised. English keys (T024) are defined first; the Ukrainian file (T025) mirrors the same structure. This ordering avoids defining keys that are never used and ensures nothing is missed.

**Tests last (T026–T031).** Backend unit tests (T026) mock `IUserRepository` and verify service logic in isolation. Integration tests (T027) use the test host to exercise the full HTTP stack including auth and validation. Frontend component tests (T028–T030) use MSW mock handlers (T016) and test each component's rendering, validation, and interaction behaviour. E2E tests (T031) run last as they require the full stack — authenticated session, live API, and browser.

**Cross-feature dependencies.**

- **Feature 0013 (Registration & Sign-In)**: The `GET /api/auth/me` route and `useAuthUser` hook established by 0013 provide the `displayName` and `email` used to pre-populate the Personal Information section. Feature 0014 depends on 0013 being complete.
- **Feature 0010a (Private Cabinet Layout)**: The Settings sidebar link and `/settings` page stub were created in 0010a. Feature 0014 replaces the stub (T023) and adds `ThemeContext` to the authenticated layout (T019). 0010a must be complete first.
- **Feature 0015 (Plan Purchase)**: No dependency in either direction — billing is a separate section not covered here.
- **Feature 0016 (Subscription Management)**: No dependency — manages plan changes, not profile or preferences.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
