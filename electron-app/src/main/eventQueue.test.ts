import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { EventQueue } from "./eventQueue";
import { PlainTextStore } from "../shared/secretStore";
import type { IntegrityEvent } from "../shared/events";

describe("EventQueue", () => {
  let dir: string;
  let filePath: string;

  beforeEach(() => {
    dir = mkdtempSync(join(tmpdir(), "herproof-queue-test-"));
    filePath = join(dir, "events.json");
  });

  afterEach(() => {
    rmSync(dir, { recursive: true, force: true });
  });

  const baseEvent: Omit<IntegrityEvent, "clientSeq" | "eventType"> = {
    sessionId: "11111111-1111-4111-8111-111111111111",
    assessmentId: "22222222-2222-4222-8222-222222222222",
    candidateId: "33333333-3333-4333-8333-333333333333",
    occurredAt: new Date().toISOString(),
    metadata: {},
  };

  it("assigns monotonically increasing client_seq values in append order", () => {
    const queue = new EventQueue(filePath, new PlainTextStore());

    const first = queue.append({ ...baseEvent, eventType: "assessment_started" });
    const second = queue.append({ ...baseEvent, eventType: "focus_lost" });
    const third = queue.append({ ...baseEvent, eventType: "focus_regained" });

    expect(first.clientSeq).toBe(0);
    expect(second.clientSeq).toBe(1);
    expect(third.clientSeq).toBe(2);
  });

  it("returns events ordered by client_seq regardless of any internal reordering", () => {
    const queue = new EventQueue(filePath, new PlainTextStore());
    queue.append({ ...baseEvent, eventType: "assessment_started" });
    queue.append({ ...baseEvent, eventType: "focus_lost" });

    const ordered = queue.allOrdered();

    expect(ordered.map((e) => e.clientSeq)).toEqual([0, 1]);
    expect(ordered.map((e) => e.eventType)).toEqual(["assessment_started", "focus_lost"]);
  });

  it("marks only the synced client_seq values as synced, leaving the rest pending", () => {
    const queue = new EventQueue(filePath, new PlainTextStore());
    queue.append({ ...baseEvent, eventType: "assessment_started" });
    queue.append({ ...baseEvent, eventType: "focus_lost" });
    queue.append({ ...baseEvent, eventType: "focus_regained" });

    queue.markSynced([0, 1]);

    expect(queue.pendingSync().map((e) => e.clientSeq)).toEqual([2]);
  });

  it("detects no gaps in a normally-appended sequence", () => {
    const queue = new EventQueue(filePath, new PlainTextStore());
    queue.append({ ...baseEvent, eventType: "assessment_started" });
    queue.append({ ...baseEvent, eventType: "focus_lost" });
    queue.append({ ...baseEvent, eventType: "focus_regained" });

    expect(queue.detectGaps()).toEqual([]);
  });

  it("resumes from a persisted queue file with the correct next client_seq", () => {
    const first = new EventQueue(filePath, new PlainTextStore());
    first.append({ ...baseEvent, eventType: "assessment_started" });
    first.append({ ...baseEvent, eventType: "focus_lost" });

    // Simulates a relaunch after a crash: a brand-new EventQueue instance reading the same file.
    const resumed = new EventQueue(filePath, new PlainTextStore());
    const next = resumed.append({ ...baseEvent, eventType: "focus_regained" });

    expect(next.clientSeq).toBe(2);
    expect(resumed.allOrdered()).toHaveLength(3);
  });

  it("reports an unfinished session when started but not submitted", () => {
    const queue = new EventQueue(filePath, new PlainTextStore());
    queue.append({ ...baseEvent, eventType: "assessment_started" });

    expect(queue.hasUnfinishedSession()).toBe(true);
  });

  it("does not report an unfinished session once submitted", () => {
    const queue = new EventQueue(filePath, new PlainTextStore());
    queue.append({ ...baseEvent, eventType: "assessment_started" });
    queue.append({ ...baseEvent, eventType: "assessment_submitted" });

    expect(queue.hasUnfinishedSession()).toBe(false);
  });

  it("starts fresh rather than crashing if the queue file is corrupt", () => {
    const corruptStore = new PlainTextStore();
    // Write genuinely undecodable bytes directly, bypassing the queue's own encrypt path.
    writeFileSync(filePath, Buffer.from([0xff, 0x00, 0xfe, 0x01]));

    const queue = new EventQueue(filePath, corruptStore);
    const appended = queue.append({ ...baseEvent, eventType: "assessment_started" });

    expect(appended.clientSeq).toBe(0);
  });

  it("persists events encrypted at rest via the injected SecretStore", () => {
    let encryptCalls = 0;
    const spyingStore = new (class {
      encrypt(plainText: string): Buffer {
        encryptCalls++;
        return Buffer.from(plainText, "utf-8");
      }

      decrypt(cipherText: Buffer): string {
        return cipherText.toString("utf-8");
      }
    })();

    const queue = new EventQueue(filePath, spyingStore);
    queue.append({ ...baseEvent, eventType: "assessment_started" });

    expect(encryptCalls).toBeGreaterThan(0);
  });
});
