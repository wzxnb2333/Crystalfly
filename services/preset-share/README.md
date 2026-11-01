# Crystalfly 预设分享服务

部署到 Vercel，并在 Vercel Marketplace 添加 Upstash Redis 集成。当前 Marketplace 集成会配置 `KV_REST_API_URL` 和 `KV_REST_API_TOKEN`；代码同时兼容 Upstash 原生的 `UPSTASH_REDIS_REST_URL` 与 `UPSTASH_REDIS_REST_TOKEN`。这些值只保存在 Vercel 项目环境变量或本地 `.env` 中。

生产地址：`https://crystalflyapi.jwst233.top/`。

当前生产与预览连接 `crystalfly-preset-share-v2` 免费库，使用 `CRYSTALFLY_KV_REST_API_URL` / `CRYSTALFLY_KV_REST_API_TOKEN`，优先于旧库变量。旧连接保留供回滚参考，不再读取旧库数据。

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
- `GET /share/{code}`：提供名称、游戏、版本、Loader、Mod 数量、分享码和 `crystalfly://` 打开链接的分享页。

支持 Hollow Knight 与 Silksong。客户端整合包页面均可粘贴 12 位分享码、本服务的 HTTPS 分享页地址或 `crystalfly://modpack?code=...` 链接。选择对应游戏的目标实例后导入；跨游戏整合包会被拒绝。分享链接用于一次导入，不会自动订阅或更新模组；下载安装仍需用户确认。

Hollow Knight 使用下方格式，允许 `gameId: "hollowKnight"`，并兼容没有 `gameId` 的旧分享数据。Silksong 使用现有 JSON 导出格式，保留包版本、启用与固定状态：

```json
{
  "name": "Practice",
  "gameId": "silksong",
  "buildId": "1.0.30000",
  "packages": [
    { "fullName": "Author-Practice", "version": "1.2.3", "enabled": false, "pinned": true }
  ]
}
```

两种格式均限制最多 1000 个条目。Upstash 免费库可能因不活跃而归档；遇到存储不可用需从 Upstash 控制台恢复并重新连接 Vercel，重新部署不会恢复已归档的数据库。

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
