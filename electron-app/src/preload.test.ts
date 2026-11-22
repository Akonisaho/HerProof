import { describe, expect, it } from "vitest";
import { z } from "zod";

const integrityEventSchema = z.object({
  type: z.enum(["focus-lost", "focus-regained", "connection-lost", "connection-restored"]),
  occurredAt: z.string().datetime(),
});

describe("integrity event schema", () => {
  it("accepts a well-formed event", () => {
    const result = integrityEventSchema.safeParse({
      type: "focus-lost",
      occurredAt: new Date().toISOString(),
    });
    expect(result.success).toBe(true);
  });

  it("rejects an unknown event type", () => {
    const result = integrityEventSchema.safeParse({
      type: "screen-capture",
      occurredAt: new Date().toISOString(),
    });
    expect(result.success).toBe(false);
  });

  it("rejects a malformed timestamp", () => {
    const result = integrityEventSchema.safeParse({
      type: "focus-lost",
      occurredAt: "not-a-date",
    });
    expect(result.success).toBe(false);
  });
});
