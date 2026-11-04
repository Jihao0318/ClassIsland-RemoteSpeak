package com.remotebroadcast.ui.util

import android.content.Context
import com.remotebroadcast.templateApp
import okhttp3.Request

data class DownloadChannel(val name: String, val url: String)

/** 按设置页选的渠道返回下载地址；没选过就默认 GitHub 直连。 */
fun downloadChannels(url: String): List<DownloadChannel> {
    val prefs = templateApp.getSharedPreferences("settings", Context.MODE_PRIVATE)
    return when (prefs.getString("download_channel", "github")) {
        "ghfast" -> listOf(DownloadChannel("ghfast", "https://ghfast.top/$url"))
        "gh-proxy" -> listOf(DownloadChannel("gh-proxy", "https://gh-proxy.com/$url"))
        "custom" -> {
            val custom = prefs.getString("download_channel_custom", "").orEmpty().trim()
            if (custom.isEmpty()) {
                listOf(DownloadChannel("GitHub 直连", url))
            } else {
                listOf(DownloadChannel("自定义", "${custom.trimEnd('/')}/$url"))
            }
        }
        else -> listOf(DownloadChannel("GitHub 直连", url))
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
