package com.remotebroadcast.ui.util

import android.content.Context
import android.os.SystemClock
import com.remotebroadcast.templateApp
import okhttp3.Request

data class DownloadChannel(val name: String, val url: String)

private fun allChannels(url: String): List<DownloadChannel> = listOf(
    DownloadChannel("GitHub 直连", url),
    DownloadChannel("ghfast", "https://ghfast.top/$url"),
    DownloadChannel("gh-proxy", "https://gh-proxy.com/$url"),
)

/**
 * 按设置页选的渠道返回候选列表：
 * auto 返回全部（调用方测速选最快），其余只返回选中的那一个。
 */
fun downloadChannels(url: String): List<DownloadChannel> {
    val prefs = templateApp.getSharedPreferences("settings", Context.MODE_PRIVATE)
    return when (prefs.getString("download_channel", "auto")) {
        "github" -> listOf(DownloadChannel("GitHub 直连", url))
        "ghfast" -> listOf(DownloadChannel("ghfast", "https://ghfast.top/$url"))
        "gh-proxy" -> listOf(DownloadChannel("gh-proxy", "https://gh-proxy.com/$url"))
        "custom" -> {
            val custom = prefs.getString("download_channel_custom", "").orEmpty().trim()
            if (custom.isEmpty()) {
                allChannels(url)
            } else {
                listOf(DownloadChannel("自定义", "${custom.trimEnd('/')}/$url"))
            }
        }
        else -> allChannels(url)
    }
}

fun checkNewVersion(): LatestVersionInfo {
    if (!isNetworkAvailable(templateApp)) return LatestVersionInfo()
    val url = "https://api.github.com/repos/Jihao0318/ClassIsland-RemoteSpeak/releases/latest"
    // default null value if failed
    val defaultValue = LatestVersionInfo()
    runCatching {
        templateApp.okhttpClient.newCall(Request.Builder().url(url).build()).execute()
            .use { response ->
                if (!response.isSuccessful) {
                    return defaultValue
                }
                val body = response.body.string()
                val json = org.json.JSONObject(body)
                val changelog = json.optString("body")

                val assets = json.getJSONArray("assets")
                for (i in 0 until assets.length()) {
                    val asset = assets.getJSONObject(i)
                    val name = asset.getString("name")
                    if (!name.endsWith(".apk")) {
                        continue
                    }

                    val regex = Regex("v(.+?)_(\\d+)-")
                    val matchResult = regex.find(name) ?: continue
                    val versionCode = matchResult.groupValues[2].toInt()
                    val downloadUrl = asset.getString("browser_download_url")

                    return LatestVersionInfo(
                        versionCode,
                        downloadUrl,
                        changelog
                    )
                }

            }
    }
    return defaultValue
}

/**
 * 对下载地址发一次 HEAD，返回毫秒；连不上或返回错误码时返回 -1。
 * 调用方负责放到 IO 线程。
 */
fun measureLatency(url: String): Int {
    val startedAt = SystemClock.elapsedRealtime()
    return runCatching {
        val request = Request.Builder().url(url).head().build()
        templateApp.okhttpClient.newCall(request).execute().use { response ->
            if (response.isSuccessful) {
                (SystemClock.elapsedRealtime() - startedAt).toInt()
            } else {
                -1
            }
        }
    }.getOrDefault(-1)
}
