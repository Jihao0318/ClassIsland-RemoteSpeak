package com.remotebroadcast.data.remote

import okhttp3.Request
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class BroadcastApiTest {

    @Test
    fun blankUrlIsInvalidInsteadOfFallingBack() {
        val normalized = BroadcastApi.normalizeBaseUrl("   ")
        assertFalse(normalized.isValid)
        assertEquals("", normalized.url)
        assertFalse(normalized.isCleartext)
    }

    @Test
    fun missingSchemeIsTreatedAsHttps() {
        val normalized = BroadcastApi.normalizeBaseUrl("example.com")
        assertTrue(normalized.isValid)
        assertEquals("https://example.com", normalized.url)
    }

    @Test
    fun cleartextIsFlaggedSoTheUiCanWarn() {
        val normalized = BroadcastApi.normalizeBaseUrl("http://192.168.1.5:8766")
        assertTrue(normalized.isValid)
        assertTrue(normalized.isCleartext)
    }

    @Test
    fun unsupportedInputIsReportedInvalidInsteadOfThrowing() {
        assertFalse(BroadcastApi.normalizeBaseUrl("not a url").isValid)
        assertFalse(BroadcastApi.normalizeBaseUrl("ftp://example.com").isValid)
    }

    @Test
    fun endpointsAreAppendedToThePath() {
        assertEquals(
            "https://example.com/speak",
            BroadcastApi.endpointUrl("example.com", "speak").toString(),
        )
        assertEquals(
            "http://192.168.1.5:8766/snapshot",
            BroadcastApi.endpointUrl("http://192.168.1.5:8766/", "snapshot").toString(),
        )
        assertEquals(
            "https://example.com/voice/health",
            BroadcastApi.endpointUrl("https://example.com/voice", "health").toString(),
        )
    }

    @Test
    fun urlBuilderReturnsNullForInvalidBaseUrl() {
        assertNull(BroadcastApi.endpointUrl("::bad::", "speak"))
    }

    @Test
    fun blankTokenSendsNoHeaderAtAll() {
        val request = BroadcastApi.withToken(Request.Builder().url("https://example.com/snapshot"), "").build()
        assertNull(request.header("X-Token"))

        val spaced = BroadcastApi.withToken(Request.Builder().url("https://example.com/snapshot"), "   ").build()
        assertNull(spaced.header("X-Token"))
    }

    @Test
    fun configuredTokenIsSentAsHeader() {
        val request = BroadcastApi.withToken(Request.Builder().url("https://example.com/snapshot"), "secret").build()
        assertEquals("secret", request.header("X-Token"))
    }

    @Test
    fun speakBodyEscapesQuotesAndLineBreaks() {
        assertEquals("{\"text\":\"安静\",\"repeat\":3}", BroadcastApi.speakBody("安静", 3))
        assertEquals(
            "{\"text\":\"他说\\\"上课\\\"\\n第二行\",\"repeat\":1}",
            BroadcastApi.speakBody("他说\"上课\"\n第二行", 1),
        )
        assertEquals(
            "{\"text\":\"路径 C:\\\\tmp\",\"repeat\":2}",
            BroadcastApi.speakBody("路径 C:\\tmp", 2),
        )
    }

    @Test
    fun repeatIsClampedToTheSupportedRange() {
        assertTrue(BroadcastApi.speakBody("x", 0).contains("\"repeat\":1"))
        assertTrue(BroadcastApi.speakBody("x", 99).contains("\"repeat\":5"))
    }

    @Test
    fun statusCodesMapToDistinctFailures() {
        assertEquals(BroadcastFailure.UNAUTHORIZED, BroadcastApi.classifyFailure(401))
        assertEquals(BroadcastFailure.UNAUTHORIZED, BroadcastApi.classifyFailure(403))
        assertEquals(BroadcastFailure.CAMERA_UNAVAILABLE, BroadcastApi.classifyFailure(503))
        assertEquals(BroadcastFailure.SERVER_ERROR, BroadcastApi.classifyFailure(500))
    }

    @Test
    fun serverErrorTextIsShownVerbatim() {
        assertEquals("Token 不对", BroadcastApi.serverErrorOf("{\"ok\":false,\"error\":\"Token 不对\"}"))
        assertEquals("带\"引号", BroadcastApi.serverErrorOf("{\"error\":\"带\\\"引号\"}"))
        assertNull(BroadcastApi.serverErrorOf("{\"ok\":true}"))
        assertNull(BroadcastApi.serverErrorOf(null))
    }

    @Test
    fun healthBodyServiceNameIsParsed() {
        assertEquals("远程播报", BroadcastApi.serviceNameOf("{\"ok\":true,\"name\":\"远程播报\"}"))
        assertEquals("", BroadcastApi.serviceNameOf("{\"ok\":true}"))
    }
}
