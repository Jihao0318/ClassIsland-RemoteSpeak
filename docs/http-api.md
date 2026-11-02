# HTTP 接口

插件在本机开一个 HTTP 服务，默认监听 `http://localhost:8765/`，同时绑定 `127.0.0.1`。

## 端点

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| `GET` | `/health`、`/` | 探活，不校验 Token |
| `POST` | `/speak` | 播报 |
| `OPTIONS` | 任意 | CORS 预检，返回 `204` |

## 请求体

`POST /speak` 只接受一个字段：

```json
{ "text": "要被朗读的文本" }
```

- `Content-Type: application/json; charset=utf-8`
- 请求体上限 **64 KB**
- `text` 会去掉首尾空白；空文本返回 `400`

## Token

在设置页填写 Token 后，请求必须带：

```
X-Token: 你的Token
```

留空表示本机任何程序都能触发。Token 只做本机防误触，不是安全边界——服务本身只监听环回地址。

## 状态码

| 状态码 | 含义 |
| --- | --- |
| `200` | 健康检查通过 |
| `202` | 已受理（不代表念完） |
| `400` | `text` 为空、缺失或 JSON 格式不对 |
| `401` | Token 不对 |
| `404` | 路径不存在 |
| `405` | 方法不对 |
| `413` | 请求体超过 64 KB |
| `503` | 插件启动中或停止中 |
| `500` | 内部错误，响应里带异常类型与原因 |

错误响应统一是：

```json
{ "ok": false, "error": "原因" }
```

## 调用示例

浏览器或 Node：

```js
await fetch("http://localhost:8765/speak", {
  method: "POST",
  headers: {
    "Content-Type": "application/json; charset=utf-8",
    "X-Token": "你的Token"
  },
  body: JSON.stringify({ text: "第三节课下课，请同学们有序离开教室。" })
});
```

curl：

```bash
curl http://localhost:8765/health
# {"ok":true,"name":"远程播报"}

curl -X POST http://localhost:8765/speak \
  -H "Content-Type: application/json; charset=utf-8" \
  -d '{"text":"上课了，请回到座位。"}'
# {"ok":true}   ← 202，只代表已受理
```

设置页的「复制调用示例」会按当前端口和 Token 生成一份现成的 `fetch` 代码。

## CORS

响应已放行所有来源与 `X-Token` 头：

```
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: POST, GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Token
Access-Control-Max-Age: 600
```

网页可以直接调用，不需要额外代理。

## 行为说明

- **202 只代表受理**：如果 **设置 → 提醒** 里的「启用提醒」总开关是关的，画面会被直接丢弃，但接口照样回 `202`。
- **打断式播报**：新播报会顶掉上一条，旧画面立刻收，念到一半的语音立刻掐，连发多条只有最后一条会念完。
- **会排队**：如果这一刻别的提醒（比如上课提醒）正占着画面，本条会排在它后面显示，语音也随它延后。
- **全局语音队列**：ClassIsland 的语音队列只有一条，掐断旧播报时会把宿主或其他插件排队的语音一起清空。
- **只监听本机**：其他设备连不上，跨网怎么打通由调用方自己处理（反向代理、内网穿透都行）。
