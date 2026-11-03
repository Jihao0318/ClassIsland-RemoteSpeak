package com.remotebroadcast.ui.viewmodel

import android.os.SystemClock
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.data.remote.BroadcastOutcome
import com.remotebroadcast.data.repository.SettingsRepository
import com.remotebroadcast.data.repository.SettingsRepositoryImpl
import com.remotebroadcast.ui.screen.status.ConnectionStatus
import com.remotebroadcast.ui.screen.status.ServerStatusUiState

class ServerStatusViewModel(
    private val repo: SettingsRepository = SettingsRepositoryImpl(),
) : ViewModel() {

    private val _uiState = MutableStateFlow(readConfig())
    val uiState: StateFlow<ServerStatusUiState> = _uiState.asStateFlow()
    private var checkJob: Job? = null

    private fun readConfig() = BroadcastApi.normalizeBaseUrl(repo.broadcastUrl)
        .let { normalized ->
            ServerStatusUiState(
                baseUrl = if (normalized.isValid) normalized.url else repo.broadcastUrl.trim(),
                hasToken = repo.broadcastToken.isNotBlank(),
                usesCleartext = normalized.isCleartext,
            )
        }

    fun syncConfig() {
        val config = readConfig()
        _uiState.update {
            it.copy(
                baseUrl = config.baseUrl,
                hasToken = config.hasToken,
                usesCleartext = config.usesCleartext,
            )
        }
    }

    /**
     * Home refreshes silently so the status card never blinks through "checking" while the verdict is
     * unchanged; only the first probe and an explicit tap on the status page show the busy state.
     */
    /** Home entry point: busy state only until the very first verdict lands. */
    fun refreshQuietly() {
        check(showBusy = _uiState.value.lastCheckedAtMillis == null)
    }

    fun check(showBusy: Boolean = false) {
        if (checkJob?.isActive == true) return
        val baseUrl = _uiState.value.baseUrl
        if (baseUrl.isBlank()) {
            // 还没填服务器地址：不装作"离线"，等用户配置完再检测。
            _uiState.update {
                it.copy(
                    connection = ConnectionStatus.UNKNOWN,
                    failure = BroadcastFailure.INVALID_URL,
                    serverMessage = null,
                    latencyMs = null,
                    lastCheckedAtMillis = null,
                )
            }
            return
        }
        val token = repo.broadcastToken
        checkJob = viewModelScope.launch {
            if (showBusy) _uiState.update { it.copy(connection = ConnectionStatus.CHECKING) }
            val startedAt = SystemClock.elapsedRealtime()
            val outcome = withContext(Dispatchers.IO) { BroadcastApi.health(baseUrl, token) }
            val latencyMs = (SystemClock.elapsedRealtime() - startedAt).toInt()
            _uiState.update {
                when (outcome) {
                    is BroadcastOutcome.Success -> it.copy(
                        connection = ConnectionStatus.ONLINE,
                        serviceName = outcome.value.ifBlank { null },
                        latencyMs = latencyMs,
                        failure = null,
                        serverMessage = null,
                        lastCheckedAtMillis = System.currentTimeMillis(),
                    )

                    is BroadcastOutcome.Failed -> it.copy(
                        connection = ConnectionStatus.OFFLINE,
                        serviceName = null,
                        latencyMs = null,
                        failure = outcome.failure,
                        serverMessage = outcome.serverMessage,
                        lastCheckedAtMillis = System.currentTimeMillis(),
                    )
                }
            }
        }
    }
}
