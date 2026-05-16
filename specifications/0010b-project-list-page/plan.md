# Implementation Plan — Projects List Page (0010b)

## Tasks

- [x] T001 [UI] Extend `useProject` hook — add `useProjects` query (query key `['projects']`, calls `projectService.list()`, sorts result by `createdAt` descending client-side) — `source/Testurio.Web/src/hooks/useProject.ts`
- [x] T002 [UI] Add `truncateText` utility — pure function `(text: string, maxLength: number) => string` that returns the original string unchanged when `text.length <= maxLength`, otherwise returns `text.slice(0, maxLength).trimEnd() + '…'` — `source/Testurio.Web/src/utils/truncateText.ts`
- [x] T003 [UI] Create `ProjectListCard` component — accepts `project: ProjectDto`; renders name, product URL, testing strategy (truncated to 120 chars via `truncateText`); wraps entire card surface in a Next.js `Link` to `PROJECT_HISTORY_ROUTE(project.projectId)`; includes an MUI `IconButton` with `EditOutlined` icon in the top-right corner that navigates to `PROJECT_SETTINGS_ROUTE(project.projectId)` via `router.push` with `stopPropagation` so it does not trigger the card `Link`; icon button has `aria-label="Edit project"` — `source/Testurio.Web/src/components/ProjectListCard/ProjectListCard.tsx`
- [x] T004 [UI] Create `ProjectsPage` page component — renders a page header with a "Create Project" `Button` that navigates to `/projects/new`; integrates `useProjects`; shows loading skeleton during fetch; shows inline error state with "Retry" button on failure; shows empty state panel (icon, heading, description, "Create your first project" `Button` navigating to `/projects/new`) when the response is an empty array; renders a card grid of `ProjectListCard` components when projects are present — `source/Testurio.Web/src/pages/ProjectsPage/ProjectsPage.tsx`
- [x] T005 [UI] Extend translation keys — add `projects` namespace keys for: page title, create button label, empty state heading, empty state description, empty state CTA label, error message, retry button label, edit button aria-label — `source/Testurio.Web/src/locales/en/projects.json`
- [x] T006 [UI] Register route — add `/projects` route mapped to `ProjectsPage` in `source/Testurio.Web/src/routes/routes.tsx`; confirm `PROJECT_HISTORY_ROUTE` and `PROJECT_SETTINGS_ROUTE` constants are already exported (added by feature 0010 T021); do not redefine them
- [x] T007 [Test] Frontend component tests for `ProjectListCard` — card `Link` points to correct history URL, edit icon button navigates to settings URL without triggering card navigation, `aria-label="Edit project"` present, long testing strategy truncated at 120 chars with ellipsis, short strategy shown in full — `source/Testurio.Web/src/components/ProjectListCard/ProjectListCard.test.tsx`
- [x] T008 [Test] Frontend component tests for `ProjectsPage` — loading skeleton visible during fetch, empty state shown on empty array response, "Create your first project" button navigates to `/projects/new`, error state visible on failed fetch with "Retry" re-triggers query, card grid renders sorted by `createdAt` descending — `source/Testurio.Web/src/pages/ProjectsPage/ProjectsPage.test.tsx`
- [x] T009 [Test] Unit tests for `truncateText` — text at limit returned unchanged, text below limit returned unchanged, text above limit truncated with ellipsis, whitespace trimmed before ellipsis, empty string handled, `maxLength: 0` returns ellipsis only — `tests/Testurio.UnitTests/` is a .NET project; place this test in `source/Testurio.Web/src/utils/truncateText.test.ts` as a TypeScript unit test
- [ ] T010 [Test] E2E tests — authenticated user sees card grid sorted by `createdAt` descending, empty state CTA navigates to `/projects/new`, card click navigates to `/projects/:id/history`, edit icon click navigates to `/projects/:id/settings` without navigating to history — `source/Testurio.Web/e2e/projects-list.spec.ts`

## Rationale

**No backend tasks.** `GET /v1/projects` is fully implemented by feature 0006 (`ProjectEndpoints.cs`, `ProjectService.ListAsync`, `ProjectRepository.ListByUserAsync`, and the Cosmos query scoped by `userId` partition key). No new domain, infrastructure, application, or API work is required.

**`useProjects` extended into the existing hook file (T001) rather than creating a new file.** Feature 0006 already created `source/Testurio.Web/src/hooks/useProject.ts` with `useProject(id)` and `useCreateProject`. Adding `useProjects` to the same file avoids a second hook module for the same resource and keeps all project-query keys co-located in `PROJECT_KEYS`.

**`truncateText` utility before the component (T002 before T003).** The component depends on the utility for the AC-004 truncation requirement. Extracting it to a utility function makes it independently testable (T009) without mounting a component.

**`ProjectListCard` before `ProjectsPage` (T003 before T004).** `ProjectsPage` composes `ProjectListCard`; the leaf component must exist first so the page can import it without circular dependency risk.

**No new MSW handler required.** Feature 0006 T012 already registered a handler for `GET /v1/projects` at `source/Testurio.Web/src/mocks/handlers/project.ts`. Component and E2E tests will reuse it. If the mock response needs additional projects to test sorting, extra entries can be added to the existing mock array in the same handler file (update, not replace).

**Route constants not redefined (T006).** `PROJECT_HISTORY_ROUTE` and `PROJECT_SETTINGS_ROUTE` were introduced by feature 0010 T021. T006 registers the `/projects` page route only and imports the existing constants — no duplicate definition.

**Translation keys in a new `projects.json` namespace (T005).** Feature 0006 added `project.json` (singular) for the form and settings page. The list page introduces new UI surface with distinct copy, so a separate `projects.json` namespace keeps concerns separated and avoids editing a shared file mid-stream.

**Tests last (T007–T010).** Component tests (T007–T008) mount components that depend on the MSW handler and the hook, both of which must exist first. The `truncateText` unit test (T009) is co-located with the utility in the Web project following the frontend test convention. E2E tests (T010) require the full dev server and registered route.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
