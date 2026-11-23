/** Pluggable at-rest encryption so EventQueue's logic is unit-testable outside a running Electron
 * process (Electron's `safeStorage` needs the real runtime and OS keychain) while still being
 * genuinely encrypted in the packaged app (docs/07 section 6: "local encrypted store"). */
export interface SecretStore {
  encrypt(plainText: string): Buffer;
  decrypt(cipherText: Buffer): string;
}

/** Used only in tests and as a last-resort fallback if the OS keychain is unavailable. Never the
 * default in the packaged app. */
export class PlainTextStore implements SecretStore {
  encrypt(plainText: string): Buffer {
    return Buffer.from(plainText, "utf-8");
  }

  decrypt(cipherText: Buffer): string {
    return cipherText.toString("utf-8");
  }
}
