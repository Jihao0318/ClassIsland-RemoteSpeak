package com.remotebroadcast.ui.screen.broadcast

import androidx.compose.foundation.Image
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.WindowInsetsSides
import androidx.compose.foundation.layout.add
import androidx.compose.foundation.layout.aspectRatio
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
import androidx.compose.material.icons.rounded.CameraAlt
import androidx.compose.material.icons.rounded.Fullscreen
import androidx.compose.material.icons.rounded.Numbers
import androidx.compose.material.icons.rounded.TipsAndUpdates
import androidx.compose.material.icons.rounded.VideocamOff
import androidx.compose.material.icons.rounded.Wifi
import androidx.compose.runtime.Composable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay
import com.remotebroadcast.R
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.ui.component.WarningLevel
import com.remotebroadcast.ui.component.dialog.rememberConfirmDialog
import com.remotebroadcast.ui.component.miuix.TextEditDialog
import com.remotebroadcast.ui.component.miuix.WarningCard
import com.remotebroadcast.ui.theme.LocalEnableBlur
import com.remotebroadcast.ui.util.BlurredBar
import com.remotebroadcast.ui.util.formatTimeOfDay
import com.remotebroadcast.ui.util.rememberBlurBackdrop
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TextButton
import top.yukonga.miuix.kmp.basic.TextField
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.blur.LayerBackdrop
import top.yukonga.miuix.kmp.blur.layerBackdrop
import top.yukonga.miuix.kmp.preference.ArrowPreference
import top.yukonga.miuix.kmp.preference.OverlayDropdownPreference
import top.yukonga.miuix.kmp.theme.MiuixTheme
import top.yukonga.miuix.kmp.theme.MiuixTheme.colorScheme
import top.yukonga.miuix.kmp.utils.overScrollVertical
import top.yukonga.miuix.kmp.utils.scrollEndHaptic

private const val LONG_PRESS_MILLIS = 500L

@Composable
fun BroadcastPagerMiuix(
    state: BroadcastUiState,
    actions: BroadcastActions,
    bottomInnerPadding: Dp,
) {
    val scrollBehavior = MiuixScrollBehavior()
    val enableBlur = LocalEnableBlur.current
    val backdrop = rememberBlurBackdrop(enableBlur)
    val barColor = if (backdrop != null) Color.Transparent else colorScheme.surface

    Scaffold(
        topBar = {
            BlurredBar(backdrop) {
                TopAppBar(
                    color = barColor,
                    title = stringResource(R.string.app_name),
                    scrollBehavior = scrollBehavior,
                )
            }
        },
        popupHost = { },
        contentWindowInsets = WindowInsets.systemBars.add(WindowInsets.displayCutout)
            .only(WindowInsetsSides.Horizontal),
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
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        if (state.showGuide) {
                            GuideBlock(state, actions)
                        } else {
                            if (state.baseUrl.isBlank()) {
                                WarningCard(
                                    message = stringResource(R.string.broadcast_url_missing_hint),
                                    level = WarningLevel.Error,
                                )
                            }
                            InputBlock(state, actions)
                            SendBlock(state, actions)
                            SnapshotBlock(state, actions)
                        }
                    }
                    Spacer(Modifier.height(bottomInnerPadding))
                }
            }
        }
    }
}

