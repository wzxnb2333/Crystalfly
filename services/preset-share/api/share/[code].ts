import { SHARE_CODE_PATTERN, renderSharePage } from "../../src/core.js";
import { pathCode } from "../../src/http.js";
import { PresetService } from "../../src/preset-service.js";
import { RedisPresetStore } from "../../src/redis-store.js";

async function handler(request: Request): Promise<Response> {
    const code = pathCode(request);
    if (request.method !== "GET" || code === null || !SHARE_CODE_PATTERN.test(code)) {
        return new Response("Not found", { status: 404 });
    }

    const preset = await new PresetService(new RedisPresetStore()).get(code);
    if (preset === null) {
        return new Response("Not found", { status: 404 });
    }

    return new Response(renderSharePage(code, preset), {
        headers: {
            "Content-Type": "text/html; charset=utf-8",
            "Cache-Control": "no-store",
            "X-Content-Type-Options": "nosniff",
        },
    });
}

export default { fetch: handler };
