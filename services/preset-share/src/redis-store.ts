import { Redis } from "@upstash/redis";

import { PRESET_TTL_SECONDS, type PresetRecord, type PresetStore } from "./preset-service.js";

export class RedisPresetStore implements PresetStore {
    private readonly redis: Redis;

    constructor(
        url = process.env.KV_REST_API_URL ?? process.env.UPSTASH_REDIS_REST_URL,
        token = process.env.KV_REST_API_TOKEN ?? process.env.UPSTASH_REDIS_REST_TOKEN,
    ) {
        if (!url || !token) {
            throw new Error("未配置 Upstash Redis 环境变量。");
        }
        this.redis = new Redis({ url, token });
    }

    async incrementCreateCount(ip: string): Promise<number> {
        const now = Date.now();
        const hour = Math.floor(now / 3_600_000);
        const secondsUntilNextHour = Math.max(1, Math.ceil(((hour + 1) * 3_600_000 - now) / 1000));
        const key = `preset-share:rate:${hour}:${ip}`;
        const count = await this.redis.incr(key);
        await this.redis.expire(key, secondsUntilNextHour);
        return count;
    }

    async insert(code: string, record: PresetRecord): Promise<boolean> {
        const result = await this.redis.set(this.presetKey(code), record, { ex: PRESET_TTL_SECONDS, nx: true });
        return result !== null;
    }

    async get(code: string): Promise<PresetRecord | null> {
        return await this.redis.get<PresetRecord>(this.presetKey(code));
    }

    async touch(code: string): Promise<void> {
        await this.redis.expire(this.presetKey(code), PRESET_TTL_SECONDS);
    }

    async delete(code: string): Promise<void> {
        await this.redis.del(this.presetKey(code));
    }

    private presetKey(code: string): string {
        return `preset-share:preset:${code}`;
    }
}
