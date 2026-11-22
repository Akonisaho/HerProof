import { contextBridge, ipcRenderer } from "electron";
import { z } from "zod";

// STUB(HP-012): full assessment IPC surface (fetch assessment, submit, sync events) comes with the
// complete Electron build in docs/05 Phase 3. This exposes only what the consent-screen skeleton needs.

const integrityEventSchema = z.object({
  type: z.enum(["focus-lost", "focus-regained", "connection-lost", "connection-restored"]),
  occurredAt: z.string().datetime(),
});

export type IntegrityEvent = z.infer<typeof integrityEventSchema>;

const api = {
  reportIntegrityEvent: (event: IntegrityEvent): void => {
    const parsed = integrityEventSchema.parse(event);
    ipcRenderer.send("integrity-event", parsed);
  },
};

export type HerProofBridge = typeof api;

contextBridge.exposeInMainWorld("herproof", api);
