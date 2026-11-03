package com.remotebroadcast.ui.screen.broadcast

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.unit.Dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.ui.viewmodel.BroadcastViewModel

@Composable
fun BroadcastPager(
    bottomInnerPadding: Dp,
    isCurrentPage: Boolean = true,
) {
    val viewModel = viewModel<BroadcastViewModel>()
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    var showFullView by rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(isCurrentPage) {
        if (isCurrentPage) viewModel.refresh()
    }

    LaunchedEffect(uiState.autoRefreshSnapshot, isCurrentPage, uiState.setupDone) {
        if (uiState.autoRefreshSnapshot && isCurrentPage && uiState.setupDone) {
            while (isActive) {
                viewModel.loadSnapshot()
                delay(SNAPSHOT_AUTO_REFRESH_MILLIS)
            }
        }
    }

    val actions = BroadcastActions(
        onTextChanged = viewModel::onTextChanged,
        onSend = viewModel::send,
        onRepeatChange = { index -> viewModel.setRepeat(BroadcastApi.MIN_REPEAT + index) },
        onSkipGuide = viewModel::skipGuide,
        onSaveSetup = viewModel::saveSetup,
        onSnapshotClick = viewModel::loadSnapshot,
        onOpenFullView = { if (uiState.snapshot != null) showFullView = true },
    )

    BroadcastPagerMiuix(uiState, actions, bottomInnerPadding)

    val frame = uiState.snapshot
    if (showFullView && frame != null) {
        SnapshotViewer(frame = frame, onDismiss = { showFullView = false })
    }
}
