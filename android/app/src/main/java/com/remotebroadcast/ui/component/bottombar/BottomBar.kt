package com.remotebroadcast.ui.component.bottombar

import androidx.compose.foundation.pager.PagerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.platform.LocalWindowInfo
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.job
import kotlinx.coroutines.launch
import top.yukonga.miuix.kmp.blur.Backdrop
import top.yukonga.miuix.kmp.blur.LayerBackdrop
import top.yukonga.miuix.kmp.utils.springAnimateToPage

class MainPagerState(
    val pagerState: PagerState,
    private val coroutineScope: CoroutineScope,
    private val animatePageChanges: Boolean,
) {
    var selectedPage by mutableIntStateOf(pagerState.currentPage)
        private set

    var isNavigating by mutableStateOf(false)
        private set

    private var navJob: Job? = null

    /** 首页点「去喊话」时置位，播报页切过去后自己取走并聚焦输入框。 */
    var broadcastFocusRequested by mutableStateOf(false)
        private set

    fun requestBroadcastFocus() {
        broadcastFocusRequested = true
    }

    fun consumeBroadcastFocus(): Boolean {
        if (!broadcastFocusRequested) return false
        broadcastFocusRequested = false
        return true
    }

    fun animateToPage(targetIndex: Int) {
        if (targetIndex == selectedPage) return

        navJob?.cancel()

        selectedPage = targetIndex
        isNavigating = true

        navJob = coroutineScope.launch {
            val myJob = coroutineContext.job
            try {
                if (animatePageChanges) {
                    pagerState.springAnimateToPage(targetIndex)
                } else {
                    pagerState.scrollToPage(targetIndex)
                }
            } finally {
                if (navJob == myJob) {
                    isNavigating = false
                    if (pagerState.currentPage != targetIndex) {
                        selectedPage = pagerState.currentPage
                    }
                }
            }
        }
    }

    fun syncPage() {
        if (!isNavigating && selectedPage != pagerState.currentPage) {
            selectedPage = pagerState.currentPage
        }
    }
}

@Composable
fun rememberMainPagerState(
    pagerState: PagerState,
    coroutineScope: CoroutineScope = rememberCoroutineScope(),
    animatePageChanges: Boolean = true,
): MainPagerState {
    return remember(pagerState, coroutineScope, animatePageChanges) {
        MainPagerState(pagerState, coroutineScope, animatePageChanges)
    }
}

@Composable
fun useNavigationRail(enableFloatingBottomBar: Boolean): Boolean {
    val windowSize = LocalWindowInfo.current.containerSize
    val deviceDensity = LocalResources.current.displayMetrics.density
    val widthDp = windowSize.width / deviceDensity
    val heightDp = windowSize.height / deviceDensity
    val wideWindow = widthDp >= 840f || (widthDp >= 600f && heightDp / widthDp < 1.2f)
    return wideWindow && !enableFloatingBottomBar
}

@Composable
fun BottomBar(
    blurBackdrop: LayerBackdrop?,
    backdrop: Backdrop,
    modifier: Modifier = Modifier,
) {
    BottomBarMiuix(blurBackdrop, backdrop, modifier)
}

@Composable
fun SideRail(
    modifier: Modifier = Modifier,
) {
    NavigationRailMiuix(modifier)
}
