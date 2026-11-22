import { app, BrowserWindow, ipcMain, session } from "electron";
import path from "node:path";
import { z } from "zod";

const integrityEventSchema = z.object({
  type: z.enum(["focus-lost", "focus-regained", "connection-lost", "connection-restored"]),
  occurredAt: z.string().datetime(),
});

ipcMain.on("integrity-event", (_event, payload: unknown) => {
  const result = integrityEventSchema.safeParse(payload);
  if (!result.success) {
    return;
  }
  // STUB(HP-013): persist to the local encrypted event queue and batch-sync to the Web API.
});

function createMainWindow(): BrowserWindow {
  const window = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      webSecurity: true,
    },
  });

  window.webContents.setWindowOpenHandler(() => ({ action: "deny" }));
  window.webContents.on("will-navigate", (event, url) => {
    if (!url.startsWith("file://")) {
      event.preventDefault();
    }
  });

  void window.loadFile(path.join(__dirname, "..", "renderer", "index.html"));
  return window;
}

app.whenReady().then(() => {
  session.defaultSession.webRequest.onHeadersReceived((details, callback) => {
    callback({
      responseHeaders: {
        ...details.responseHeaders,
        "Content-Security-Policy": ["default-src 'self'"],
      },
    });
  });

  createMainWindow();

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createMainWindow();
    }
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
