# HTTP 接口

插件在本机开一个 HTTP 服务，默认监听 `http://localhost:8765/`，同时绑定 `127.0.0.1`。

## 端点

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| `GET` | `/health`、`/` | 探活，不校验 Token |
| `POST` | `/speak` | 播报 |
| `GET` | `/snapshot` | 取一帧本机摄像头画面（JPEG），默认关闭 |
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

## 取景 `GET /snapshot`

在设置页的「摄像头取景」里打开开关后，这个接口返回一帧当前画面：

```
GET /snapshot
X-Token: 你的Token          （设置了 Token 才需要）
```

- 成功：`200`，`Content-Type: image/jpeg`，带 `Cache-Control: no-store`
- 开关没开：`404`（这条路由等于不存在）
- 拍不出来：`503`，`error` 里写清楚原因（没摄像头 / 正被其他程序占用 / 系统禁止桌面应用访问相机）

拍一张约 1.3 秒：开摄像头、等自动曝光稳定（丢掉前两张偏黑的）、再按档位缩放。出图尺寸是高度上限，比例跟随摄像头，不会拉伸——16:9 的摄像头给 1280×720，4:3 的给 960×720。同一时刻只拍一张，后到的请求直接回 `503 上一次取景还没结束`，不排队。

用的是共享模式：不会把摄像头从别的程序（视频会议、直播）手里抢走，代价是改不了摄像头正在出的尺寸。

**这个接口不写硬盘**：JPEG 在内存里编好后直接写进响应流，插件侧没有临时文件、没有缓存目录，也不会留下任何副本。唯一落盘的是设置页的「试拍一张」，固定覆盖同一张 `%TEMP%\RemoteSpeak 试拍.jpg`。

> 取景期间画面不会有任何屏上提示，宿主的摄像头指示灯该亮还是会亮。要不要在网页那边标一句「本教室画面已接入」，由调用方决定。

## 状态码

| 状态码 | 含义 |
| --- | --- |
| `200` | 健康检查通过，或取景图 |
| `202` | 已受理（不代表念完） |
| `400` | `text` 为空、缺失或 JSON 格式不对 |
| `401` | Token 不对 |
| `404` | 路径不存在，或取景功能未开启 |
| `405` | 方法不对 |
| `413` | 请求体超过 64 KB |
| `503` | 插件启动中、停止中，或摄像头拍不出图 |
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

curl -o now.jpg -H "X-Token: 你的Token" http://localhost:8765/snapshot
# 成功时直接是 JPEG 字节流；失败时是个写着原因的 JSON
```

把画面挂到网页上：

```js
const shot = await fetch("http://localhost:8765/snapshot", { headers: { "X-Token": "你的Token" } });
if (shot.ok) document.querySelector("#cam").src = URL.createObjectURL(await shot.blob());
```

设置页的「复制调用示例」会按当前端口和 Token 生成 `health` / `speak` / `snapshot` 三段现成的 `fetch` 代码。

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
- **每条请求都记进插件日志**：方法、路径、状态码，播报额外带字数和正文前 20 字，取景额外带设备、尺寸、大小和耗时。`X-Token` 永不记录；不想记流水可以在设置里关掉「记录正常请求」。
