import { describe, expect, test } from "vitest";

import type { Preset } from "../src/core.js";
import type { PresetRecord, PresetStore } from "../src/preset-service.js";
import { PresetService } from "../src/preset-service.js";

const preset: Preset = {
    schemaVersion: 1,
    id: "preset-1",
    name: "Co-op pack",
    gameBuildId: "latest-stable",
    loaderId: "bepinex-5.4.23.4",
    applyMode: "exact",
    entries: [{ id: "com.example.first", name: "First", version: "1.0.0", fileHashes: [] }],
};

class MemoryStore implements PresetStore {
    readonly records = new Map<string, PresetRecord>();
    readonly creationCounts = new Map<string, number>();
    touchCount = 0;

    async incrementCreateCount(ip: string): Promise<number> {
        const count = (this.creationCounts.get(ip) ?? 0) + 1;
        this.creationCounts.set(ip, count);
        return count;
    }

    async insert(code: string, record: PresetRecord): Promise<boolean> {
        if (this.records.has(code)) return false;
        this.records.set(code, record);
        return true;
    }

    async get(code: string): Promise<PresetRecord | null> {
        return this.records.get(code) ?? null;
    }

    async touch(code: string): Promise<void> {
        this.touchCount++;
    }

    async delete(code: string): Promise<void> {
        this.records.delete(code);
    }
}

describe("PresetService", () => {
    test("stores only the deletion-token hash and returns the raw token once", async () => {
        const store = new MemoryStore();
        const service = new PresetService(store, async () => ({ code: "A1B2C3D4E5F6", deleteToken: "token", deleteTokenHash: "hashed-token" }));

        const result = await service.create("192.0.2.7", preset);

        expect(result).toEqual({ code: "A1B2C3D4E5F6", deleteToken: "token" });
        expect(store.records.get(result.code)).toMatchObject({ deleteTokenHash: "hashed-token", preset });
        expect(JSON.stringify(store.records.get(result.code))).not.toContain("\"token\"");
    });

    test("extends a preset's 180-day lifetime only after a successful read", async () => {
        const store = new MemoryStore();
        store.records.set("A1B2C3D4E5F6", { preset, deleteTokenHash: "hash" });
        const service = new PresetService(store, async () => { throw new Error("not used"); });

        await expect(service.get("A1B2C3D4E5F6")).resolves.toEqual(preset);
        await expect(service.get("B1B2C3D4E5F6")).resolves.toBeNull();
        expect(store.touchCount).toBe(1);
    });

    test("enforces the tenth create request per IP in the current hour", async () => {
        const store = new MemoryStore();
        store.creationCounts.set("192.0.2.7", 10);
        const service = new PresetService(store, async () => ({ code: "A1B2C3D4E5F6", deleteToken: "token", deleteTokenHash: "hash" }));

        await expect(service.create("192.0.2.7", preset)).rejects.toThrow("请求过多");
    });

    test("deletes only when the supplied token matches the stored hash", async () => {
        const store = new MemoryStore();
        store.records.set("A1B2C3D4E5F6", { preset, deleteTokenHash: "correct" });
        const service = new PresetService(store, async () => { throw new Error("not used"); }, async token => token === "token" ? "correct" : "wrong");

        await expect(service.delete("A1B2C3D4E5F6", "wrong")).resolves.toBe(false);
        await expect(service.delete("A1B2C3D4E5F6", "token")).resolves.toBe(true);
        expect(store.records.has("A1B2C3D4E5F6")).toBe(false);
    });
});
