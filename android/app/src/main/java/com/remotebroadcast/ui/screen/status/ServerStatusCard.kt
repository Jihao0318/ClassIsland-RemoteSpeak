package com.remotebroadcast.ui.screen.status

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Cancel
import androidx.compose.material.icons.rounded.CheckCircleOutline
import androidx.compose.material.icons.rounded.HelpOutline
import androidx.compose.material.icons.rounded.Sync
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.remotebroadcast.R
import com.remotebroadcast.ui.component.WarningLevel
import com.remotebroadcast.ui.component.miuix.WarningCard
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.CardDefaults
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.Text
import com.remotebroadcast.ui.theme.isInDarkTheme
import top.yukonga.miuix.kmp.theme.MiuixTheme
import top.yukonga.miuix.kmp.theme.MiuixTheme.colorScheme
import top.yukonga.miuix.kmp.theme.MiuixTheme.isDynamicColor
import top.yukonga.miuix.kmp.utils.PressFeedbackType

@Composable
fun stringForConnection(status: ConnectionStatus): String = stringResource(
    when (status) {
        ConnectionStatus.ONLINE -> R.string.status_online
        ConnectionStatus.OFFLINE -> R.string.status_offline
        ConnectionStatus.CHECKING -> R.string.status_checking
        ConnectionStatus.UNKNOWN -> R.string.status_not_checked
    }
)

@Composable
fun connectionHeadline(state: ServerStatusUiState): String = stringResource(
    when (state.connection) {
        ConnectionStatus.ONLINE -> R.string.status_server_online
        ConnectionStatus.OFFLINE -> R.string.status_server_offline
        ConnectionStatus.CHECKING -> R.string.status_checking_server
        ConnectionStatus.UNKNOWN -> R.string.status_not_checked_yet
    }
)

private data class StatusPalette(val container: Color, val icon: Color, val text: Color)

@Composable
private fun statusPalette(status: ConnectionStatus): StatusPalette? {
    val dynamic = isDynamicColor
    val dark = isInDarkTheme()

    return when (status) {
        // 开了 Monet 就跟主题容器色走；没开就回到原来的绿/红，别把静态配色也改掉。
        ConnectionStatus.ONLINE -> when {
            dynamic -> StatusPalette(
                container = colorScheme.secondaryContainer,
                icon = colorScheme.primary,
                text = colorScheme.onSecondaryContainer,
            )
            dark -> StatusPalette(Color(0xFF12381C), Color(0xFF36D167), Color(0xFFB7F7C6))
            else -> StatusPalette(Color(0xFFDFFAE4), Color(0xFF36D167), Color(0xFF111111))
        }

        ConnectionStatus.OFFLINE -> when {
            dynamic -> StatusPalette(
                container = colorScheme.errorContainer,
                icon = colorScheme.error,
                text = colorScheme.onErrorContainer,
            )
            dark -> StatusPalette(Color(0xFF310808), Color(0xFFF72727), Color(0xFFF2B8B5))
            else -> StatusPalette(Color(0xFFF8E2E2), Color(0xFFF72727), Color(0xFF111111))
        }

        else -> null
    }
}

@Composable
private fun statusIcon(status: ConnectionStatus): ImageVector = when (status) {
    ConnectionStatus.ONLINE -> Icons.Rounded.CheckCircleOutline
    ConnectionStatus.OFFLINE -> Icons.Rounded.Cancel
    ConnectionStatus.CHECKING -> Icons.Rounded.Sync
    ConnectionStatus.UNKNOWN -> Icons.Rounded.HelpOutline
}

/** Green tick when the server answers, red cross when it does not, plain card before the first probe. */
@Composable
fun ServerStatusCard(
    state: ServerStatusUiState,
    version: String,
    onClick: () -> Unit,
) {
    val palette = statusPalette(state.connection)
    val container = palette?.container ?: MiuixTheme.colorScheme.surface
    val text = palette?.text ?: MiuixTheme.colorScheme.onSurface

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.defaultColors(color = container, contentColor = text),
        onClick = onClick,
        showIndication = true,
        pressFeedbackType = PressFeedbackType.Tilt,
    ) {
        Box {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .offset(27.dp, 31.dp),
                contentAlignment = Alignment.BottomEnd,
            ) {
                Icon(
                    modifier = Modifier.size(110.dp),
                    imageVector = statusIcon(state.connection),
                    tint = palette?.icon ?: MiuixTheme.colorScheme.onSurfaceVariantSummary,
                    contentDescription = null,
                )
            }
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(16.dp, 14.dp),
                contentAlignment = Alignment.TopStart,
            ) {
                Column {
                    Text(
                        text = connectionHeadline(state),
                        fontSize = 22.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = text,
                    )
                    Spacer(Modifier.height(1.dp))
                    Text(
                        text = version,
                        fontSize = 15.sp,
                        color = text,
                    )
                }
            }
        }
    }
}

@Composable
fun ConnectionAdviceCard(state: ServerStatusUiState) {
    when {
        state.tokenMissing -> WarningCard(
            message = stringResource(R.string.status_token_missing_hint),
            level = WarningLevel.Error,
        )

        state.usesCleartext -> WarningCard(
            message = stringResource(R.string.status_cleartext_warning),
            level = WarningLevel.Notice,
        )
    }
}
