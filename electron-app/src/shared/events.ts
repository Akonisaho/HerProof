import { z } from "zod";

/**
 * The complete, fixed event list (docs/07 section 5) — the same list shown to the candidate on the
 * consent screen before anything is recorded. Nothing outside this list may ever be queued: no screen
 * capture, no keystroke logging, no clipboard reads, no other-app inspection (CLAUDE.md rule 5).
 */
export const EVENT_TYPES = [
  "assessment_started",
  "assessment_paused",
  "assessment_resumed",
  "assessment_submitted",
  "focus_lost",
  "focus_regained",
  "window_closed",
  "camera_connected",
  "camera_disconnected",
  "microphone_connected",
  "microphone_disconnected",
  "network_lost",
  "network_restored",
  "session_interrupted",
  "snapshot_saved",
] as const;

export type EventType = (typeof EVENT_TYPES)[number];

export const integrityEventSchema = z.object({
  sessionId: z.uuid(),
  assessmentId: z.uuid(),
  candidateId: z.uuid(),
  clientSeq: z.number().int().nonnegative(),
  eventType: z.enum(EVENT_TYPES),
  occurredAt: z.string().datetime(),
  metadata: z.record(z.string(), z.unknown()).default({}),
});

export type IntegrityEvent = z.infer<typeof integrityEventSchema>;

/** What the renderer is allowed to ask the main process to record — everything else about the event
 * (sessionId, assessmentId, candidateId, clientSeq) is filled in by the main process, which is the
 * only part of the app that holds that session context. This keeps the renderer's IPC surface small
 * and stops it from being able to forge another session's events. */
export const recordEventRequestSchema = z.object({
  eventType: z.enum(EVENT_TYPES),
  metadata: z.record(z.string(), z.unknown()).optional(),
});

export type RecordEventRequest = z.infer<typeof recordEventRequestSchema>;
