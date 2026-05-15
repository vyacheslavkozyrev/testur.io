export interface AuthUser {
  id: string;
  /** May be null or empty; callers should fall back to email prefix. */
  displayName: string | null;
  email: string;
  avatarUrl?: string;
}

export interface SidebarState {
  collapsed: boolean;
}
