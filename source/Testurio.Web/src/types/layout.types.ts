export interface AuthUser {
  id: string;
  displayName: string;
  email: string;
  avatarUrl?: string;
}

export interface SidebarState {
  collapsed: boolean;
}
