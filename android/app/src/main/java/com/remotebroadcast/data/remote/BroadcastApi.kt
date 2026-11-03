package com.remotebroadcast.data.remote

import com.remotebroadcast.templateApp
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import java.io.IOException

private val JSON_MEDIA_TYPE = "application/json; charset=utf-8".toMediaType()

private const val ENDPOINT_HEALTH = "health"
private const val ENDPOINT_SPEAK = "speak"
private const val ENDPOINT_SNAPSHOT = "snapshot"

enum class BroadcastFailure {
    INVALID_URL,
    UNAUTHORIZED,
    CAMERA_UNAVAILABLE,
    SERVER_ERROR,
    NETWORK,
}

sealed interface BroadcastOutcome<out T> {
    data class Success<T>(val value: T) : BroadcastOutcome<T>
    data class Failed(
        val failure: BroadcastFailure,
        val httpCode: Int? = null,
        val serverMessage: String? = null,
    ) : BroadcastOutcome<Nothing>
}

class SnapshotFrame(val jpeg: ByteArray, val capturedAtMillis: Long)

object BroadcastApi {

    const val MIN_REPEAT = 1
    const val MAX_REPEAT = 5

    /** Blank input is invalid: the app ships with no server, the user has to enter one. */
    fun normalizeBaseUrl(raw: String): NormalizedUrl {
        val trimmed = raw.trim()
        val candidate = when {
            trimmed.isEmpty() -> return NormalizedUrl("", isValid = false, isCleartext = false)
            trimmed.startsWith("http://") || trimmed.startsWith("https://") -> trimmed
            trimmed.contains("://") -> return NormalizedUrl(trimmed, isValid = false, isCleartext = false)
            else -> "https://$trimmed"
        }
        val url = candidate.runCatching { toHttpUrl() }.getOrNull()
            ?: return NormalizedUrl(candidate, isValid = false, isCleartext = false)
        if (url.host.isBlank()) return NormalizedUrl(candidate, isValid = false, isCleartext = url.scheme == "http")
        return NormalizedUrl(
            url = url.toString().trimEnd('/'),
            isValid = true,
            isCleartext = url.scheme == "http",
        )
    }

    data class NormalizedUrl(val url: String, val isValid: Boolean, val isCleartext: Boolean)

    fun endpointUrl(baseUrl: String, endpoint: String): HttpUrl? {
        val normalized = normalizeBaseUrl(baseUrl)
        if (!normalized.isValid) return null
        return normalized.url.toHttpUrl().newBuilder()
            .addPathSegment(endpoint)
            .build()
    }

    /** An unconfigured server sends no token at all; an empty header would be rejected as a wrong one. */
    fun withToken(builder: Request.Builder, token: String): Request.Builder =
        if (token.isBlank()) builder else builder.header("X-Token", token)

    fun speakBody(text: String, repeat: Int): String {
        val clamped = repeat.coerceIn(MIN_REPEAT, MAX_REPEAT)
        return "{\"text\":${jsonString(text)},\"repeat\":$clamped}"
    }

    private fun jsonString(value: String): String {
        val builder = StringBuilder(value.length + 2)
        builder.append('"')
        value.forEach { char ->
            when (char) {
                '\\' -> builder.append("\\\\")
                '"' -> builder.append("\\\"")
                '\n' -> builder.append("\\n")
                '\r' -> builder.append("\\r")
                '\t' -> builder.append("\\t")
                else -> if (char.code < 0x20) builder.append("\\u").append(char.code.toString(16).padStart(4, '0'))
                else builder.append(char)
            }
        }
        builder.append('"')
        return builder.toString()
    }

    fun classifyFailure(httpCode: Int): BroadcastFailure = when (httpCode) {
        401, 403 -> BroadcastFailure.UNAUTHORIZED
        503 -> BroadcastFailure.CAMERA_UNAVAILABLE
        else -> BroadcastFailure.SERVER_ERROR
    }

    fun serverErrorOf(body: String?): String? {
        if (body.isNullOrBlank()) return null
        return unescapeJsonString(JSON_ERROR.find(body)?.groupValues?.getOrNull(1))
    }

    fun serviceNameOf(body: String): String =
        unescapeJsonString(JSON_NAME.find(body)?.groupValues?.getOrNull(1)).orEmpty()

    private fun unescapeJsonString(raw: String?): String? = raw
        ?.replace("\\\"", "\"")
        ?.replace("\\\\", "\\")
        ?.takeIf { it.isNotBlank() }

    private val JSON_ERROR = Regex("\"error\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"")
    private val JSON_NAME = Regex("\"name\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"")

    fun health(baseUrl: String, token: String): BroadcastOutcome<String> =
        get(ENDPOINT_HEALTH, baseUrl, token) { response ->
            response.body.string().let(::serviceNameOf)
        }

    fun snapshot(baseUrl: String, token: String): BroadcastOutcome<SnapshotFrame> =
        get(ENDPOINT_SNAPSHOT, baseUrl, token) { response ->
            SnapshotFrame(response.body.bytes(), System.currentTimeMillis())
        }

    private inline fun <T> get(
        endpoint: String,
        baseUrl: String,
        token: String,
        read: (Response) -> T,
    ): BroadcastOutcome<T> {
        val url = endpointUrl(baseUrl, endpoint) ?: return BroadcastOutcome.Failed(BroadcastFailure.INVALID_URL)
        val request = withToken(
            Request.Builder().url(url).header("Cache-Control", "no-store"),
            token,
        ).build()
        return execute(request, uncachedClient()) { response ->
            if (!response.isSuccessful) {
                failedFrom(response)
            } else {
                BroadcastOutcome.Success(read(response))
            }
        }
    }

    /** The shared client caches GET responses on disk; a stale classroom frame must never be served. */
    private fun uncachedClient(): OkHttpClient =
        templateApp.okhttpClient.newBuilder().cache(null).build()

    fun speak(baseUrl: String, token: String, text: String, repeat: Int): BroadcastOutcome<Int> {
        val trimmed = text.trim()
        if (trimmed.isEmpty()) return BroadcastOutcome.Failed(BroadcastFailure.INVALID_URL)
        val url = endpointUrl(baseUrl, ENDPOINT_SPEAK) ?: return BroadcastOutcome.Failed(BroadcastFailure.INVALID_URL)
        val request = withToken(
            Request.Builder()
                .url(url)
                .header("Cache-Control", "no-store")
                .post(speakBody(trimmed, repeat).toRequestBody(JSON_MEDIA_TYPE)),
            token,
        ).build()
        val clamped = repeat.coerceIn(MIN_REPEAT, MAX_REPEAT)
        return execute(request) { response ->
            if (!response.isSuccessful) {
                failedFrom(response)
            } else {
                BroadcastOutcome.Success(clamped)
            }
        }
    }

    private inline fun <T> execute(
        request: Request,
        client: OkHttpClient = templateApp.okhttpClient,
        handle: (Response) -> BroadcastOutcome<T>,
    ): BroadcastOutcome<T> =
        try {
            client.newCall(request).execute().use(handle)
        } catch (exception: IOException) {
            BroadcastOutcome.Failed(BroadcastFailure.NETWORK, serverMessage = exception.message)
        }

    private fun failedFrom(response: Response): BroadcastOutcome.Failed =
        BroadcastOutcome.Failed(
            failure = classifyFailure(response.code),
            httpCode = response.code,
            serverMessage = serverErrorOf(runCatching { response.body.string() }.getOrNull()),
        )
}
