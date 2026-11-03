package com.remotebroadcast.ui.screen.status

import androidx.compose.runtime.Immutable
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.data.remote.BroadcastFailure

enum class ConnectionStatus { UNKNOWN, CHECKING, ONLINE, OFFLINE }

@Immutable
data class ServerStatusUiState(
    val connection: ConnectionStatus = ConnectionStatus.UNKNOWN,
    val serviceName: String? = null,
    val baseUrl: String = "",
    val hasToken: Boolean = false,
    val usesCleartext: Boolean = false,
    val latencyMs: Int? = null,
    val lastCheckedAtMillis: Long? = null,
    val failure: BroadcastFailure? = null,
    val serverMessage: String? = null,
) {
    val isOnline: Boolean
        get() = connection == ConnectionStatus.ONLINE

    /** The server rejected us and nothing is stored locally, so the fix is to enter a token. */
    val tokenMissing: Boolean
        get() = !hasToken && failure == BroadcastFailure.UNAUTHORIZED
}
