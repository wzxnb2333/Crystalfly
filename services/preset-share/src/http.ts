import { MAX_BODY_BYTES } from "./core.js";

const crystalflyOrigin = "crystalfly://";

export class RequestBodyError extends Error {
    constructor(message: string, readonly status: number) {
        super(message);
    }
}

export function corsHeaders(request: Request): Headers {
    const headers = new Headers({
        "Access-Control-Allow-Methods": "POST, GET, DELETE, OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type, X-Delete-Token",
        "Access-Control-Max-Age": "86400",
        "Vary": "Origin",
    });
    if (request.headers.get("origin") === crystalflyOrigin) {
        headers.set("Access-Control-Allow-Origin", crystalflyOrigin);
    }
    return headers;
}

export async function readJsonBody(request: Request): Promise<unknown> {
    const contentType = request.headers.get("content-type") ?? "";
    if (!contentType.toLowerCase().startsWith("application/json")) {
        throw new RequestBodyError("Content-Type 必须为 application/json。", 415);
    }

    const declaredLength = request.headers.get("content-length");
    if (declaredLength !== null && (!/^\d+$/.test(declaredLength) || Number(declaredLength) > MAX_BODY_BYTES)) {
        throw new RequestBodyError("请求体不能超过 128 KiB。", 413);
    }

    const text = await request.text();
    if (Buffer.byteLength(text, "utf8") > MAX_BODY_BYTES) {
        throw new RequestBodyError("请求体不能超过 128 KiB。", 413);
    }
    try {
        return JSON.parse(text) as unknown;
    } catch {
        throw new RequestBodyError("请求体不是有效 JSON。", 400);
    }
}

export function json(request: Request, status: number, body: unknown): Response {
    const headers = corsHeaders(request);
    headers.set("Content-Type", "application/json; charset=utf-8");
    headers.set("Cache-Control", "no-store");
    return new Response(JSON.stringify(body), { status, headers });
}

export function empty(request: Request, status: number): Response {
    const headers = corsHeaders(request);
    headers.set("Cache-Control", "no-store");
    return new Response(null, { status, headers });
}

export function clientIp(request: Request): string {
    const value = request.headers.get("x-forwarded-for")?.split(",", 1)[0]?.trim();
    return value && value.length <= 64 ? value : "unknown";
}

export function pathCode(request: Request): string | null {
    const segment = new URL(request.url).pathname.split("/").at(-1);
    if (!segment || !/^[A-Za-z0-9_-]{12}$/.test(segment)) {
        return null;
    }
    return segment;
}
