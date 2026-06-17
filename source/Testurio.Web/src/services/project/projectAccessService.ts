import apiClient from '@/services/apiClient';
import type { AccessMode, ProjectAccessDto, UpdateProjectAccessRequest } from '@/types/projectAccess.types';

// .NET API serializes enum values as PascalCase (e.g. "BasicAuth"); normalize to camelCase.
function normalizeAccessMode(raw: string): AccessMode {
  return (raw.charAt(0).toLowerCase() + raw.slice(1)) as AccessMode;
}

function normalizeDto(dto: ProjectAccessDto): ProjectAccessDto {
  return { ...dto, accessMode: normalizeAccessMode(dto.accessMode as string) };
}

export const projectAccessService = {
  get: (projectId: string): Promise<ProjectAccessDto> =>
    apiClient.get<ProjectAccessDto>(`/v1/projects/${projectId}/access`).then((r) => normalizeDto(r.data)),

  update: (projectId: string, body: UpdateProjectAccessRequest): Promise<ProjectAccessDto> =>
    apiClient.patch<ProjectAccessDto>(`/v1/projects/${projectId}/access`, body).then((r) => normalizeDto(r.data)),
};
