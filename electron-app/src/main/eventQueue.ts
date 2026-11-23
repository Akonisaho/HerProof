import { existsSync, readFileSync, writeFileSync } from "node:fs";
import type { SecretStore } from "../shared/secretStore";
import type { IntegrityEvent } from "../shared/events";

interface StoredEvent extends IntegrityEvent {
  synced: boolean;
}

/**
 * Local, encrypted-at-rest, append-ordered event queue (docs/07 sections 6 and 11). `clientSeq` is
 * assigned here, monotonically, and is the single source of truth the server dedupes against
 * ((session_id, client_seq), per docs/07 section 6). Persisted to disk on every append so a crash
 * loses at most the in-flight write, never the whole queue — that's what makes resume possible.
 */
export class EventQueue {
  private events: StoredEvent[] = [];
  private nextSeq = 0;

  constructor(
    private readonly filePath: string,
    private readonly secretStore: SecretStore,
  ) {
    this.load();
  }

  private load(): void {
    if (!existsSync(this.filePath)) {
      return;
    }

    try {
      const cipherText = readFileSync(this.filePath);
      const plainText = this.secretStore.decrypt(cipherText);
      this.events = JSON.parse(plainText) as StoredEvent[];
      this.nextSeq = this.events.length === 0 ? 0 : Math.max(...this.events.map((e) => e.clientSeq)) + 1;
    } catch {
      // Corrupt or unreadable queue file: start fresh rather than crash the app. The candidate's
      // session continues; only already-synced history (which the server also has) is at risk.
      this.events = [];
      this.nextSeq = 0;
    }
  }

  private persist(): void {
    const plainText = JSON.stringify(this.events);
    writeFileSync(this.filePath, this.secretStore.encrypt(plainText));
  }

  /** Was there an unfinished session the last time this queue file was written? Used to decide
   * whether to offer "resume" on relaunch and to record session_interrupted (docs/07 section 6). */
  hasUnfinishedSession(): boolean {
    return this.events.some((e) => e.eventType === "assessment_started")
      && !this.events.some((e) => e.eventType === "assessment_submitted");
  }

  append(event: Omit<IntegrityEvent, "clientSeq">): IntegrityEvent {
    const stored: StoredEvent = { ...event, clientSeq: this.nextSeq, synced: false };
    this.nextSeq += 1;
    this.events.push(stored);
    this.persist();
    return stored;
  }

  pendingSync(): IntegrityEvent[] {
    return this.events.filter((e) => !e.synced).map(({ synced: _synced, ...rest }) => rest);
  }

  markSynced(clientSeqs: readonly number[]): void {
    const seqSet = new Set(clientSeqs);
    for (const event of this.events) {
      if (seqSet.has(event.clientSeq)) {
        event.synced = true;
      }
    }

    this.persist();
  }

  /** Ordered ascending by clientSeq — the queue never reorders on disk, but this guards any future
   * change that might append out of order (docs/07 section 11: "event queue ordering"). */
  allOrdered(): IntegrityEvent[] {
    return [...this.events].sort((a, b) => a.clientSeq - b.clientSeq).map(({ synced: _synced, ...rest }) => rest);
  }

  /** Missing clientSeq values in the recorded sequence — should always be empty for a queue that only
   * ever appends via `append()`, but this is what the server-side gap detection (docs/07 section 6)
   * mirrors, and it's worth being able to assert locally too. */
  detectGaps(): number[] {
    const seqs = this.events.map((e) => e.clientSeq).sort((a, b) => a - b);
    const gaps: number[] = [];
    for (let i = 1; i < seqs.length; i++) {
      const previous = seqs[i - 1];
      const current = seqs[i];
      if (previous === undefined || current === undefined) {
        continue;
      }

      for (let missing = previous + 1; missing < current; missing++) {
        gaps.push(missing);
      }
    }

    return gaps;
  }
}
