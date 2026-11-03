package com.remotebroadcast.ui.screen.status

import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.data.remote.BroadcastFailure
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ServerStatusUiStateTest {

    @Test
    fun aRejectedRequestWithoutStoredTokenPointsAtTheTokenField() {
        val state = ServerStatusUiState(
            connection = ConnectionStatus.OFFLINE,
            failure = BroadcastFailure.UNAUTHORIZED,
        )
        assertTrue(state.tokenMissing)

        val withToken = state.copy(hasToken = true)
        assertFalse("A wrong token is not the same as a missing one", withToken.tokenMissing)
    }

    @Test
    fun otherFailuresDoNotBlameTheToken() {
        val offline = ServerStatusUiState(
            connection = ConnectionStatus.OFFLINE,
            failure = BroadcastFailure.NETWORK,
        )
        assertFalse(offline.tokenMissing)
        assertFalse(offline.isOnline)
    }

    @Test
    fun onlineIsTheOnlyVerdictThatLightsTheCardGreen() {
        assertTrue(ServerStatusUiState(connection = ConnectionStatus.ONLINE).isOnline)
        assertFalse(ServerStatusUiState(connection = ConnectionStatus.CHECKING).isOnline)
        assertFalse(ServerStatusUiState().isOnline)
    }

    @Test
    fun cleartextAddressIsFlaggedOnTheStatusPage() {
        val state = ServerStatusUiState(
            baseUrl = BroadcastApi.normalizeBaseUrl("http://192.168.1.5:8766").url,
            usesCleartext = true,
        )
        assertTrue(state.usesCleartext)
        assertFalse(state.tokenMissing)
    }

    @Test
    fun uncheckedStatusCarriesNoTimestamp() {
        val state = ServerStatusUiState()
        assertEquals(ConnectionStatus.UNKNOWN, state.connection)
        assertEquals(null, state.lastCheckedAtMillis)
    }
}
