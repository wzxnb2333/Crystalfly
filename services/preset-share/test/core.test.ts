import { describe, expect, test } from "vitest";

import {
    MAX_ENTRIES,
    PresetValidationError,
    SHARE_CODE_PATTERN,
    createDeleteTokenHash,
    parsePreset,
    renderSharePage,
    type Preset,
} from "../src/core.js";

const preset: Preset = {
    schemaVersion: 1,
    id: "preset-1",
    name: "Co-op pack",
    gameBuildId: "latest-stable",
    loaderId: "bepinex-5.4.23.4",
    applyMode: "append",
    entries: [
        { id: "com.example.first", name: "First", version: "1.0.0", fileHashes: [] },
        { id: null, name: "Local helper", version: null, fileHashes: ["A".repeat(64)] },
    ],
};

describe("parsePreset", () => {
    test("accepts current Hollow Knight gameId and keeps legacy documents valid", () => {
        expect(parsePreset({ ...preset, gameId: "hollowKnight" })).toEqual({ ...preset, gameId: "hollowKnight" });
        expect(() => parsePreset({ ...preset, gameId: "other" })).toThrow(PresetValidationError);
    });

    test("round-trips Silksong package versions, enabled and pinned states", () => {
        const silksong = { name: "丝之歌练习", gameId: "silksong", buildId: "1.0.30000",
            packages: [{ fullName: "Author-Practice", version: "1.2.3", enabled: false, pinned: true }] };
        expect(parsePreset(silksong)).toEqual(silksong);
        for (const invalid of [
            { ...silksong, loaderId: "unexpected" },
            { ...silksong, packages: [{ ...silksong.packages[0], enabled: "false" }] },
            { ...silksong, packages: [{ ...silksong.packages[0], version: "../file" }] },
            { ...silksong, packages: [silksong.packages[0], silksong.packages[0]] },
            { ...silksong, packages: Array(MAX_ENTRIES + 1).fill(silksong.packages[0]) },
        ]) expect(() => parsePreset(invalid)).toThrow(PresetValidationError);
    });
    test("accepts a complete preset", () => {
        expect(parsePreset(preset)).toEqual(preset);
    });

    test("rejects unknown fields and invalid mod entries", () => {
        expect(() => parsePreset({ ...preset, extra: true })).toThrow("未知字段");
        expect(() => parsePreset({
            ...preset,
            entries: [{ id: "mod", name: "Mod", version: "1", fileHashes: [], enabled: true }],
        })).toThrow("未知字段");
    });

    test("limits a preset to 1000 mod entries", () => {
        expect(() => parsePreset({
            ...preset,
            entries: Array.from({ length: MAX_ENTRIES + 1 }, (_, index) => ({
                id: `mod-${index}`,
                name: `Mod ${index}`,
                version: "1",
                fileHashes: [],
            })),
        })).toThrow("1000");
    });

    test("rejects local entries without a sha-256 hash and rejects package locations", () => {
        expect(() => parsePreset({
            ...preset,
            entries: [{ id: null, name: "Local", version: null, fileHashes: [] }],
        })).toThrow("哈希");
        expect(() => parsePreset({ ...preset, packageUrl: "https://example.test/mod.zip" })).toThrow("未知字段");
    });

    test("classifies every invalid preset as a client validation error", () => {
        expect(() => parsePreset({ ...preset, applyMode: "invalid" })).toThrow(PresetValidationError);
        expect(() => parsePreset({ ...preset, schemaVersion: 2 })).toThrow(PresetValidationError);
        expect(() => parsePreset({ ...preset, entries: [{ id: "mod", name: "Mod", version: "1", fileHashes: ["bad"] }] }))
            .toThrow(PresetValidationError);
    });
});

describe("share secrets", () => {
    test("creates a 12-character URL-safe share code and does not retain the raw deletion token", async () => {
        const { code, deleteToken } = await createDeleteTokenHash();

        expect(code).toMatch(SHARE_CODE_PATTERN);
        expect(code).toHaveLength(12);
        expect(deleteToken).toMatch(/^[A-Za-z0-9_-]{43}$/);
    });
});

describe("renderSharePage", () => {
    test("identifies Silksong and exposes the share code without adding executable metadata", () => {
        const html = renderSharePage("A1B2C3D4E5F6", { name: "Silk pack", gameId: "silksong",
            buildId: "1.0.30000", packages: [] });
        expect(html).toContain("Hollow Knight: Silksong");
        expect(html).toContain("1.0.30000");
        expect(html).toContain("分享码");
        expect(html).toContain("A1B2C3D4E5F6");
    });
    test("escapes preset metadata and links the native client to the share code", () => {
        const html = renderSharePage("A1B2C3D4E5F6", { ...preset, name: "<script>alert(1)</script>" });

        expect(html).toContain("&lt;script&gt;alert(1)&lt;/script&gt;");
        expect(html).not.toContain("<script>alert(1)</script>");
        expect(html).toContain('href="crystalfly://modpack?code=A1B2C3D4E5F6"');
        expect(html).toContain("2 个 Mod");
    });
});
