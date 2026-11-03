package com.remotebroadcast.ui.screen.broadcast

import com.remotebroadcast.data.remote.BroadcastApi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BroadcastUiStateTest {

    private val configured = BroadcastUiState(
        baseUrl = "https://classroom.example",
        setupDone = true,
        text = "上课了",
    )

    @Test
    fun guideBlocksSendingUntilItIsAnswered() {
        val fresh = BroadcastUiState()
        assertTrue(fresh.showGuide)
        assertFalse(fresh.canSend)

        val answered = fresh.copy(baseUrl = "https://classroom.example", setupDone = true, text = "上课了")
        assertFalse(answered.showGuide)
        assertTrue(answered.canSend)
    }

    @Test
    fun sendingNeedsTextAndAValidAddress() {
        assertTrue(configured.canSend)
        assertFalse(configured.copy(text = "   ").canSend)
        assertFalse(configured.copy(baseUrl = "not a url").canSend)
        assertFalse(configured.copy(sendStatus = SendStatus.Sending).canSend)
    }

    @Test
    fun eachTriggerModeDrivesExactlyOneGesture() {
        val direct = configured.copy(triggerMode = BroadcastTriggerMode.DIRECT)
        assertTrue(direct.sendsOnClick)
        assertFalse(direct.confirmsBeforeSend)
        assertFalse(direct.sendsOnLongPress)

        val confirm = configured.copy(triggerMode = BroadcastTriggerMode.CONFIRM)
        assertFalse(confirm.sendsOnClick)
        assertTrue(confirm.confirmsBeforeSend)
        assertFalse(confirm.sendsOnLongPress)

        val hold = configured.copy(triggerMode = BroadcastTriggerMode.LONG_PRESS)
        assertFalse(hold.sendsOnClick)
        assertFalse(hold.confirmsBeforeSend)
        assertTrue(hold.sendsOnLongPress)
    }

    @Test
    fun unknownPersistedValuesFallBackInsteadOfCrashing() {
        assertEquals(BroadcastTriggerMode.CONFIRM, BroadcastTriggerMode.fromValue(42))
        assertEquals(BroadcastTriggerMode.LONG_PRESS, BroadcastTriggerMode.fromValue(2))
    }

    @Test
    fun defaultStateHasNoServerUntilConfigured() {
        val fresh = BroadcastUiState()
        assertFalse(fresh.urlIsValid)
        assertEquals("", fresh.baseUrl)
    }

    @Test
    fun plainHttpAddressIsDetectedForTheWarningLine() {
        assertTrue(configured.copy(baseUrl = "http://192.168.1.5:8766").usesCleartext)
        assertFalse(configured.copy(baseUrl = "https://192.168.1.5:8766").usesCleartext)
    }

    @Test
    fun repeatDropdownIndexMatchesStoredValue() {
        assertEquals(0, BroadcastApi.MIN_REPEAT.let { configured.copy(repeat = it).repeat - BroadcastApi.MIN_REPEAT })
        val last = configured.copy(repeat = BroadcastApi.MAX_REPEAT)
        assertEquals(BroadcastApi.MAX_REPEAT - BroadcastApi.MIN_REPEAT, last.repeat - BroadcastApi.MIN_REPEAT)
    }
}
