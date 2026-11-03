package com.remotebroadcast.ui.screen.broadcast

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.calculatePan
import androidx.compose.foundation.gestures.calculateZoom
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Close
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.remotebroadcast.R
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.Text

private const val MIN_SCALE = 1f
private const val MAX_SCALE = 5f

/**
 * Full-frame viewer. Pinch to zoom, drag to pan once zoomed, double-tap to toggle,
 * and an explicit close button — a bare tap must never dismiss it, because panning
 * and tapping are indistinguishable enough to lose the frame by accident.
 */
@Composable
fun SnapshotViewer(
    frame: SnapshotView,
    onDismiss: () -> Unit,
) {
    var scale by remember { mutableFloatStateOf(MIN_SCALE) }
    var offset by remember { mutableStateOf(Offset.Zero) }
    var viewport by remember { mutableStateOf(IntSize.Zero) }

    fun clampAndApply(nextScale: Float, nextOffset: Offset) {
        val clamped = nextScale.coerceIn(MIN_SCALE, MAX_SCALE)
        val boundX = (clamped - 1f) * viewport.width / 2f
        val boundY = (clamped - 1f) * viewport.height / 2f
        scale = clamped
        offset = if (clamped <= MIN_SCALE) {
            Offset.Zero
        } else {
            Offset(
                nextOffset.x.coerceIn(-boundX, boundX),
                nextOffset.y.coerceIn(-boundY, boundY),
            )
        }
    }

    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false),
    ) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black)
                .onSizeChanged { viewport = it },
            contentAlignment = Alignment.Center,
        ) {
            Image(
                bitmap = frame.image,
                contentDescription = stringResource(R.string.broadcast_snapshot),
                modifier = Modifier
                    .fillMaxSize()
                    .graphicsLayer {
                        scaleX = scale
                        scaleY = scale
                        translationX = offset.x
                        translationY = offset.y
                    }
                    .pointerInput(frame) {
                        awaitEachGesture {
                            awaitFirstDown(requireUnconsumed = false)
                            do {
                                val event = awaitPointerEvent()
                                val zoom = event.calculateZoom()
                                val pan = event.calculatePan()
                                if (zoom != 1f || pan != Offset.Zero) {
                                    clampAndApply(scale * zoom, offset + pan)
                                    event.changes.forEach { it.consume() }
                                }
                            } while (event.changes.any { it.pressed })
                        }
                    }
                    .pointerInput(frame) {
                        detectTapGestures(
                            onDoubleTap = {
                                clampAndApply(
                                    if (scale > MIN_SCALE) MIN_SCALE else 2.5f,
                                    Offset.Zero,
                                )
                            },
                        )
                    },
                contentScale = ContentScale.Fit,
            )

            IconButton(
                onClick = onDismiss,
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .padding(12.dp)
                    .size(40.dp),
            ) {
                Icon(
                    imageVector = Icons.Rounded.Close,
                    contentDescription = stringResource(R.string.broadcast_snapshot_close),
                    tint = Color.White,
                )
            }

            Text(
                text = stringResource(R.string.broadcast_snapshot_zoom_hint),
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(bottom = 28.dp),
                color = Color.White.copy(alpha = 0.7f),
            )
        }
    }
}
