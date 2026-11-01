import { describe, expect, test, vi } from "vitest";

vi.mock("../src/redis-store.js", () => ({
    RedisPresetStore: class {
        constructor() { throw new Error("Storage unavailable"); }
    },
}));

import presets from "../api/presets.js";
import presetByCode from "../api/presets/[code].js";
import shareByCode from "../api/share/[code].js";

describe("Vercel Web handlers", () => {
    test("storage outage returns a readable retryable response without exposing infrastructure", async () => {
        const log = vi.spyOn(console, "error").mockImplementation(() => {});
        try {
            const responses = await Promise.all([
                presets.fetch(new Request("https://example.test/api/presets", {
                    method: "POST", headers: { "Content-Type": "application/json" }, body: "{}",
                })),
                presetByCode.fetch(new Request("https://example.test/api/presets/A1B2C3D4E5F6")),
                presetByCode.fetch(new Request("https://example.test/api/presets/A1B2C3D4E5F6", {
                    method: "DELETE", headers: { "X-Delete-Token": "A".repeat(43) },
                })),
                shareByCode.fetch(new Request("https://example.test/share/A1B2C3D4E5F6")),
            ]);
            for (const response of responses) {
                expect(response.status).toBe(503);
                const body = await response.text();
                expect(body).toContain("请稍后重试");
                expect(body).not.toContain("Storage unavailable");
                expect(response.headers.get("Cache-Control")).toBe("no-store");
            }
        } finally { log.mockRestore(); }
    });

    test("exports fetch handlers instead of legacy Node request handlers", async () => {
        expect(typeof presets.fetch).toBe("function");
        expect(typeof presetByCode.fetch).toBe("function");
        expect(typeof shareByCode.fetch).toBe("function");

        const options = await presets.fetch(new Request("https://example.test/api/presets", { method: "OPTIONS" }));
        const invalidCode = await presetByCode.fetch(new Request("https://example.test/api/presets/bad", { method: "GET" }));
        const invalidShare = await shareByCode.fetch(new Request("https://example.test/share/bad", { method: "GET" }));

        expect(options.status).toBe(204);
        expect(invalidCode.status).toBe(400);
        expect(invalidShare.status).toBe(404);
    });
});
