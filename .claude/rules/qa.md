# QA Rules — Testurio Tests

## Coverage Requirement
Every feature must pass all three levels before it is marked complete.

## Test Locations

| Level | Location |
|-------|----------|
| Backend unit | `tests/Testurio.UnitTests/Services/EntityServiceTests.cs` |
| Backend integration | `tests/Testurio.IntegrationTests/Controllers/EntityControllerTests.cs` |
| Frontend component | co-located — `EntityComponent/EntityComponent.test.tsx` |
| E2E | `Testurio.Web/e2e/<feature-name>.spec.ts` |

## Layer Tag
Use `[Test]` tag — always implement last, after all other layers are done.
