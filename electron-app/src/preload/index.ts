import { contextBridge, ipcRenderer } from "electron";
import { recordEventRequestSchema, type IntegrityEvent, type RecordEventRequest } from "../shared/events";
import type { SessionInfo } from "../main/syncClient";

/**
 * The entire renderer-facing surface (docs/07 section 3): a tiny typed API through contextBridge, no
 * generic ipcRenderer exposure. Every outbound call is schema-validated here, before it ever reaches
 * the main process's own (equally strict) validation — defence in depth, not redundant.
 */
const api = {
  startSession: (): Promise<IntegrityEvent> => ipcRenderer.invoke("herproof:start-session"),

  recordEvent: (request: RecordEventRequest): Promise<{ ok: boolean }> => {
    const parsed = recordEventRequestSchema.parse(request);
    return ipcRenderer.invoke("herproof:record-event", parsed);
  },

  submit: (code: string): Promise<IntegrityEvent> => ipcRenderer.invoke("herproof:submit", code),

  getEvents: (): Promise<IntegrityEvent[]> => ipcRenderer.invoke("herproof:get-events"),

  getSessionInfo: (): Promise<SessionInfo | null> => ipcRenderer.invoke("herproof:get-session-info"),

  onEvent: (callback: (event: IntegrityEvent) => void): void => {
    ipcRenderer.on("herproof:event-recorded", (_ipcEvent, event: IntegrityEvent) => callback(event));
  },
};

export type HerProofBridge = typeof api;

contextBridge.exposeInMainWorld("herproof", api);
