package com.remotebroadcast.ui.screen.home

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
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
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.systemBars
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.MenuBook
import androidx.compose.material.icons.filled.Android
import androidx.compose.material.icons.filled.Smartphone
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.remotebroadcast.R
import com.remotebroadcast.ui.component.WarningLevel
import com.remotebroadcast.ui.component.dialog.rememberConfirmDialog
import com.remotebroadcast.ui.component.miuix.WarningCard
import com.remotebroadcast.ui.screen.status.ServerStatusCard
import com.remotebroadcast.ui.screen.status.ServerStatusDetails
import com.remotebroadcast.ui.screen.status.ServerStatusUiState
import com.remotebroadcast.ui.theme.LocalEnableBlur
import com.remotebroadcast.ui.util.BlurredBar
import com.remotebroadcast.ui.util.rememberBlurBackdrop
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.ScrollBehavior
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.blur.LayerBackdrop
import top.yukonga.miuix.kmp.blur.layerBackdrop
import top.yukonga.miuix.kmp.preference.ArrowPreference
import top.yukonga.miuix.kmp.theme.MiuixTheme
import top.yukonga.miuix.kmp.theme.MiuixTheme.colorScheme
import top.yukonga.miuix.kmp.utils.overScrollVertical
import top.yukonga.miuix.kmp.utils.scrollEndHaptic

@Composable
fun HomePagerMiuix(
    state: HomeUiState,
    serverStatus: ServerStatusUiState,
    actions: HomeActions,
    bottomInnerPadding: Dp,
) {
    val scrollBehavior = MiuixScrollBehavior()
    val enableBlur = LocalEnableBlur.current
    val backdrop = rememberBlurBackdrop(enableBlur)
    val blurActive = backdrop != null
    val barColor = if (blurActive) Color.Transparent else colorScheme.surface
    Scaffold(
        topBar = {
            TopBar(
                scrollBehavior = scrollBehavior,
                backdrop = backdrop,
                barColor = barColor,
            )
        },
        popupHost = { },
        contentWindowInsets = WindowInsets.systemBars.add(WindowInsets.displayCutout).only(WindowInsetsSides.Horizontal)
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
                    Column(
                        modifier = Modifier.padding(vertical = 12.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        // Keep the theme settings preview in sync whenever this home layout changes.
                        if (state.checkUpdateEnabled) {
                            UpdateCard(state, actions)
                        }
                        ServerStatusCard(
                            state = serverStatus,
                            version = state.systemInfo.appVersion,
                            onClick = actions.onServerStatusRefresh,
                        )
                        Button(
                            onClick = actions.onGoBroadcast,
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Text(
                                text = stringResource(R.string.home_go_broadcast),
                                fontSize = MiuixTheme.textStyles.button.fontSize,
                                fontWeight = FontWeight.SemiBold,
                            )
                        }
                        ServerStatusDetails(serverStatus)
                        InfoCard(systemInfo = state.systemInfo)
                    }
                    Spacer(Modifier.height(bottomInnerPadding))
                }
            }
        }
    }
}

@Composable
private fun UpdateCard(state: HomeUiState, actions: HomeActions) {
    val newVersion = state.latestVersionInfo
    val title = stringResource(R.string.update_changelog)
    val updateText = stringResource(R.string.update)
    val updateDialog = rememberConfirmDialog(onConfirm = { actions.onOpenUrl(newVersion.downloadUrl) })

    AnimatedVisibility(
        visible = state.hasUpdate,
        enter = fadeIn() + expandVertically(),
        exit = shrinkVertically() + fadeOut(),
    ) {
        WarningCard(
            message = stringResource(R.string.new_version_available, newVersion.versionCode),
            level = WarningLevel.Notice,
            onClick = {
                if (newVersion.changelog.isEmpty()) {
                    actions.onOpenUrl(newVersion.downloadUrl)
                } else {
                    updateDialog.showConfirm(
                        title = title,
                        content = newVersion.changelog,
                        markdown = true,
                        confirm = updateText,
                    )
                }
            },
        )
    }
}

@Composable
private fun TopBar(
    scrollBehavior: ScrollBehavior,
    backdrop: LayerBackdrop?,
    barColor: Color,
) {
    BlurredBar(backdrop) {
        TopAppBar(
            color = barColor,
            title = stringResource(R.string.app_name),
            scrollBehavior = scrollBehavior
        )
    }
}

@Composable
private fun InfoCard(systemInfo: SystemInfo) {
    @Composable
    fun InfoText(
        icon: ImageVector,
        title: String,
        content: String,
        bottomPadding: Dp = 24.dp
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(bottom = bottomPadding),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                modifier = Modifier.padding(end = 12.dp).size(24.dp),
                tint = colorScheme.onSurface,
            )
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = title,
                    fontSize = MiuixTheme.textStyles.headline1.fontSize,
                    fontWeight = FontWeight.Medium,
                    color = colorScheme.onSurface,
                )
                Text(
                    text = content,
                    fontSize = MiuixTheme.textStyles.body2.fontSize,
                    color = colorScheme.onSurfaceVariantSummary,
                    modifier = Modifier.padding(top = 2.dp),
                )
            }
        }
    }

    Card {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp)
        ) {
            InfoText(
                icon = Icons.Filled.Smartphone,
                title = stringResource(R.string.home_device_model),
                content = systemInfo.deviceModel,
            )
            InfoText(
                icon = Icons.Filled.Android,
                title = stringResource(R.string.home_android_version),
                content = systemInfo.androidVersion,
                bottomPadding = 0.dp
            )
        }
    }
}
