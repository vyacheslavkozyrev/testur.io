export type AccessMode = 'ipAllowlist' | 'basicAuth' | 'headerToken';

export interface ProjectAccessDto {
  projectId: string;
  accessMode: AccessMode;
  /** Pre-filled when mode is basicAuth; null otherwise. */
  basicAuthUser: string | null;
  /** Pre-filled when mode is headerToken; null otherwise. */
  headerTokenName: string | null;
}

export interface UpdateProjectAccessRequest {
  accessMode: AccessMode;
  /** Only sent when saving basicAuth mode; never returned from API. */
  basicAuthUser?: string | null;
  /** Only sent when saving basicAuth mode; never returned from API. */
  basicAuthPass?: string | null;
  /** Only sent when saving headerToken mode. */
  headerTokenName?: string | null;
  /** Only sent when saving headerToken mode; never returned from API. */
  headerTokenValue?: string | null;
}
