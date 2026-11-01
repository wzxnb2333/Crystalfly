import { createHash, randomBytes } from "node:crypto";

export const MAX_BODY_BYTES = 128 * 1024;
export const MAX_ENTRIES = 1000;
export const SHARE_CODE_PATTERN = /^[A-Za-z0-9_-]{12}$/;

const presetKeys = ["schemaVersion", "id", "name", "gameBuildId", "loaderId", "applyMode", "entries"];
const entryKeys = ["id", "name", "version", "fileHashes"];
const sha256Pattern = /^[A-Fa-f0-9]{64}$/;

export interface PresetEntry {
    id: string | null;
    name: string;
    version: string | null;
    fileHashes: string[];
}

export interface Preset {
    gameId?: "hollowKnight";
    schemaVersion: 1;
    id: string;
    name: string;
    gameBuildId: string;
    loaderId: string;
    applyMode: "append" | "exact";
    entries: PresetEntry[];
}

export interface SilksongPreset {
    name: string;
    gameId: "silksong";
    buildId: string;
    packages: { fullName: string; version: string; enabled: boolean; pinned: boolean }[];
}

export type SharedPreset = Preset | SilksongPreset;

export interface ShareSecrets {
    code: string;
    deleteToken: string;
    deleteTokenHash: string;
}

export class PresetValidationError extends Error {
    constructor(message: string) {
        super(message);
        this.name = "PresetValidationError";
    }
}

export function parsePreset(value: unknown): SharedPreset {
    try {
        return parsePresetCore(value);
    } catch (error) {
        if (error instanceof PresetValidationError) {
            throw error;
        }
        throw new PresetValidationError(error instanceof Error ? error.message : "预设格式无效。");
    }
}

function parsePresetCore(value: unknown): SharedPreset {
    const record = requireObject(value, "预设");
    if (record.gameId === "silksong") return parseSilksongPreset(record);
    requireExactKeys(record, presetKeys, "预设", ["gameId"]);
    if (record.gameId !== undefined && record.gameId !== "hollowKnight") throw new Error("gameId 无效。");

    if (record.schemaVersion !== 1) {
        throw new Error("schemaVersion 必须为 1。");
    }
    const entries = record.entries;
    if (!Array.isArray(entries)) {
        throw new Error("entries 必须是数组。");
    }
    if (entries.length > MAX_ENTRIES) {
        throw new Error(`entries 最多允许 ${MAX_ENTRIES} 条目。`);
    }

    const parsed: Preset = {
        ...(record.gameId === undefined ? {} : { gameId: "hollowKnight" as const }),
        schemaVersion: 1,
        id: requireText(record.id, "id", 160),
        name: requireText(record.name, "name", 120),
        gameBuildId: requireText(record.gameBuildId, "gameBuildId", 160),
        loaderId: requireText(record.loaderId, "loaderId", 160),
        applyMode: parseApplyMode(record.applyMode),
        entries: entries.map((entry, index) => parseEntry(entry, index)),
    };
    ensureUniqueEntries(parsed.entries);
    return parsed;
}

function parseSilksongPreset(record: Record<string, unknown>): SilksongPreset {
    requireExactKeys(record, ["name", "gameId", "buildId", "packages"], "丝之歌整合包");
    if (!Array.isArray(record.packages) || record.packages.length > MAX_ENTRIES)
        throw new Error(`packages 必须是数组，最多 ${MAX_ENTRIES} 条目。`);
    const names = new Set<string>();
    const packages = record.packages.map((value, index) => {
        const item = requireObject(value, `packages[${index}]`);
        requireExactKeys(item, ["fullName", "version", "enabled", "pinned"], `packages[${index}]`);
        const fullName = requireText(item.fullName, "fullName", 200);
        const version = requireText(item.version, "version", 128);
        if (!/^[A-Za-z0-9_-]+$/.test(fullName) || !/^\d+\.\d+\.\d+$/.test(version)
            || version.split(".").some(part => Number(part) > 2147483647)
            || typeof item.enabled !== "boolean" || typeof item.pinned !== "boolean")
            throw new Error(`packages[${index}] 的包名、版本或状态无效。`);
        if (names.has(fullName.toLowerCase())) throw new Error(`整合包包含重复条目：${fullName}。`);
        names.add(fullName.toLowerCase());
        return { fullName, version, enabled: item.enabled, pinned: item.pinned };
    });
    return { name: requireText(record.name, "name", 120), gameId: "silksong",
        buildId: requireText(record.buildId, "buildId", 160), packages };
}

export async function createDeleteTokenHash(): Promise<ShareSecrets> {
    const code = randomBytes(9).toString("base64url");
    const deleteToken = randomBytes(32).toString("base64url");
    return { code, deleteToken, deleteTokenHash: await hashDeleteToken(deleteToken) };
}

export async function hashDeleteToken(deleteToken: string): Promise<string> {
    return createHash("sha256").update(deleteToken, "utf8").digest("hex");
}

