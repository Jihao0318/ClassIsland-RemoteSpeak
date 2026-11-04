package com.remotebroadcast.ui.screen.settings

import androidx.compose.runtime.Immutable
import com.materialkolor.PaletteStyle
import com.materialkolor.dynamiccolor.ColorSpec
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.ui.screen.broadcast.BroadcastTriggerMode

@Immutable
data class SettingsUiState(
    val checkUpdate: Boolean = true,
    val themeMode: Int = 0,
    val miuixMonet: Boolean = false,
    val keyColor: Int = 0,
    val colorStyle: String = PaletteStyle.TonalSpot.name,
    val colorSpec: String = ColorSpec.SpecVersion.Default.name,
    val enablePredictiveBack: Boolean = true,
    val enableSwipeDismiss: Boolean = true,
    val pagerInterceptionMode: Int = 1,
    val enableBlur: Boolean = true,
    val enableFloatingBottomBar: Boolean = true,
    val enableFloatingBottomBarBlur: Boolean = true,
    val pageScale: Float = 1.0f,
    val broadcastUrl: String = "",
    val broadcastToken: String = "",
    val broadcastRepeat: Int = BroadcastApi.MIN_REPEAT,
    val broadcastTriggerMode: Int = BroadcastTriggerMode.CONFIRM.value,
    val snapshotAutoRefresh: Boolean = false,
    val snapshotAllowed: Boolean = true,
)

@Immutable
data class SettingsScreenActions(
    val onSetCheckUpdate: (Boolean) -> Unit,
    val onOpenTheme: () -> Unit,
    val onOpenAbout: () -> Unit,
    val onSetBroadcastUrl: (String) -> Unit,
    val onSetBroadcastToken: (String) -> Unit,
    val onSetBroadcastTriggerIndex: (Int) -> Unit,
    val onSetBroadcastRepeatIndex: (Int) -> Unit,
    val onSetSnapshotAutoRefresh: (Boolean) -> Unit,
)
