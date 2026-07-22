import { timingSafeEqual } from "node:crypto";

import { createDeleteTokenHash, hashDeleteToken, parsePreset, type Preset, type ShareSecrets } from "./core.js";

export const PRESET_TTL_SECONDS = 180 * 24 * 60 * 60;
export const MAX_CREATES_PER_HOUR = 10;

export interface PresetRecord {
    preset: Preset;
    deleteTokenHash: string;
}

export interface PresetStore {
    incrementCreateCount(ip: string): Promise<number>;
    insert(code: string, record: PresetRecord): Promise<boolean>;
    get(code: string): Promise<PresetRecord | null>;
    touch(code: string): Promise<void>;
    delete(code: string): Promise<void>;
}

export class PresetService {
    constructor(
        private readonly store: PresetStore,
        private readonly createSecrets: () => Promise<ShareSecrets> = createDeleteTokenHash,
        private readonly hashToken: (token: string) => Promise<string> = hashDeleteToken,
    ) {}

    async create(ip: string, input: unknown): Promise<{ code: string; deleteToken: string }> {
        const count = await this.store.incrementCreateCount(ip);
        if (count > MAX_CREATES_PER_HOUR) {
            throw new Error("请求过多，请在下一个小时后重试。");
        }

        const preset = parsePreset(input);
        for (let attempt = 0; attempt < 3; attempt++) {
            const secrets = await this.createSecrets();
            const inserted = await this.store.insert(secrets.code, {
                preset,
                deleteTokenHash: secrets.deleteTokenHash,
            });
            if (inserted) {
                return { code: secrets.code, deleteToken: secrets.deleteToken };
            }
        }
        throw new Error("暂时无法生成唯一分享码，请重试。");
    }

    async get(code: string): Promise<Preset | null> {
        const record = await this.store.get(code);
        if (record === null) {
            return null;
        }
        await this.store.touch(code);
        return record.preset;
    }

    async delete(code: string, deleteToken: string): Promise<boolean> {
        const record = await this.store.get(code);
        if (record === null) {
            return false;
        }

        const suppliedHash = await this.hashToken(deleteToken);
        if (!constantTimeEquals(record.deleteTokenHash, suppliedHash)) {
            return false;
        }
        await this.store.delete(code);
        return true;
    }
}

function constantTimeEquals(left: string, right: string): boolean {
    const leftBuffer = Buffer.from(left, "utf8");
    const rightBuffer = Buffer.from(right, "utf8");
    return leftBuffer.length === rightBuffer.length && timingSafeEqual(leftBuffer, rightBuffer);
}
