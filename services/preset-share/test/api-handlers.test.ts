import { describe, expect, test } from "vitest";

import presets from "../api/presets.js";
import presetByCode from "../api/presets/[code].js";
import shareByCode from "../api/share/[code].js";

describe("Vercel Web handlers", () => {
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
