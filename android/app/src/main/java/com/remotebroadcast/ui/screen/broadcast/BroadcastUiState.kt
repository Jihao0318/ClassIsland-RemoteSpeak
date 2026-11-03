package com.remotebroadcast.ui.screen.broadcast

import androidx.compose.runtime.Immutable
import androidx.compose.ui.graphics.ImageBitmap
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.data.remote.BroadcastFailure

const val SNAPSHOT_AUTO_REFRESH_MILLIS = 2000L

enum class BroadcastTriggerMode(val value: Int) {
    DIRECT(0),
    CONFIRM(1),
    LONG_PRESS(2);

    companion object {
        fun fromValue(value: Int): BroadcastTriggerMode =
            entries.firstOrNull { it.value == value } ?: CONFIRM
    }
}

sealed interface SendStatus {
    data object Idle : SendStatus
    data object Empty : SendStatus
    data object Sending : SendStatus
    data class Sent(val repeat: Int) : SendStatus
    data class Rejected(val failure: BroadcastFailure, val serverMessage: String?) : SendStatus
}

sealed interface SnapshotStatus {
    data object Idle : SnapshotStatus
    data object Loading : SnapshotStatus
    data object Ready : SnapshotStatus
    data class Failed(val failure: BroadcastFailure, val serverMessage: String?) : SnapshotStatus
}

@Immutable
data class SnapshotView(
    val image: ImageBitmap,
    val capturedAtMillis: Long,
    val byteSize: Int,
)

@Immutable
data class BroadcastUiState(
    val baseUrl: String = "",
    val token: String = "",
    val repeat: Int = BroadcastApi.MIN_REPEAT,
    val triggerMode: BroadcastTriggerMode = BroadcastTriggerMode.CONFIRM,
    val autoRefreshSnapshot: Boolean = false,
    val setupDone: Boolean = false,
    val text: String = "",
    val sendStatus: SendStatus = SendStatus.Idle,
    val snapshotStatus: SnapshotStatus = SnapshotStatus.Idle,
    val snapshot: SnapshotView? = null,
) {
    val showGuide: Boolean
        get() = !setupDone

    private val normalizedUrl: BroadcastApi.NormalizedUrl
        get() = BroadcastApi.normalizeBaseUrl(baseUrl)

    val urlIsValid: Boolean
        get() = normalizedUrl.isValid

    val usesCleartext: Boolean
        get() = normalizedUrl.isCleartext

    val canSend: Boolean
        get() = setupDone && text.isNotBlank() && urlIsValid && sendStatus !is SendStatus.Sending

    val sendsOnClick: Boolean
        get() = triggerMode == BroadcastTriggerMode.DIRECT

    val confirmsBeforeSend: Boolean
        get() = triggerMode == BroadcastTriggerMode.CONFIRM

    val sendsOnLongPress: Boolean
        get() = triggerMode == BroadcastTriggerMode.LONG_PRESS
}

@Immutable
data class BroadcastActions(
    val onTextChanged: (String) -> Unit,
    val onSend: () -> Unit,
    val onRepeatChange: (Int) -> Unit,
    val onSkipGuide: () -> Unit,
    val onSaveSetup: (String, String) -> Unit,
    val onSnapshotClick: () -> Unit,
    val onOpenFullView: () -> Unit,
)
