package com.remotebroadcast.ui.screen.settings

import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.unit.Dp
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.remotebroadcast.ui.LocalMainPagerState
import com.remotebroadcast.ui.navigation3.Navigator
import com.remotebroadcast.ui.navigation3.Route
import com.remotebroadcast.ui.viewmodel.SettingsViewModel

@Composable
fun SettingPager(
    navigator: Navigator,
    bottomInnerPadding: Dp
) {
    val viewModel = viewModel<SettingsViewModel>()
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    LifecycleResumeEffect(Unit) {
        viewModel.refresh()
        onPauseOrDispose { }
    }

    val actions = SettingsScreenActions(
        onSetCheckUpdate = viewModel::setCheckUpdate,
        onCheckUpdateNow = viewModel::checkUpdateNow,
        onOpenTheme = { navigator.push(Route.ColorPalette) },
        onOpenAbout = { navigator.push(Route.About) },
        onSetBroadcastUrl = viewModel::setBroadcastUrl,
        onSetBroadcastToken = viewModel::setBroadcastToken,
        onSetBroadcastTriggerIndex = viewModel::setBroadcastTriggerIndex,
        onSetBroadcastRepeatIndex = viewModel::setBroadcastRepeatIndex,
        onSetSnapshotAutoRefresh = viewModel::setSnapshotAutoRefresh,
    )

    val mainPagerState = LocalMainPagerState.current
    SettingPagerMiuix(
        uiState = uiState,
        actions = actions,
        bottomInnerPadding = bottomInnerPadding,
        editUrlRequest = mainPagerState.pendingSettingsEdit == "url",
        onEditUrlHandled = { mainPagerState.clearSettingsEdit() },
    )
}
