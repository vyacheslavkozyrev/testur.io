import * as fs from 'fs';
import * as path from 'path';

interface SessionData {
  userId: string;
  firstName: string | null;
  lastName: string | null;
  email: string;
  displayName: string | null;
  avatarUrl?: string;
  exp: number;
  idToken: string;
}

export type { SessionData };

// ---------------------------------------------------------------------------
// In-memory store (survives Next.js HMR hot reloads via globalThis)
// ---------------------------------------------------------------------------
const g = globalThis as { _testurioSessions?: Map<string, SessionData> };
if (!g._testurioSessions) g._testurioSessions = new Map<string, SessionData>();

// ---------------------------------------------------------------------------
// File-based persistence for dev/test — survives full server restarts.
// Not used in production (NEXT_PUBLIC_APP_ENV === 'production' or NODE_ENV === 'production').
// ---------------------------------------------------------------------------
const SESSION_FILE =
  process.env.NODE_ENV !== 'production'
    ? path.join(process.cwd(), '.sessions.json')
    : null;

function loadFromFile(): void {
  if (!SESSION_FILE) return;
  try {
    if (!fs.existsSync(SESSION_FILE)) return;
    const raw = fs.readFileSync(SESSION_FILE, 'utf-8');
    const entries = JSON.parse(raw) as Array<[string, SessionData]>;
    const nowSec = Math.floor(Date.now() / 1000);
    for (const [id, data] of entries) {
      if (data.exp > nowSec) g._testurioSessions!.set(id, data);
    }
  } catch {
    // Corrupt file — start fresh
  }
}

function saveToFile(): void {
  if (!SESSION_FILE) return;
  try {
    const entries = Array.from(g._testurioSessions!.entries());
    fs.writeFileSync(SESSION_FILE, JSON.stringify(entries));
  } catch {
    // Non-fatal — in-memory store still works
  }
}

// Populate in-memory store from disk on first module load
if (g._testurioSessions!.size === 0) loadFromFile();

export function getSessionStore(): Map<string, SessionData> {
  return g._testurioSessions!;
}

export function persistSession(id: string, data: SessionData): void {
  g._testurioSessions!.set(id, data);
  saveToFile();
}

export function deleteSession(id: string): void {
  g._testurioSessions!.delete(id);
  saveToFile();
}
