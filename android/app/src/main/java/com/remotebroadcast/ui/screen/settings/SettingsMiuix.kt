package com.remotebroadcast.ui.screen.settings

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.WindowInsetsSides
import androidx.compose.foundation.layout.add
import androidx.compose.foundation.layout.displayCutout
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.only
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.systemBars
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.BugReport
import androidx.compose.material.icons.rounded.Campaign
import androidx.compose.material.icons.rounded.CloudDownload
import androidx.compose.material.icons.rounded.ContactPage
import androidx.compose.material.icons.rounded.Link
import androidx.compose.material.icons.rounded.Dashboard
import androidx.compose.material.icons.rounded.Numbers
import androidx.compose.material.icons.rounded.Palette
import androidx.compose.material.icons.rounded.TipsAndUpdates
import androidx.compose.material.icons.rounded.Update
import androidx.compose.material.icons.rounded.Videocam
import androidx.compose.runtime.Composable
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.remotebroadcast.R
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.ui.component.dialog.rememberLoadingDialog
import com.remotebroadcast.ui.component.miuix.SendLogDialog
import com.remotebroadcast.ui.component.miuix.TextEditPreference
import com.remotebroadcast.ui.screen.broadcast.BroadcastTriggerMode
import com.remotebroadcast.ui.screen.broadcast.repeatLabels
import com.remotebroadcast.ui.screen.broadcast.triggerModeLabels
import com.remotebroadcast.ui.theme.LocalEnableBlur
import com.remotebroadcast.ui.util.BlurredBar
import com.remotebroadcast.ui.util.rememberBlurBackdrop
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.blur.layerBackdrop
import top.yukonga.miuix.kmp.preference.ArrowPreference
import top.yukonga.miuix.kmp.preference.OverlayDropdownPreference
import top.yukonga.miuix.kmp.preference.SwitchPreference
import top.yukonga.miuix.kmp.theme.MiuixTheme.colorScheme
import top.yukonga.miuix.kmp.utils.overScrollVertical
import top.yukonga.miuix.kmp.utils.scrollEndHaptic

/**
 * @author weishu
 * @date 2023/1/1.
 */
