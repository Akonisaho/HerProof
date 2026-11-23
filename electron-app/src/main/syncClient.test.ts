import { describe, expect, it, vi } from "vitest";
import { SyncClient } from "./syncClient";
import type { IntegrityEvent } from "../shared/events";

const sampleEvent: IntegrityEvent = {
  sessionId: "11111111-1111-4111-8111-111111111111",
  assessmentId: "22222222-2222-4222-8222-222222222222",
  candidateId: "33333333-3333-4333-8333-333333333333",
  clientSeq: 0,
  eventType: "focus_lost",
  occurredAt: new Date().toISOString(),
  metadata: {},
};

describe("SyncClient", () => {
  it("returns no accepted events when there is no access token (not yet signed in)", async () => {
    const client = new SyncClient("https://api.example.test", () => null);

    const result = await client.syncBatch("session-1", [sampleEvent]);

    expect(result.acceptedClientSeqs).toEqual([]);
  });

  it("posts the batch with a bearer token and returns the accepted sequence numbers", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ acceptedClientSeqs: [0] }),
    });

    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    const result = await client.syncBatch("session-1", [sampleEvent]);

    expect(result.acceptedClientSeqs).toEqual([0]);
    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.example.test/api/assessment-sessions/session-1/events",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({ Authorization: "Bearer token-123" }),
      }),
    );
  });

  it("never throws when the network call fails — offline is routine, not an error", async () => {
    const fetchMock = vi.fn().mockRejectedValue(new Error("network down"));
    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    const result = await client.syncBatch("session-1", [sampleEvent]);

    expect(result.acceptedClientSeqs).toEqual([]);
  });

  it("returns no accepted events when the server responds with an error status", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: false, json: async () => ({}) });
    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    const result = await client.syncBatch("session-1", [sampleEvent]);

    expect(result.acceptedClientSeqs).toEqual([]);
  });

  it("does not call fetch at all for an empty batch", async () => {
    const fetchMock = vi.fn();
    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    await client.syncBatch("session-1", []);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("fetchSessionInfo returns the server's authoritative start time and duration", async () => {
    const info = { status: "InProgress", startedAtUtc: "2026-11-23T10:00:00Z", durationMinutes: 60, serverTimeUtc: "2026-11-23T10:05:00Z" };
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => info });
    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    const result = await client.fetchSessionInfo("session-1");

    expect(result).toEqual(info);
    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.example.test/api/assessment-sessions/session-1/info",
      expect.objectContaining({ headers: expect.objectContaining({ Authorization: "Bearer token-123" }) }),
    );
  });

  it("fetchSessionInfo returns null without a token rather than guessing a deadline", async () => {
    const client = new SyncClient("https://api.example.test", () => null);

    const result = await client.fetchSessionInfo("session-1");

    expect(result).toBeNull();
  });

  it("fetchSessionInfo returns null on a network failure", async () => {
    const fetchMock = vi.fn().mockRejectedValue(new Error("network down"));
    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    const result = await client.fetchSessionInfo("session-1");

    expect(result).toBeNull();
  });

  it("submitCode posts the code with a bearer token and returns true on success", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true });
    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    const result = await client.submitCode("session-1", "// my solution");

    expect(result).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.example.test/api/assessment-sessions/session-1/code",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({ Authorization: "Bearer token-123" }),
        body: JSON.stringify({ code: "// my solution" }),
      }),
    );
  });

  it("submitCode returns false without a token", async () => {
    const client = new SyncClient("https://api.example.test", () => null);

    const result = await client.submitCode("session-1", "// my solution");

    expect(result).toBe(false);
  });

  it("submitCode returns false on a network failure rather than throwing", async () => {
    const fetchMock = vi.fn().mockRejectedValue(new Error("network down"));
    const client = new SyncClient("https://api.example.test", () => "token-123", fetchMock as unknown as typeof fetch);

    const result = await client.submitCode("session-1", "// my solution");

    expect(result).toBe(false);
  });
});
