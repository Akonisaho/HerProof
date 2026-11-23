import type { IntegrityEvent } from "../shared/events";

export interface SyncResult {
  acceptedClientSeqs: number[];
}

export interface SessionInfo {
  status: string;
  startedAtUtc: string | null;
  durationMinutes: number;
  serverTimeUtc: string;
}

/**
 * Batched upload to HerProof's integrity-events endpoint (docs/07 section 4: "sync to server every
 * 30 seconds or on change when online"). Never throws on a network failure — offline is an expected,
 * routine state (docs/01 section 15: "Electron loses network -> local autosave and event queue,
 * resume and sync, record interruption"), not an error the candidate should see.
 */
export class SyncClient {
  constructor(
    private readonly apiBaseUrl: string,
    private readonly getAccessToken: () => string | null,
    private readonly fetchImpl: typeof fetch = fetch,
  ) {}

  async syncBatch(sessionId: string, events: readonly IntegrityEvent[]): Promise<SyncResult> {
    if (events.length === 0) {
      return { acceptedClientSeqs: [] };
    }

    const token = this.getAccessToken();
    if (!token) {
      return { acceptedClientSeqs: [] };
    }

    try {
      const response = await this.fetchImpl(`${this.apiBaseUrl}/api/assessment-sessions/${sessionId}/events`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ events }),
      });

      if (!response.ok) {
        return { acceptedClientSeqs: [] };
      }

      const result = (await response.json()) as SyncResult;
      return result;
    } catch {
      // Offline, DNS failure, timeout — all routine. The caller keeps unsynced events queued and
      // retries on the next tick; nothing here is ever surfaced as a candidate-facing error.
      return { acceptedClientSeqs: [] };
    }
  }

  /**
   * The server-authoritative source for the agent's timer (HP-026, closed): StartedAtUtc and
   * DurationMinutes both come from the server, not the client, so the countdown the candidate sees
   * can't be extended by changing the system clock. Returns null on any failure — the renderer falls
   * back to showing an elapsed-time stopwatch with no deadline rather than breaking the assessment.
   */
  async fetchSessionInfo(sessionId: string): Promise<SessionInfo | null> {
    const token = this.getAccessToken();
    if (!token) {
      return null;
    }

    try {
      const response = await this.fetchImpl(`${this.apiBaseUrl}/api/assessment-sessions/${sessionId}/info`, {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        return null;
      }

      return (await response.json()) as SessionInfo;
    } catch {
      return null;
    }
  }

  /**
   * The only place the candidate's actual solution reaches the server from the Electron agent
   * (HP-026/HP-028): the server decides, from its own clock and the session's real StartedAtUtc,
   * whether this lands as a Submitted or a TimedOut AssessmentSubmission — never the client.
   */
  async submitCode(sessionId: string, code: string): Promise<boolean> {
    const token = this.getAccessToken();
    if (!token) {
      return false;
    }

    try {
      const response = await this.fetchImpl(`${this.apiBaseUrl}/api/assessment-sessions/${sessionId}/code`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ code }),
      });

      return response.ok;
    } catch {
      return false;
    }
  }
}
