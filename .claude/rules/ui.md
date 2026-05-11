# UI Rules — Testurio.Web (Next.js / React)

## Language
TypeScript only — no `.js` files under `Testurio.Web/src`. Every file must have explicit types; avoid `any`.

```tsx
// ✅
interface ProjectCardProps {
  project: ProjectDto;
  onDelete: (id: string) => void;
}

// ❌ — inferred any, no explicit return type
const handler = (e) => setOpen(e.target.value);
```

## Data Fetching
All server state goes through React Query. No raw `fetch`/`axios` inside components. Every endpoint has an MSW mock handler.

```ts
// src/services/project/projectService.ts
import apiClient from '@/services/apiClient';
import type { ProjectDto, CreateProjectRequest } from '@/types/project.types';

export const projectService = {
  list:   ()                         => apiClient.get<ProjectDto[]>('/v1/projects').then(r => r.data),
  get:    (id: string)               => apiClient.get<ProjectDto>(`/v1/projects/${id}`).then(r => r.data),
  create: (body: CreateProjectRequest) => apiClient.post<ProjectDto>('/v1/projects', body).then(r => r.data),
  delete: (id: string)               => apiClient.delete(`/v1/projects/${id}`),
};
```

```ts
// src/mocks/handlers/project.ts
import { http, HttpResponse } from 'msw';
import type { ProjectDto } from '@/types/project.types';

const mockProject: ProjectDto = { id: '1', name: 'Demo', productUrl: 'https://example.com' };

export const projectHandlers = [
  http.get('/v1/projects',      () => HttpResponse.json([mockProject])),
  http.get('/v1/projects/:id',  () => HttpResponse.json(mockProject)),
  http.post('/v1/projects',     () => HttpResponse.json(mockProject, { status: 201 })),
  http.delete('/v1/projects/:id', () => new HttpResponse(null, { status: 204 })),
];
```

## React Query
All queries and mutations live in dedicated hooks under `src/hooks/`. Never call `useQuery`/`useMutation` directly inside a component.

```ts
// src/hooks/useProject.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { projectService } from '@/services/project/projectService';
import type { ProjectDto, CreateProjectRequest } from '@/types/project.types';
import type { ApiError } from '@/types/api.types';

export const PROJECT_KEYS = {
  all:    ['projects']              as const,
  detail: (id: string) => ['projects', id] as const,
};

export function useProjects() {
  return useQuery<ProjectDto[], ApiError>({
    queryKey: PROJECT_KEYS.all,
    queryFn:  projectService.list,
  });
}

export function useProject(id: string) {
  return useQuery<ProjectDto, ApiError>({
    queryKey: PROJECT_KEYS.detail(id),
    queryFn:  () => projectService.get(id),
    enabled:  Boolean(id),
  });
}

export function useCreateProject() {
  const qc = useQueryClient();
  return useMutation<ProjectDto, ApiError, CreateProjectRequest>({
    mutationFn: projectService.create,
    onSuccess:  () => qc.invalidateQueries({ queryKey: PROJECT_KEYS.all }),
  });
}

export function useDeleteProject() {
  const qc = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: projectService.delete,
    onSuccess:  () => qc.invalidateQueries({ queryKey: PROJECT_KEYS.all }),
  });
}
```

Never copy server data into `useState` — let React Query own it:

```tsx
// ❌
const { data } = useProjects();
const [projects, setProjects] = useState(data);  // stale copy

// ✅
const { data: projects, isPending, isError } = useProjects();
```

## File Naming
- `PascalCase` — components, pages: `ProjectCard.tsx`, `ProjectPage.tsx`
- `camelCase` — services, hooks, types: `projectService.ts`, `useProject.ts`, `project.types.ts`

## useCallback
Wrap handlers passed as props. Deps array must list every outer-scope value the function reads or writes:

```tsx
// ✅ — passed as prop, deps complete
const handleDelete = useCallback((id: string) => {
  deleteProject.mutate(id);
}, [deleteProject]);

return <ProjectList onDelete={handleDelete} />;

// ❌ — no need to wrap; used only locally, never passed down
const toggleOpen = () => setOpen(o => !o);
```

## useMemo
Use for expensive derived values — filtered lists, formatted data, computed configs. Never for primitives:

```tsx
// ✅ — filters a potentially large list
const activeProjects = useMemo(
  () => projects?.filter(p => p.status === 'active') ?? [],
  [projects],
);

// ❌ — primitive; useMemo overhead exceeds benefit
const label = useMemo(() => project.name.toUpperCase(), [project.name]);
```

## MUI
Import from specific subpaths — never barrel imports. Use `theme` for all spacing, palette, and typography:

```tsx
// ✅
import Box      from '@mui/material/Box';
import Button   from '@mui/material/Button';
import Typography from '@mui/material/Typography';

// ❌
import { Box, Button, Typography } from '@mui/material';
```

Extend the theme in one place:

```ts
// src/theme/theme.ts
import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: {
    primary: { main: '#1976d2' },
  },
  typography: {
    fontFamily: 'Inter, sans-serif',
  },
});
```

Never call `createTheme` or override theme tokens inside a component.

## Styles
Define styles as a `getStyles` function using `useMemo`, co-located at the bottom of the file. Call it at the top of the component:

```tsx
import { useTheme, type Theme } from '@mui/material/styles';
import { useMemo } from 'react';

export default function ProjectCard({ project }: ProjectCardProps) {
  const theme = useTheme();
  const styles = getStyles(theme);

  return (
    <Box sx={styles.root}>
      <Typography sx={styles.title}>{project.name}</Typography>
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(() => ({
    root:  { padding: theme.spacing(2), borderRadius: theme.shape.borderRadius },
    title: { ...theme.typography.h6, color: theme.palette.text.primary },
  }), [theme]);
```

No `style={{ ... }}` props. Use `sx` only for true one-off overrides, never for layout or repeated patterns.

## Localization
All user-visible strings go through `i18next`. No hardcoded string literals in components or pages:

```json
// src/locales/en/project.json
{
  "list": {
    "title": "Projects",
    "emptyState": "No projects yet. Create one to get started.",
    "deleteConfirm": "Delete \"{{name}}\"?"
  },
  "status": {
    "active_one":  "{{count}} active project",
    "active_other": "{{count}} active projects"
  }
}
```

```tsx
// inside a component
import { useTranslation } from 'react-i18next';

const { t } = useTranslation('project');

<Typography>{t('list.title')}</Typography>
<Typography>{t('list.deleteConfirm', { name: project.name })}</Typography>
<Typography>{t('status.active', { count: activeProjects.length })}</Typography>
```

Key naming: `<feature>.<context>.<element>`. Never reuse keys across features.

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