@Composable
private fun GuideBlock(state: BroadcastUiState, actions: BroadcastActions) {
    var url by rememberSaveable { mutableStateOf(state.baseUrl) }
    var token by rememberSaveable { mutableStateOf(state.token) }
    var urlMissing by rememberSaveable { mutableStateOf(false) }

    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Rounded.Wifi,
                    contentDescription = null,
                    tint = colorScheme.primary,
                    modifier = Modifier
                        .padding(end = 10.dp)
                        .size(28.dp),
                )
                Column {
                    Text(
                        text = stringResource(R.string.setup_title),
                        fontSize = MiuixTheme.textStyles.title4.fontSize,
                        fontWeight = FontWeight.SemiBold,
                        color = colorScheme.onSurface,
                    )
                    Text(
                        text = stringResource(R.string.setup_summary),
                        fontSize = MiuixTheme.textStyles.body2.fontSize,
                        color = colorScheme.onSurfaceVariantSummary,
                    )
                }
            }

            TextField(
                value = url,
                onValueChange = {
                    url = it
                    urlMissing = false
                },
                modifier = Modifier.fillMaxWidth(),
                label = stringResource(R.string.setup_url_label),
                useLabelAsPlaceholder = true,
            )
            if (urlMissing) {
                Text(
                    text = stringResource(R.string.setup_url_required),
                    fontSize = MiuixTheme.textStyles.body2.fontSize,
                    color = Color(0xFFD32F2F),
                    modifier = Modifier.padding(horizontal = 4.dp),
                )
            }

            TextField(
                value = token,
                onValueChange = { token = it },
                modifier = Modifier.fillMaxWidth(),
                label = stringResource(R.string.setup_token_label),
                useLabelAsPlaceholder = true,
            )
            Text(
                text = stringResource(R.string.setup_token_hint),
                fontSize = MiuixTheme.textStyles.body2.fontSize,
                color = colorScheme.onSurfaceVariantSummary,
                modifier = Modifier.padding(horizontal = 4.dp),
            )

            Button(
                onClick = {
                    if (url.isBlank()) {
                        urlMissing = true
                    } else {
                        actions.onSaveSetup(url, token)
                    }
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 4.dp),
            ) {
                Text(
                    text = stringResource(R.string.setup_save),
                    fontSize = MiuixTheme.textStyles.button.fontSize,
                    fontWeight = FontWeight.SemiBold,
                )
            }
            TextButton(
                text = stringResource(R.string.setup_skip),
                onClick = actions.onSkipGuide,
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

@Composable
private fun InputBlock(state: BroadcastUiState, actions: BroadcastActions) {
    Card(modifier = Modifier.fillMaxWidth()) {
        TextField(
            value = state.text,
            onValueChange = actions.onTextChanged,
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            label = stringResource(R.string.broadcast_input_label),
            minLines = 4,
            maxLines = 8,
        )

        val labels = repeatLabels()
        OverlayDropdownPreference(
            title = stringResource(R.string.broadcast_repeat),
            summary = stringResource(R.string.broadcast_repeat_summary),
            items = labels,
            startAction = {
                Icon(
                    Icons.Rounded.Numbers,
                    modifier = Modifier.padding(end = 6.dp),
                    contentDescription = stringResource(R.string.broadcast_repeat),
                    tint = colorScheme.onBackground,
                )
            },
            selectedIndex = (state.repeat - BroadcastApi.MIN_REPEAT).coerceIn(0, labels.lastIndex),
            onSelectedIndexChange = actions.onRepeatChange,
        )
    }
}

@Composable
private fun SendBlock(state: BroadcastUiState, actions: BroadcastActions) {
    var releaseTooEarly by rememberSaveable { mutableStateOf(false) }
    val confirmTitle = stringResource(R.string.broadcast_confirm_title)
    val confirmLabel = stringResource(R.string.confirm)
    val dialog = rememberConfirmDialog(onConfirm = actions.onSend)

    LaunchedEffect(releaseTooEarly) {
        if (releaseTooEarly) {
            delay(2500)
            releaseTooEarly = false
        }
    }

    fun requestSend() {
        if (!state.canSend) return
        if (state.confirmsBeforeSend) {
            dialog.showConfirm(
                title = confirmTitle,
                content = state.text,
                confirm = confirmLabel,
            )
        } else {
            actions.onSend()
        }
    }

    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        SendButton(
            enabled = state.canSend,
            sending = state.sendStatus is SendStatus.Sending,
            longPress = state.sendsOnLongPress,
            onSend = ::requestSend,
            onShortTap = { releaseTooEarly = true },
        )
        Text(
            text = stringResource(
                if (state.sendsOnLongPress) R.string.broadcast_long_press_hint
                else R.string.broadcast_interrupt_note
            ),
            fontSize = MiuixTheme.textStyles.body2.fontSize,
            color = colorScheme.onSurfaceVariantSummary,
            modifier = Modifier.padding(horizontal = 16.dp),
        )
        if (releaseTooEarly) {
            WarningCard(message = stringResource(R.string.broadcast_long_press_too_short))
        }
        when (val status = state.sendStatus) {
            is SendStatus.Sending -> WarningCard(
                message = stringResource(R.string.broadcast_sending),
                level = WarningLevel.Notice,
            )

            is SendStatus.Sent -> WarningCard(
                message = stringResource(R.string.broadcast_sent, status.repeat),
                level = WarningLevel.Notice,
            )

            is SendStatus.Rejected -> WarningCard(
                message = stringForFailure(status.failure) +
                    status.serverMessage?.let { " · $it" }.orEmpty(),
            )

            is SendStatus.Idle -> Unit
        }
    }
}

