# 远程播报 RemoteSpeak

让网页或脚本用一条 HTTP 请求，把文字变成 ClassIsland 顶栏的提醒并朗读出来。

## 装好之后

1. **设置 → 提醒 → 启用提醒语音**（ClassIsland 默认关着，不开只有画面没有声音）。
2. **设置 → 外部设置 → 远程播报** → 打开 HTTP 服务，按需设置端口与 Token。

## 调用

```js
await fetch("http://localhost:8765/speak", {
  method: "POST",
  headers: {
    "Content-Type": "application/json; charset=utf-8",
    "X-Token": "你的Token"
  },
  body: JSON.stringify({ text: "这段话会被念出来" })
});
```

`GET /health` 探活，`POST /speak` 播报。请求体上限 64 KB，`202` 表示已受理。
朗读用的是 ClassIsland 自己选定的语音引擎，本插件不带第二套朗读通道。
设置页的「复制调用示例」会按当前端口和 Token 生成现成代码。

## 没有声音

按顺序检查：

1. **设置 → 提醒 → 启用提醒语音** 是否打开。
2. **设置 → 提醒 → 提醒管理 → 远程播报** 里若开了「对此提醒来源启用特殊高级设置」，
   以本条自己的「启用提醒语音」为准，而它默认是关的。
3. Edge TTS 连不上、429、合成超时都发生在宿主里，插件看不到——去设置页点
   「打开 ClassIsland 日志目录」查。

## 注意

- 只监听本机，其他设备连不上；跨网自己处理。
- 新播报直接顶掉上一条：旧画面立刻收，念到一半的语音立刻掐，连发多条只有最后一条会念完。
- 语音队列全局只有一条，掐断时宿主或其他插件排队的语音会一并清空。
- 别的提醒正占着画面时，本条会排在它后面，语音也随它延后。

项目主页：<https://github.com/Jihao0318/ClassIsland-RemoteSpeak>
