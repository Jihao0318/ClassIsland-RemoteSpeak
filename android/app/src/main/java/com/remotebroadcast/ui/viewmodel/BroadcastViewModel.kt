package com.remotebroadcast.ui.viewmodel

import android.graphics.BitmapFactory
import androidx.compose.ui.graphics.asImageBitmap
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
import com.remotebroadcast.data.remote.BroadcastFailure
import com.remotebroadcast.data.remote.BroadcastOutcome
import com.remotebroadcast.data.remote.SnapshotFrame
import com.remotebroadcast.data.repository.SettingsRepository
import com.remotebroadcast.data.repository.SettingsRepositoryImpl
import com.remotebroadcast.ui.screen.broadcast.BroadcastTriggerMode
import com.remotebroadcast.ui.screen.broadcast.BroadcastUiState
import com.remotebroadcast.ui.screen.broadcast.SendStatus
import com.remotebroadcast.ui.screen.broadcast.SnapshotStatus
import com.remotebroadcast.ui.screen.broadcast.SnapshotView

class BroadcastViewModel(
    private val repo: SettingsRepository = SettingsRepositoryImpl(),
) : ViewModel() {

    private val _uiState = MutableStateFlow(readPersisted())
    val uiState: StateFlow<BroadcastUiState> = _uiState.asStateFlow()

    private var sendJob: Job? = null
    private var snapshotJob: Job? = null

    private fun readPersisted(): BroadcastUiState = BroadcastUiState(
        baseUrl = repo.broadcastUrl,
        token = repo.broadcastToken,
        repeat = repo.broadcastRepeat,
        triggerMode = BroadcastTriggerMode.fromValue(repo.broadcastTriggerMode),
        autoRefreshSnapshot = repo.snapshotAutoRefresh,
        setupDone = repo.broadcastSetupDone,
    )

    fun refresh() {
        val persisted = readPersisted()
        _uiState.update {
            it.copy(
                baseUrl = persisted.baseUrl,
                token = persisted.token,
                repeat = persisted.repeat,
                triggerMode = persisted.triggerMode,
                autoRefreshSnapshot = persisted.autoRefreshSnapshot,
                setupDone = persisted.setupDone,
            )
        }
    }

    fun onTextChanged(value: String) {
        _uiState.update { it.copy(text = value, sendStatus = SendStatus.Idle) }
    }

    fun saveSetup(url: String, token: String) {
        repo.broadcastUrl = url
        repo.broadcastToken = token
        repo.broadcastSetupDone = true
        _uiState.update {
            it.copy(
                baseUrl = repo.broadcastUrl,
                token = repo.broadcastToken,
                setupDone = true,
            )
        }
    }

    fun skipGuide() {
        repo.broadcastSetupDone = true
        _uiState.update { it.copy(setupDone = true) }
    }

    fun setRepeat(value: Int) {
        repo.broadcastRepeat = value
        _uiState.update { it.copy(repeat = repo.broadcastRepeat) }
    }

    fun setAutoRefreshSnapshot(enabled: Boolean) {
        repo.snapshotAutoRefresh = enabled
        _uiState.update { it.copy(autoRefreshSnapshot = enabled) }
    }

    fun send() {
        val state = _uiState.value
        if (state.text.isBlank()) {
            _uiState.update { it.copy(sendStatus = SendStatus.Empty) }
            return
        }
        if (!state.urlIsValid) {
            _uiState.update {
                it.copy(sendStatus = SendStatus.Rejected(BroadcastFailure.INVALID_URL, null))
            }
            return
        }
        if (state.sendStatus is SendStatus.Sending) return
        sendJob?.cancel()
        sendJob = viewModelScope.launch {
            _uiState.update { it.copy(sendStatus = SendStatus.Sending) }
            val outcome = withContext(Dispatchers.IO) {
                BroadcastApi.speak(state.baseUrl, state.token, state.text, state.repeat)
            }
            _uiState.update {
                when (outcome) {
                    is BroadcastOutcome.Success -> it.copy(sendStatus = SendStatus.Sent(outcome.value))
                    is BroadcastOutcome.Failed -> it.copy(
                        sendStatus = SendStatus.Rejected(outcome.failure, outcome.serverMessage),
                    )
                }
            }
        }
    }

    fun loadSnapshot() {
        if (snapshotJob?.isActive == true) return
        val state = _uiState.value
        snapshotJob = viewModelScope.launch {
            if (state.snapshot == null) _uiState.update { it.copy(snapshotStatus = SnapshotStatus.Loading) }
            val outcome = withContext(Dispatchers.IO) {
                BroadcastApi.snapshot(state.baseUrl, state.token)
            }
            when (outcome) {
                is BroadcastOutcome.Success -> withContext(Dispatchers.Main) {
                    val view = decodeFrame(outcome.value)
                    if (view == null) {
                        _uiState.update {
                            it.copy(snapshotStatus = SnapshotStatus.Failed(BroadcastFailure.SERVER_ERROR, null))
                        }
                    } else {
                        _uiState.update {
                            it.copy(snapshot = view, snapshotStatus = SnapshotStatus.Ready)
                        }
                    }
                }

                is BroadcastOutcome.Failed -> _uiState.update {
                    it.copy(snapshotStatus = SnapshotStatus.Failed(outcome.failure, outcome.serverMessage))
                }
            }
        }
    }

    private fun decodeFrame(frame: SnapshotFrame): SnapshotView? = runCatching {
        BitmapFactory.decodeByteArray(frame.jpeg, 0, frame.jpeg.size)
            ?.asImageBitmap()
            ?.let { SnapshotView(it, frame.capturedAtMillis, frame.jpeg.size) }
    }.getOrNull()
}
