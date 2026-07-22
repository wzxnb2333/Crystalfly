import { describe, expect, test } from "vitest";

import { corsHeaders, readJsonBody } from "../src/http.js";

describe("HTTP helpers", () => {
    test("permits CORS only for the Crystalfly custom origin", () => {
        const allowed = corsHeaders(new Request("https://example.test", { headers: { origin: "crystalfly://" } }));
        const rejected = corsHeaders(new Request("https://example.test", { headers: { origin: "https://example.test" } }));

        expect(allowed.get("Access-Control-Allow-Origin")).toBe("crystalfly://");
        expect(allowed.get("Access-Control-Allow-Methods")).toBe("POST, GET, DELETE, OPTIONS");
        expect(rejected.has("Access-Control-Allow-Origin")).toBe(false);
    });

    test("rejects JSON bodies larger than 128 KiB before parsing", async () => {
        const request = new Request("https://example.test", {
            method: "POST",
            headers: { "content-type": "application/json", "content-length": "131073" },
            body: "{}",
        });

        await expect(readJsonBody(request)).rejects.toThrow("128 KiB");
    });
});
