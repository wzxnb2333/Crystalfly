import { describe, expect, test } from "vitest";

import { corsHeaders, readJsonBody } from "../src/http.js";
import { MAX_BODY_BYTES } from "../src/core.js";

describe("HTTP helpers", () => {
    test("cancels an oversized streaming body without reading the remaining stream", async () => {
        let reads = 0;
        let canceled = false;
        const body = new ReadableStream<Uint8Array>({
            pull(controller) {
                reads++;
                if (reads === 1) {
                    controller.enqueue(new Uint8Array(MAX_BODY_BYTES + 1));
                } else {
                    controller.error(new Error("Oversized body was read past its limit"));
                }
            },
            cancel() {
                canceled = true;
            },
        }, { highWaterMark: 0 });
        const request = new Request("https://example.test", {
            method: "POST",
            headers: { "content-type": "application/json" },
            body,
            duplex: "half",
        } as RequestInit & { duplex: "half" });

        await expect(readJsonBody(request)).rejects.toMatchObject({ status: 413 });
        expect(reads).toBe(1);
        expect(canceled).toBe(true);
    });

    test("preserves multibyte text split across stream chunks", async () => {
        const bytes = new TextEncoder().encode(JSON.stringify({ name: "空洞骑士" }));
        const body = new ReadableStream<Uint8Array>({
            start(controller) {
                for (const byte of bytes) controller.enqueue(new Uint8Array([byte]));
                controller.close();
            },
        });
        const request = new Request("https://example.test", {
            method: "POST",
            headers: { "content-type": "application/json; charset=utf-8" },
            body,
            duplex: "half",
        } as RequestInit & { duplex: "half" });

        await expect(readJsonBody(request)).resolves.toEqual({ name: "空洞骑士" });
    });

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