@Composable
fun SettingPagerMiuix(
    uiState: SettingsUiState,
    actions: SettingsScreenActions,
    bottomInnerPadding: Dp,
    editUrlRequest: Boolean = false,
    onEditUrlHandled: () -> Unit = {},
) {
    val scrollBehavior = MiuixScrollBehavior()
    val enableBlur = LocalEnableBlur.current
    val backdrop = rememberBlurBackdrop(enableBlur)
    val blurActive = backdrop != null
    val barColor = if (blurActive) Color.Transparent else colorScheme.surface
    val loadingDialog = rememberLoadingDialog()
    val showSendLogDialog = rememberSaveable { mutableStateOf(false) }

    Scaffold(
        topBar = {
            BlurredBar(backdrop) {
                TopAppBar(
                    color = barColor,
                    title = stringResource(R.string.settings),
                    scrollBehavior = scrollBehavior
                )
            }
        },
        popupHost = { },
        contentWindowInsets = WindowInsets.systemBars.add(WindowInsets.displayCutout).only(WindowInsetsSides.Horizontal),
    ) { innerPadding ->
        Box(modifier = if (backdrop != null) Modifier.layerBackdrop(backdrop) else Modifier) {
            LazyColumn(
                modifier = Modifier
                    .fillMaxHeight()
                    .scrollEndHaptic()
                    .overScrollVertical()
                    .nestedScroll(scrollBehavior.nestedScrollConnection)
                    .padding(horizontal = 12.dp),
                contentPadding = innerPadding,
                overscrollEffect = null,
            ) {
                item {
                    Card(
                        modifier = Modifier
                            .padding(top = 12.dp)
                            .fillMaxWidth(),
                    ) {
                        SwitchPreference(
                            title = stringResource(id = R.string.settings_check_update),
                            summary = stringResource(id = R.string.settings_check_update_summary),
                            startAction = {
                                Icon(
                                    Icons.Rounded.Update,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = stringResource(id = R.string.settings_check_update),
                                    tint = colorScheme.onBackground
                                )
                            },
                            checked = uiState.checkUpdate,
                            onCheckedChange = actions.onSetCheckUpdate
                        )
                        OverlayDropdownPreference(
                            title = stringResource(id = R.string.settings_download_channel),
                            summary = stringResource(id = R.string.settings_download_channel_summary),
                            items = listOf("自动（测速选最快）", "GitHub 直连", "ghfast", "gh-proxy", "自定义"),
                            startAction = {
                                Icon(
                                    Icons.Rounded.CloudDownload,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = stringResource(id = R.string.settings_download_channel),
                                    tint = colorScheme.onBackground
                                )
                            },
                            selectedIndex = when (uiState.downloadChannel) {
                                "github" -> 1
                                "ghfast" -> 2
                                "gh-proxy" -> 3
                                "custom" -> 4
                                else -> 0
                            },
                            onSelectedIndexChange = actions.onSetDownloadChannelIndex
                        )
                        if (uiState.downloadChannel == "custom") {
                            TextEditPreference(
                                title = stringResource(id = R.string.settings_download_channel_custom),
                                value = uiState.downloadChannelCustom,
                                placeholder = "https://example.com/",
                                summaryWhenEmpty = stringResource(id = R.string.settings_download_channel_custom_summary),
                                icon = Icons.Rounded.Link,
                                onSave = actions.onSetDownloadChannelCustom,
                            )
                        }
                    }

                    Card(
                        modifier = Modifier
                            .padding(top = 12.dp)
                            .fillMaxWidth(),
                    ) {
                        ArrowPreference(
                            title = stringResource(id = R.string.settings_theme),
                            summary = stringResource(id = R.string.settings_theme_summary),
                            startAction = {
                                Icon(
                                    Icons.Rounded.Palette,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = stringResource(id = R.string.settings_theme),
                                    tint = colorScheme.onBackground
                                )
                            },
                            onClick = actions.onOpenTheme
                        )
                    }

                    Card(
                        modifier = Modifier
                            .padding(top = 12.dp)
                            .fillMaxWidth(),
                    ) {
                        TextEditPreference(
                            title = stringResource(id = R.string.broadcast_settings_url),
                            value = uiState.broadcastUrl,
                            placeholder = stringResource(id = R.string.broadcast_url_placeholder),
                            summaryWhenEmpty = stringResource(id = R.string.broadcast_url_unset),
                            icon = Icons.Rounded.Dashboard,
                            onSave = actions.onSetBroadcastUrl,
                            openRequest = editUrlRequest,
                            onOpenRequestHandled = onEditUrlHandled,
                        )
                        TextEditPreference(
                            title = stringResource(id = R.string.broadcast_settings_token),
                            value = uiState.broadcastToken,
                            placeholder = stringResource(id = R.string.token_placeholder),
                            summaryWhenEmpty = stringResource(id = R.string.broadcast_token_unset),
                            icon = Icons.Rounded.TipsAndUpdates,
                            onSave = actions.onSetBroadcastToken,
                        )
                        OverlayDropdownPreference(
                            title = stringResource(id = R.string.broadcast_settings_trigger),
                            summary = stringResource(id = R.string.broadcast_settings_trigger_summary),
                            items = triggerModeLabels(),
                            startAction = {
                                Icon(
                                    Icons.Rounded.Campaign,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = stringResource(id = R.string.broadcast_settings_trigger),
                                    tint = colorScheme.onBackground
                                )
                            },
                            selectedIndex = BroadcastTriggerMode.entries.indexOf(
                                BroadcastTriggerMode.fromValue(uiState.broadcastTriggerMode)
                            ),
                            onSelectedIndexChange = actions.onSetBroadcastTriggerIndex
                        )
                        OverlayDropdownPreference(
                            title = stringResource(id = R.string.broadcast_repeat),
                            summary = stringResource(id = R.string.broadcast_settings_repeat_summary),
                            items = repeatLabels(),
                            startAction = {
                                Icon(
                                    Icons.Rounded.Numbers,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = stringResource(id = R.string.broadcast_repeat),
                                    tint = colorScheme.onBackground
                                )
                            },
                            selectedIndex = (uiState.broadcastRepeat - BroadcastApi.MIN_REPEAT)
                                .coerceIn(0, repeatLabels().lastIndex),
                            onSelectedIndexChange = actions.onSetBroadcastRepeatIndex
                        )
                        SwitchPreference(
                            title = stringResource(id = R.string.broadcast_settings_auto_refresh),
                            summary = stringResource(id = R.string.broadcast_settings_auto_refresh_summary),
                            enabled = uiState.snapshotAllowed,
                            startAction = {
                                Icon(
                                    Icons.Rounded.Videocam,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = stringResource(id = R.string.broadcast_settings_auto_refresh),
                                    tint = colorScheme.onBackground
                                )
                            },
                            checked = uiState.snapshotAutoRefresh,
                            onCheckedChange = actions.onSetSnapshotAutoRefresh
                        )
                    }

                    Card(
                        modifier = Modifier
                            .padding(vertical = 12.dp)
                            .fillMaxWidth(),
                    ) {
                        ArrowPreference(
                            title = stringResource(id = R.string.send_log),
                            startAction = {
                                Icon(
                                    Icons.Rounded.BugReport,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = stringResource(id = R.string.send_log),
                                    tint = colorScheme.onBackground
                                )
                            },
                            onClick = { showSendLogDialog.value = true },
                        )
                        SendLogDialog(
                            show = showSendLogDialog.value,
                            onDismissRequest = { showSendLogDialog.value = false },
                            loadingDialog = loadingDialog
                        )
                        val about = stringResource(id = R.string.about)
                        ArrowPreference(
                            title = about,
                            startAction = {
                                Icon(
                                    Icons.Rounded.ContactPage,
                                    modifier = Modifier.padding(end = 6.dp),
                                    contentDescription = about,
                                    tint = colorScheme.onBackground
                                )
                            },
                            onClick = actions.onOpenAbout,
                        )
                    }
                    Spacer(Modifier.height(bottomInnerPadding))
                }
            }
        }
    }
}
