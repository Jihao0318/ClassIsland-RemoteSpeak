package com.remotebroadcast.ui.component.miuix

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.theme.MiuixTheme

/** A label with its value on the next line, so long addresses wrap instead of clipping. */
@Composable
fun InfoRow(
    label: String,
    value: String,
    isWarning: Boolean = false,
) {
    val colorScheme = MiuixTheme.colorScheme
    Column(modifier = Modifier.fillMaxWidth().padding(bottom = 14.dp)) {
        Text(
            text = label,
            fontSize = MiuixTheme.textStyles.body2.fontSize,
            color = colorScheme.onSurfaceVariantSummary,
        )
        Text(
            text = value,
            fontSize = MiuixTheme.textStyles.main.fontSize,
            color = if (isWarning) colorScheme.error else colorScheme.onSurface,
            modifier = Modifier.padding(top = 2.dp),
            maxLines = 4,
            overflow = TextOverflow.Ellipsis,
        )
    }
}
