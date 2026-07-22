import { RequestBodyError, clientIp, empty, json, readJsonBody } from "../src/http.js";
import { PresetValidationError } from "../src/core.js";
import { PresetService } from "../src/preset-service.js";
import { RedisPresetStore } from "../src/redis-store.js";

async function handler(request: Request): Promise<Response> {
    if (request.method === "OPTIONS") {
        return empty(request, 204);
    }
    if (request.method !== "POST") {
        return json(request, 405, { error: "仅支持 POST。" });
    }

    try {
        const input = await readJsonBody(request);
        const result = await new PresetService(new RedisPresetStore()).create(clientIp(request), input);
        return json(request, 201, result);
    } catch (error) {
        return handleError(request, error);
    }
}

export default { fetch: handler };

function handleError(request: Request, error: unknown): Response {
    if (error instanceof RequestBodyError) {
        return json(request, error.status, { error: error.message });
    }
    if (error instanceof Error && error.message.startsWith("请求过多")) {
        return json(request, 429, { error: error.message });
    }
    if (error instanceof PresetValidationError) {
        return json(request, 400, { error: error.message });
    }
    console.error(error);
    return json(request, 500, { error: "服务暂时不可用。" });
}
