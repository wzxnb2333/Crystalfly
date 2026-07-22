# Crystalfly 预设分享服务

部署到 Vercel，并在 Vercel Marketplace 添加 Upstash Redis 集成。当前 Marketplace 集成会配置 `KV_REST_API_URL` 和 `KV_REST_API_TOKEN`；代码同时兼容 Upstash 原生的 `UPSTASH_REDIS_REST_URL` 与 `UPSTASH_REDIS_REST_TOKEN`。这些值只保存在 Vercel 项目环境变量或本地 `.env` 中。

```powershell
cd services/preset-share
npm install
npm test
npm run typecheck
vercel dev
```

接口：

- `POST /api/presets`：创建预设。请求体最大 128 KiB，每个 IP 每个 UTC 小时最多 10 次。响应中的 `deleteToken` 仅返回这一次。
- `GET /api/presets/{code}`：读取 12 位分享码对应的预设，并将其保存期续为 180 天。
- `DELETE /api/presets/{code}`：在 `X-Delete-Token` 请求头提供创建时返回的删除令牌。
- `GET /share/{code}`：提供只含名称、构建、Loader、Mod 数量和 `crystalfly://` 打开链接的分享页。

创建示例：

```json
{
  "schemaVersion": 1,
  "id": "coop-pack",
  "name": "Co-op pack",
  "gameBuildId": "latest-stable",
  "loaderId": "bepinex-5.4.23.4",
  "applyMode": "append",
  "entries": [
    {
      "id": "com.example.first",
      "name": "First",
      "version": "1.0.0",
      "fileHashes": []
    }
  ]
}
```

请求对象和每个 Mod 条目均执行字段白名单校验。服务不会使用账号或客户端密钥。浏览器 CORS 仅对 `crystalfly://` Origin 返回允许头。
