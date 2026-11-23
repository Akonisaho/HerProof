import { describe, expect, it } from "vitest";
import { integrityEventSchema, recordEventRequestSchema } from "./events";

describe("integrityEventSchema", () => {
  const valid = {
    sessionId: "11111111-1111-4111-8111-111111111111",
    assessmentId: "22222222-2222-4222-8222-222222222222",
    candidateId: "33333333-3333-4333-8333-333333333333",
    clientSeq: 0,
    eventType: "focus_lost",
    occurredAt: new Date().toISOString(),
    metadata: {},
  };

  it("accepts a well-formed event", () => {
    expect(integrityEventSchema.safeParse(valid).success).toBe(true);
  });

  it("rejects an event type outside the fixed, documented list", () => {
    // This is the actual enforcement of "no screen capture, no keystroke logging, no clipboard reads"
    // (CLAUDE.md rule 5) at the schema level — only the documented event catalogue can ever exist.
    const result = integrityEventSchema.safeParse({ ...valid, eventType: "screen_capture" });
    expect(result.success).toBe(false);
  });

  it("rejects a negative client_seq", () => {
    expect(integrityEventSchema.safeParse({ ...valid, clientSeq: -1 }).success).toBe(false);
  });

  it("rejects a malformed timestamp", () => {
    expect(integrityEventSchema.safeParse({ ...valid, occurredAt: "not-a-date" }).success).toBe(false);
  });

  it("rejects a non-uuid session id", () => {
    expect(integrityEventSchema.safeParse({ ...valid, sessionId: "not-a-uuid" }).success).toBe(false);
  });

  it("defaults metadata to an empty object when omitted", () => {
    const { metadata: _metadata, ...withoutMetadata } = valid;
    const result = integrityEventSchema.safeParse(withoutMetadata);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.metadata).toEqual({});
    }
  });
});

describe("recordEventRequestSchema", () => {
  it("accepts a minimal valid request", () => {
    expect(recordEventRequestSchema.safeParse({ eventType: "snapshot_saved" }).success).toBe(true);
  });

  it("rejects an unknown event type", () => {
    expect(recordEventRequestSchema.safeParse({ eventType: "keystroke_logged" }).success).toBe(false);
  });

  it("rejects a request with no event type at all", () => {
    expect(recordEventRequestSchema.safeParse({}).success).toBe(false);
  });
});
