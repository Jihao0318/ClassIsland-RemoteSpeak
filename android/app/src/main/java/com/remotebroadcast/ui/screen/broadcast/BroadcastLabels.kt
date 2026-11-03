package com.remotebroadcast.ui.screen.broadcast

import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import com.remotebroadcast.R
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.data.remote.BroadcastFailure

@Composable
fun stringForFailure(failure: BroadcastFailure): String = stringResource(
    when (failure) {
        BroadcastFailure.INVALID_URL -> R.string.broadcast_fail_invalid_url
        BroadcastFailure.UNAUTHORIZED -> R.string.broadcast_fail_unauthorized
        BroadcastFailure.CAMERA_UNAVAILABLE -> R.string.broadcast_fail_camera
        BroadcastFailure.SERVER_ERROR -> R.string.broadcast_fail_server
        BroadcastFailure.NETWORK -> R.string.broadcast_fail_network
    }
)

@Composable
fun stringForTriggerMode(mode: BroadcastTriggerMode): String = stringResource(
    when (mode) {
        BroadcastTriggerMode.DIRECT -> R.string.broadcast_trigger_direct
        BroadcastTriggerMode.CONFIRM -> R.string.broadcast_trigger_confirm
        BroadcastTriggerMode.LONG_PRESS -> R.string.broadcast_trigger_long
    }
)

@Composable
fun triggerModeLabels(): List<String> =
    BroadcastTriggerMode.entries.map { stringForTriggerMode(it) }

@Composable
fun repeatLabels(): List<String> =
    (BroadcastApi.MIN_REPEAT..BroadcastApi.MAX_REPEAT).map { stringResource(R.string.broadcast_repeat_times, it) }

@Composable
fun stringForSnapshotStatus(status: SnapshotStatus, hasFrame: Boolean): String? = when (status) {
    is SnapshotStatus.Idle -> if (hasFrame) null else stringResource(R.string.broadcast_snapshot_idle)
    is SnapshotStatus.Loading -> stringResource(R.string.broadcast_snapshot_loading)
    is SnapshotStatus.Ready -> null
    is SnapshotStatus.Failed -> stringForFailure(status.failure) +
        status.serverMessage?.let { " · $it" }.orEmpty()
}