export function renderSharePage(code: string, preset: SharedPreset): string {
    if (!SHARE_CODE_PATTERN.test(code)) {
        throw new Error("分享码格式无效。");
    }

    const name = escapeHtml(preset.name);
    const silksong = preset.gameId === "silksong";
    const build = escapeHtml(silksong ? preset.buildId : preset.gameBuildId);
    const loader = silksong ? "BepInEx" : escapeHtml(preset.loaderId);
    const count = silksong ? preset.packages.length : preset.entries.length;
    const modLabel = `${count} 个 Mod`;

    return `<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
  <title>${name} · Crystalfly 整合包</title>
  <style>body{font-family:system-ui,sans-serif;background:#10141d;color:#eef3fb;max-width:42rem;margin:8vh auto;padding:0 1.5rem}main{background:#1b2230;border:1px solid #33415a;border-radius:1rem;padding:2rem}dl{display:grid;grid-template-columns:max-content 1fr;gap:.7rem 1.25rem}dt{color:#aab6ca}dd{margin:0;overflow-wrap:anywhere}a{display:inline-block;margin-top:1.5rem;padding:.7rem 1rem;border-radius:.5rem;background:#6ea8fe;color:#07101f;font-weight:700;text-decoration:none}</style>
</head>
<body><main>
  <h1>${name}</h1>
  <dl><dt>游戏</dt><dd>${silksong ? "Hollow Knight: Silksong" : "Hollow Knight"}</dd><dt>游戏版本</dt><dd>${build}</dd><dt>Loader</dt><dd>${loader}</dd><dt>Mod</dt><dd>${modLabel}</dd><dt>分享码</dt><dd><code style="user-select:all">${code}</code></dd></dl>
  <a href="crystalfly://modpack?code=${code}">在 Crystalfly 中打开</a>
  <p>先在 Crystalfly 中选择对应游戏的目标实例，再打开链接；也可在整合包页面粘贴分享码或本页地址。</p>
</main></body>
</html>`;
}

function parseEntry(value: unknown, index: number): PresetEntry {
    const record = requireObject(value, `entries[${index}]`);
    requireExactKeys(record, entryKeys, `entries[${index}]`);
    if (!Array.isArray(record.fileHashes)
        || record.fileHashes.some(hash => typeof hash !== "string" || !sha256Pattern.test(hash))) {
        throw new Error(`entries[${index}].fileHashes 必须只包含 SHA-256 哈希。`);
    }
    const id = record.id === null ? null : requireText(record.id, `entries[${index}].id`, 256);
    const version = record.version === null
        ? null
        : requireText(record.version, `entries[${index}].version`, 128);
    if ((id === null) !== (version === null)) {
        throw new Error(`entries[${index}] 的 id 与 version 必须同时为空或同时存在。`);
    }
    if (id === null && record.fileHashes.length === 0) {
        throw new Error(`entries[${index}] 的本地 Mod 必须包含文件哈希。`);
    }
    return {
        id,
        name: requireText(record.name, `entries[${index}].name`, 256),
        version,
        fileHashes: [...record.fileHashes].sort((left, right) => left.localeCompare(right)),
    };
}

function parseApplyMode(value: unknown): "append" | "exact" {
    if (value !== "append" && value !== "exact") {
        throw new Error("applyMode 必须为 append 或 exact。");
    }
    return value;
}

function ensureUniqueEntries(entries: PresetEntry[]): void {
    const keys = new Set<string>();
    for (const entry of entries) {
        const key = entry.id === null
            ? `local:${entry.name}:${entry.fileHashes.join(":")}`
            : `managed:${entry.id.toLowerCase()}`;
        if (keys.has(key)) {
            throw new Error(`预设包含重复条目：${entry.name}。`);
        }
        keys.add(key);
    }
}

function requireObject(value: unknown, name: string): Record<string, unknown> {
    if (value === null || typeof value !== "object" || Array.isArray(value)) {
        throw new Error(`${name} 必须是对象。`);
    }
    return value as Record<string, unknown>;
}

function requireExactKeys(value: Record<string, unknown>, keys: string[], name: string, optionalKeys: string[] = []): void {
    for (const key of Object.keys(value)) {
        if (!keys.includes(key) && !optionalKeys.includes(key)) {
            throw new Error(`${name} 包含未知字段：${key}。`);
        }
    }
    for (const key of keys) {
        if (!(key in value)) {
            throw new Error(`${name} 缺少字段：${key}。`);
        }
    }
}

function requireText(value: unknown, name: string, maximumLength: number): string {
    if (typeof value !== "string") {
        throw new Error(`${name} 必须是字符串。`);
    }
    const text = value.trim();
    if (text.length === 0 || text.length > maximumLength || /[\u0000-\u001f\u007f]/.test(text)) {
        throw new Error(`${name} 长度或字符无效。`);
    }
    return text;
}

function escapeHtml(value: string): string {
    return value.replace(/[&<>"']/g, character => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        "\"": "&quot;",
        "'": "&#39;",
    })[character] ?? character);
}
