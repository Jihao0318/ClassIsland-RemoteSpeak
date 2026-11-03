package com.remotebroadcast.ui.screen.home

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.unit.Dp
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.remotebroadcast.ui.LocalMainPagerState
import com.remotebroadcast.ui.navigation3.Navigator
import com.remotebroadcast.ui.viewmodel.HomeViewModel
import com.remotebroadcast.ui.viewmodel.ServerStatusViewModel

@Composable
fun HomePager(
    navigator: Navigator,
    bottomInnerPadding: Dp,
    isCurrentPage: Boolean = true
) {
    val viewModel = viewModel<HomeViewModel>()
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val statusViewModel = viewModel<ServerStatusViewModel>()
    val serverStatus by statusViewModel.uiState.collectAsStateWithLifecycle()
    val uriHandler = LocalUriHandler.current

    val latestIsCurrentPage by rememberUpdatedState(isCurrentPage)
    val initialResumeHandled = rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(isCurrentPage) {
        if (isCurrentPage) {
            viewModel.refresh()
            statusViewModel.syncConfig()
            statusViewModel.refreshQuietly()
        }
    }
    LifecycleResumeEffect(Unit) {
        if (initialResumeHandled.value && latestIsCurrentPage) {
            viewModel.refresh()
            statusViewModel.refreshQuietly()
        }
        initialResumeHandled.value = true
        onPauseOrDispose { }
    }

    val mainPagerState = LocalMainPagerState.current
    val actions = HomeActions(
        onServerStatusRefresh = {
            if (serverStatus.baseUrl.isBlank()) {
                mainPagerState.animateToPage(2)
            } else {
                statusViewModel.check(showBusy = true)
            }
        },
        onGoBroadcast = {
            mainPagerState.requestBroadcastFocus()
            mainPagerState.animateToPage(1)
        },
        onOpenUrl = uriHandler::openUri,
    )

    HomePagerMiuix(
        state = uiState,
        serverStatus = serverStatus,
        actions = actions,
        bottomInnerPadding = bottomInnerPadding,
    )
}
