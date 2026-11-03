package com.remotebroadcast.ui.component.miuix

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Edit
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import com.remotebroadcast.R
import top.yukonga.miuix.kmp.basic.ButtonDefaults
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.TextButton
import top.yukonga.miuix.kmp.basic.TextField
import top.yukonga.miuix.kmp.preference.ArrowPreference
import top.yukonga.miuix.kmp.theme.MiuixTheme
import top.yukonga.miuix.kmp.window.WindowDialog

/**
 * A preference row that only shows the stored value until tapped; editing happens in a dialog so a
 * stray touch cannot drop garbage into the address or token field.
 */
@Composable
fun TextEditPreference(
    title: String,
    value: String,
    placeholder: String,
    onSave: (String) -> Unit,
    icon: ImageVector = Icons.Rounded.Edit,
    summaryWhenEmpty: String? = null,
    openRequest: Boolean = false,
    onOpenRequestHandled: () -> Unit = {},
) {
    var editing by rememberSaveable { mutableStateOf(false) }

    // 从别处跳过来时直接把编辑框拉起来，省得用户自己在设置里找。
    LaunchedEffect(openRequest) {
        if (openRequest) {
            editing = true
            onOpenRequestHandled()
        }
    }

    ArrowPreference(
        title = title,
        summary = value.ifBlank { summaryWhenEmpty ?: placeholder },
        startAction = {
            Icon(
                icon,
                modifier = Modifier.padding(end = 6.dp),
                contentDescription = title,
                tint = MiuixTheme.colorScheme.onBackground,
            )
        },
        onClick = { editing = true },
    )

    if (editing) {
        TextEditDialog(
            title = title,
            initialValue = value,
            onDismiss = { editing = false },
            onSave = {
                editing = false
                onSave(it)
            },
        )
    }
}

@Composable
fun TextEditDialog(
    title: String,
    initialValue: String,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
) {
    var draft by rememberSaveable(initialValue) { mutableStateOf(initialValue) }
    val focusRequester = remember { FocusRequester() }
    val keyboard = LocalSoftwareKeyboardController.current

    LaunchedEffect(Unit) {
        focusRequester.requestFocus()
        keyboard?.show()
    }

    WindowDialog(
        show = true,
        title = title,
        onDismissRequest = onDismiss,
        content = {
            Column {
                TextField(
                    value = draft,
                    onValueChange = { draft = it },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 12.dp)
                        .focusRequester(focusRequester),
                    label = title,
                    useLabelAsPlaceholder = true,
                )
                Row(
                    horizontalArrangement = Arrangement.SpaceBetween,
                    modifier = Modifier
                        .padding(top = 8.dp)
                        .fillMaxWidth(),
                ) {
                    TextButton(
                        text = stringResource(android.R.string.cancel),
                        onClick = onDismiss,
                        modifier = Modifier.weight(1f),
                    )
                    Spacer(Modifier.width(20.dp))
                    TextButton(
                        text = stringResource(R.string.save),
                        onClick = { onSave(draft.trim()) },
                        modifier = Modifier.weight(1f),
                        colors = ButtonDefaults.textButtonColorsPrimary(),
                    )
                }
            }
        },
    )
}
