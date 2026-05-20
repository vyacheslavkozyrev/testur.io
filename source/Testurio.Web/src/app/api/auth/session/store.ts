interface SessionData {
  userId: string;
  firstName: string | null;
  lastName: string | null;
  email: string;
  displayName: string | null;
  avatarUrl?: string;
  exp: number;
}

export type { SessionData };

const g = globalThis as { _testurioSessions?: Map<string, SessionData> };
if (!g._testurioSessions) g._testurioSessions = new Map<string, SessionData>();

export function getSessionStore(): Map<string, SessionData> {
  return g._testurioSessions!;
}
