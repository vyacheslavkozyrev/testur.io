# Implementation Plan — [Feature Name] ([####])

## Tasks

- [ ] T001 [Migration] Add migration `[####]_[Name]` — `source/Testurio.Infrastructure/Migrations/[####]_[Name].cs`
- [ ] T002 [Domain] Create `Entity` class — `source/Testurio.Core/Entities/Entity.cs`
- [ ] T003 [Domain] Add `IEntityRepository` interface — `source/Testurio.Core/Interfaces/IEntityRepository.cs`
- [ ] T004 [Infra] Implement `EntityRepository` — `source/Testurio.Infrastructure/Repositories/EntityRepository.cs`
- [ ] T005 [Infra] Add EF Core configuration — `source/Testurio.Infrastructure/Configurations/EntityConfiguration.cs`
- [ ] T006 [Infra] Register repository in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T007 [App] Create `EntityDto` — `source/Testurio.Api/DTOs/EntityDto.cs`
- [ ] T008 [App] Implement `EntityService` — `source/Testurio.Api/Services/EntityService.cs`
- [ ] T009 [API] Register entity endpoints — `source/Testurio.Api/Endpoints/EntityEndpoints.cs`
- [ ] T010 [UI] Add API types — `source/Testurio.Web/src/types/entity.types.ts`
- [ ] T011 [UI] Add API client — `source/Testurio.Web/src/services/entity/entityService.ts`
- [ ] T012 [UI] Add React Query hook — `source/Testurio.Web/src/hooks/useEntity.ts`
- [ ] T013 [UI] Add MSW mock handler — `source/Testurio.Web/src/mocks/handlers/entity.ts`
- [ ] T014 [UI] Create component — `source/Testurio.Web/src/components/EntityComponent/EntityComponent.tsx`
- [ ] T015 [UI] Add page component — `source/Testurio.Web/src/pages/EntityPage/EntityPage.tsx`
- [ ] T016 [UI] Add translation keys — `source/Testurio.Web/src/locales/en/entity.json`
- [ ] T017 [UI] Register route — `source/Testurio.Web/src/routes/routes.tsx`
- [ ] T018 [Test] Backend unit tests — `tests/Testurio.UnitTests/Services/EntityServiceTests.cs`
- [ ] T019 [Test] Backend integration tests — `tests/Testurio.IntegrationTests/Controllers/EntityControllerTests.cs`
- [ ] T020 [Test] Frontend component tests — `source/Testurio.Web/src/components/EntityComponent/EntityComponent.test.tsx`
- [ ] T021 [Test] E2E tests — `source/Testurio.Web/e2e/[feature-name].spec.ts`

## Rationale

[Explain the ordering: why migration first, why domain before infrastructure, etc.]

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