@Composable
private fun SendButton(
    enabled: Boolean,
    sending: Boolean,
    longPress: Boolean,
    onSend: () -> Unit,
    onShortTap: () -> Unit,
) {
    val interactionSource = remember { MutableInteractionSource() }
    val pressed by interactionSource.collectIsPressedAsState()
    var holdFired by remember { mutableStateOf(false) }

    // Releasing before the timeout cancels this effect, so only a real hold sends.
    LaunchedEffect(pressed) {
        if (pressed && longPress && enabled) {
            delay(LONG_PRESS_MILLIS)
            holdFired = true
            onSend()
        }
    }

    Button(
        onClick = {
            when {
                !longPress -> onSend()
                holdFired -> holdFired = false
                else -> onShortTap()
            }
        },
        modifier = Modifier.fillMaxWidth(),
        enabled = enabled,
        interactionSource = interactionSource,
    ) {
        Text(
            text = stringResource(if (sending) R.string.broadcast_sending else R.string.broadcast_send),
            fontSize = MiuixTheme.textStyles.button.fontSize,
            fontWeight = FontWeight.SemiBold,
        )
    }
}

@Composable
private fun SnapshotBlock(state: BroadcastUiState, actions: BroadcastActions) {
    val frame = state.snapshot

    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
        ) {
            Text(
                text = stringResource(R.string.broadcast_snapshot),
                fontSize = MiuixTheme.textStyles.title4.fontSize,
                fontWeight = FontWeight.SemiBold,
                color = colorScheme.onSurface,
            )
            // No empty box before the first capture: an unlabelled black rectangle reads as a bug.
            if (frame != null) {
                Spacer(Modifier.height(10.dp))
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .aspectRatio(16f / 9f)
                        .pointerInput(Unit) { detectTapGestures { actions.onOpenFullView() } },
                    contentAlignment = Alignment.Center,
                ) {
                    Image(
                        bitmap = frame.image,
                        contentDescription = stringResource(R.string.broadcast_snapshot),
                        modifier = Modifier.fillMaxWidth(),
                        contentScale = ContentScale.FillWidth,
                    )
                }
            }
            Spacer(Modifier.height(10.dp))
            Text(
                text = snapshotCaption(state),
                fontSize = MiuixTheme.textStyles.body2.fontSize,
                color = colorScheme.onSurfaceVariantSummary,
            )
        }

        ArrowPreference(
            title = stringResource(R.string.broadcast_snapshot_refresh),
            summary = stringResource(R.string.broadcast_snapshot_refresh_summary),
            startAction = {
                Icon(
                    Icons.Rounded.CameraAlt,
                    modifier = Modifier.padding(end = 6.dp),
                    contentDescription = stringResource(R.string.broadcast_snapshot_refresh),
                    tint = colorScheme.onBackground,
                )
            },
            onClick = actions.onSnapshotClick,
        )

        if (frame != null) {
            ArrowPreference(
                title = stringResource(R.string.broadcast_snapshot_full),
                startAction = {
                    Icon(
                        Icons.Rounded.Fullscreen,
                        modifier = Modifier.padding(end = 6.dp).size(24.dp),
                        contentDescription = stringResource(R.string.broadcast_snapshot_full),
                        tint = colorScheme.onBackground,
                    )
                },
                onClick = actions.onOpenFullView,
            )
        }
    }
}

@Composable
private fun snapshotCaption(state: BroadcastUiState): String {
    val frame = state.snapshot
    return listOfNotNull(
        frame?.let { stringResource(R.string.broadcast_snapshot_at, formatTimeOfDay(it.capturedAtMillis)) },
        frame?.let { "${it.byteSize / 1024} KB" },
        stringForSnapshotStatus(state.snapshotStatus, hasFrame = frame != null),
    ).joinToString(" · ").ifBlank { stringResource(R.string.broadcast_snapshot_idle) }
}
