package com.remotebroadcast.ui.util

import android.content.Intent
import androidx.core.content.FileProvider
import com.remotebroadcast.templateApp
import okhttp3.Request
import java.io.File

/** GitHub 直连 + 常见加速镜像，按顺序试，前一个失败换下一个。 */
private val downloadChannels: List<(String) -> String> = listOf(
    { it },
    { "https://ghfast.top/$it" },
    { "https://ghproxy.net/$it" },
    { "https://gh-proxy.com/$it" },
)

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
 * 把 APK 下到应用缓存目录，返回文件；所有渠道都失败时返回 null。
 * 调用方负责放到 IO 线程。
 */
fun downloadApk(url: String, onProgress: (Float) -> Unit): File? {
    val dir = File(templateApp.cacheDir, "updates").apply { mkdirs() }
    val file = File(dir, "RemoteBroadcast-update.apk")

    for (wrap in downloadChannels) {
        val candidate = wrap(url)
        runCatching {
            val request = Request.Builder().url(candidate).build()
            templateApp.okhttpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@use
                val body = response.body
                val total = body.contentLength()
                file.outputStream().use { output ->
                    body.byteStream().use { input ->
                        val buffer = ByteArray(16 * 1024)
                        var written = 0L
                        while (true) {
                            val read = input.read(buffer)
                            if (read < 0) break
                            output.write(buffer, 0, read)
                            written += read
                            if (total > 0) {
                                onProgress((written.toFloat() / total).coerceIn(0f, 1f))
                            }
                        }
                    }
                }
            }
            if (file.length() > 1024) return file
        }
        file.delete()
    }
    return null
}

/** 调系统安装器；没有安装权限的机型会走到应用详情页让用户自己放行。 */
fun installApk(file: File) {
    val context = templateApp
    val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
    val intent = Intent(Intent.ACTION_VIEW).apply {
        setDataAndType(uri, "application/vnd.android.package-archive")
        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    }
    context.startActivity(intent)
}
