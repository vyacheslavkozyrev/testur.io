# Implementation Plan — [Feature Name] ([####])

## Tasks

- [ ] T001 [Migration] Add migration `[####]_[Name]` — `cpr-api/src/CPR.Infrastructure/Data/Migrations/[####]_[Name].cs`
- [ ] T002 [Domain] Create `Entity` class — `cpr-api/src/CPR.Domain/Entities/Entity.cs`
- [ ] T003 [Domain] Add `IEntityRepository` interface — `cpr-api/src/CPR.Domain/Repositories/IEntityRepository.cs`
- [ ] T004 [Infra] Implement `EntityRepository` — `cpr-api/src/CPR.Infrastructure/Data/Repositories/EntityRepository.cs`
- [ ] T005 [Infra] Add EF Core configuration — `cpr-api/src/CPR.Infrastructure/Data/Configurations/EntityConfiguration.cs`
- [ ] T006 [Infra] Register repository in DI — `cpr-api/src/CPR.Infrastructure/DependencyInjection.cs`
- [ ] T007 [App] Create `EntityDto` — `cpr-api/src/CPR.Application/DTOs/EntityDto.cs`
- [ ] T008 [App] Implement `EntityService` — `cpr-api/src/CPR.Application/Services/EntityService.cs`
- [ ] T009 [API] Create `EntityController` — `cpr-api/src/CPR.Api/Controllers/EntityController.cs`
- [ ] T010 [UI] Add API types — `cpr-ui/src/types/entity.types.ts`
- [ ] T011 [UI] Add API client — `cpr-ui/src/services/entity/entityService.ts`
- [ ] T012 [UI] Add React Query hook — `cpr-ui/src/hooks/useEntity.ts`
- [ ] T013 [UI] Add MSW mock handler — `cpr-ui/src/mocks/handlers/entity.ts`
- [ ] T014 [UI] Create component — `cpr-ui/src/components/EntityComponent/EntityComponent.tsx`
- [ ] T015 [UI] Add page component — `cpr-ui/src/pages/EntityPage/EntityPage.tsx`
- [ ] T016 [UI] Add translation keys — `cpr-ui/src/locales/en/entity.json`
- [ ] T017 [UI] Register route — `cpr-ui/src/routes/routes.tsx`
- [ ] T018 [Test] Backend unit tests — `cpr-api/tests/CPR.UnitTests/Services/EntityServiceTests.cs`
- [ ] T019 [Test] Backend integration tests — `cpr-api/tests/CPR.IntegrationTests/Controllers/EntityControllerTests.cs`
- [ ] T020 [Test] Frontend component tests — `cpr-ui/src/components/EntityComponent/EntityComponent.test.tsx`
- [ ] T021 [Test] E2E tests — `cpr-ui/e2e/[feature-name].spec.ts`

## Rationale

[Explain the ordering: why migration first, why domain before infrastructure, etc.]

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects |
| `[Infra]` | Repositories, EF config, DI registration |
| `[App]` | DTOs, services, validators |
| `[API]` | Controllers, middleware, route config |
| `[Config]` | App configuration, environment settings, constants, feature flags |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Test files |
