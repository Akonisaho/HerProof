import { safeStorage } from "electron";
import type { SecretStore } from "../shared/secretStore";
import { PlainTextStore } from "../shared/secretStore";

/** Real at-rest encryption via the OS keychain (docs/07 section 6). Falls back to plain text only if
 * the OS keychain genuinely isn't available (e.g. some headless Linux CI images) — logged loudly so
 * that's never a silent downgrade. */
export class ElectronSecretStore implements SecretStore {
  private readonly fallback = new PlainTextStore();

  encrypt(plainText: string): Buffer {
    if (!safeStorage.isEncryptionAvailable()) {
      console.warn("safeStorage encryption is unavailable on this platform; falling back to plain text.");
      return this.fallback.encrypt(plainText);
    }

    return safeStorage.encryptString(plainText);
  }

  decrypt(cipherText: Buffer): string {
    if (!safeStorage.isEncryptionAvailable()) {
      return this.fallback.decrypt(cipherText);
    }

    return safeStorage.decryptString(cipherText);
  }
}
