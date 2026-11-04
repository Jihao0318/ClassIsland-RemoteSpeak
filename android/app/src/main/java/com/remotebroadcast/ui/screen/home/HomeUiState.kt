package com.remotebroadcast.ui.screen.home

import androidx.compose.runtime.Immutable
import com.remotebroadcast.ui.util.LatestVersionInfo

@Immutable
data class HomeUiState(
    val checkUpdateEnabled: Boolean,
    val latestVersionInfo: LatestVersionInfo,
    val currentAppVersionCode: Long,
    val systemInfo: SystemInfo,
    val downloading: Boolean = false,
    val downloadProgress: Float = 0f,
    val downloadError: String? = null,
) {
    val hasUpdate: Boolean
        get() = latestVersionInfo.versionCode > currentAppVersionCode
}

@Immutable
data class HomeActions(
    val onServerStatusRefresh: () -> Unit,
    val onGoBroadcast: () -> Unit,
    val onDownloadUpdate: () -> Unit,
    val onOpenUrl: (String) -> Unit,
)
