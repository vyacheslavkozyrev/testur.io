# UI Rules — Testurio.Web (Next.js / React)

## Language

- TypeScript only — no `.js` files under `Testurio.Web/src`

## Data Fetching

- Use **React Query** (`@tanstack/react-query`) for all server state — no raw `fetch`/`axios` calls inside components
- Every API endpoint must have an **MSW** mock handler

## React Query

- All queries live in dedicated hooks under `src/hooks/` (e.g. `useEntity.ts`) — never call `useQuery`/`useMutation` directly inside components
- Query keys must be arrays and defined as constants in the hook file: `const ENTITY_KEYS = { all: ['entity'] as const, detail: (id: string) => ['entity', id] as const }`
- Always type the `useQuery` and `useMutation` generics explicitly: `useQuery<EntityDto[], ApiError>(...)`
- Use `useMutation` for all write operations (create, update, delete); invalidate relevant query keys in `onSuccess`
- Do not store server data in local `useState` — let React Query own it

## File Naming

- `PascalCase` — components, pages
- `camelCase` — services, hooks, types

## useCallback

- Wrap event handlers and callbacks passed as props in `useCallback`
- Deps array must list every value from the outer scope the function reads or writes
- Do not wrap callbacks that are only used locally within the same component and never passed down

## useMemo

- Use `useMemo` for expensive derived values (filtered lists, formatted data, computed configs)
- Do not use `useMemo` for primitive values or simple property reads — the overhead outweighs the benefit
- Deps array must be exhaustive — same rules as `useCallback`

## MUI

- Use **MUI (Material UI)** component library — do not bring in other UI component libraries
- Import from specific subpaths to keep bundle size small: `import Button from '@mui/material/Button'`, not `import { Button } from '@mui/material'`
- Use the MUI `theme` for all spacing, palette, and typography values — no hardcoded px/color values
- Extend the theme in one place (`src/theme/theme.ts`) — never patch it inside components

## Styles

- Define styles as a separate `getStyles` function using `useMemo`, co-located at the bottom of the component file:
  ```tsx
  const getStyles = (theme: Theme) =>
    useMemo(
      () => ({
        root: { padding: theme.spacing(2) },
      }),
      [theme],
    );
  ```
- Call it at the top of the component: `const styles = getStyles(theme)`
- **No inline styles** — `style={{ ... }}` props are forbidden
- Use `sx` prop only for one-off overrides that are not worth extracting; never for layout or repeated patterns

## Localization

- All user-visible strings must use `i18next` — no hardcoded string literals in components or pages
- Translation files live in `src/locales/<lang>/<feature>.json` — one file per feature per language
- English (`en`) is the source language; add keys there first before adding other languages
- Key naming: `<feature>.<context>.<element>` — e.g. `testRun.table.emptyState`
- Never reuse keys across features — each feature owns its namespace
- Plurals and interpolations must use i18next standard format (`_one`/`_other`, `{{variable}}`)

## Per-Feature File Order

Implement in this sequence:

1. `src/types/entity.types.ts`
2. `src/services/entity/entityService.ts`
3. `src/hooks/useEntity.ts`
4. `src/mocks/handlers/entity.ts`
5. `src/components/EntityComponent/EntityComponent.tsx`
6. `src/pages/EntityPage/EntityPage.tsx`
7. `src/locales/en/entity.json`
8. `src/routes/routes.tsx`
