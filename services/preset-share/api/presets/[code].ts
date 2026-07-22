import { SHARE_CODE_PATTERN } from "../../src/core.js";
import { empty, json, pathCode } from "../../src/http.js";
import { PresetService } from "../../src/preset-service.js";
import { RedisPresetStore } from "../../src/redis-store.js";

async function handler(request: Request): Promise<Response> {
    if (request.method === "OPTIONS") {
        return empty(request, 204);
    }

    const code = pathCode(request);
    if (code === null || !SHARE_CODE_PATTERN.test(code)) {
        return json(request, 400, { error: "分享码格式无效。" });
    }

    const service = new PresetService(new RedisPresetStore());
    if (request.method === "GET") {
        const preset = await service.get(code);
        return preset === null ? json(request, 404, { error: "分享预设不存在或已过期。" }) : json(request, 200, { code, preset });
    }
    if (request.method === "DELETE") {
        const deleteToken = request.headers.get("x-delete-token") ?? "";
        if (!/^[A-Za-z0-9_-]{43}$/.test(deleteToken)) {
            return json(request, 400, { error: "删除令牌格式无效。" });
        }
        return await service.delete(code, deleteToken) ? empty(request, 204) : json(request, 404, { error: "分享预设不存在或删除令牌无效。" });
    }
    return json(request, 405, { error: "仅支持 GET 或 DELETE。" });
}

export default { fetch: handler };
