package com.remotebroadcast.ui.screen.onboarding

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Campaign
import androidx.compose.material.icons.rounded.Key
import androidx.compose.material.icons.rounded.Wifi
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.remotebroadcast.R
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TextButton
import top.yukonga.miuix.kmp.basic.TextField
import top.yukonga.miuix.kmp.theme.MiuixTheme
import top.yukonga.miuix.kmp.theme.MiuixTheme.colorScheme

private const val STEP_COUNT = 3

/**
 * First-run wizard: welcome, server address, optional token. The app ships with no server of its
 * own, so this is the only place a new install can learn where to send broadcasts.
 */
@Composable
fun OnboardingScreen(
    onFinish: (url: String, token: String) -> Unit,
    onSkip: () -> Unit,
) {
    var step by rememberSaveable { mutableStateOf(0) }
    var url by rememberSaveable { mutableStateOf("") }
    var token by rememberSaveable { mutableStateOf("") }
    var urlMissing by rememberSaveable { mutableStateOf(false) }

    Scaffold { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(horizontal = 24.dp, vertical = 20.dp),
            verticalArrangement = Arrangement.SpaceBetween,
        ) {
            StepIndicator(step)

            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(16.dp),
            ) {
                when (step) {
                    0 -> WelcomeStep()
                    1 -> ServerStep(
                        url = url,
                        urlMissing = urlMissing,
                        onUrlChange = {
                            url = it
                            urlMissing = false
                        },
                    )
                    else -> TokenStep(
                        token = token,
                        onTokenChange = { token = it },
                    )
                }
            }

            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Button(
                    onClick = {
                        when (step) {
                            0 -> step = 1
                            1 -> if (url.isBlank()) urlMissing = true else step = 2
                            else -> onFinish(url, token)
                        }
                    },
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text(
                        text = stringResource(
                            when (step) {
                                0 -> R.string.onboarding_start
                                1 -> R.string.onboarding_next
                                else -> R.string.onboarding_finish
                            }
                        ),
                        fontSize = MiuixTheme.textStyles.button.fontSize,
                        fontWeight = FontWeight.SemiBold,
                    )
                }
                TextButton(
                    text = stringResource(
                        if (step == 0) R.string.onboarding_skip else R.string.onboarding_back
                    ),
                    onClick = {
                        if (step == 0) onSkip() else step -= 1
                    },
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        }
    }
}

@Composable
private fun StepIndicator(step: Int) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(6.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        repeat(STEP_COUNT) { index ->
            Box(
                modifier = Modifier
                    .size(if (index == step) 22.dp else 8.dp)
                    .clip(CircleShape)
                    .background(
                        if (index == step) colorScheme.primary
                        else colorScheme.onSurfaceVariantSummary.copy(alpha = 0.3f)
                    ),
            )
        }
    }
}

@Composable
private fun WelcomeStep() {
    Column(
        modifier = Modifier.fillMaxWidth(),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Icon(
            imageVector = Icons.Rounded.Campaign,
            contentDescription = null,
            tint = colorScheme.primary,
            modifier = Modifier.size(72.dp),
        )
        Text(
            text = stringResource(R.string.onboarding_welcome_title),
            fontSize = MiuixTheme.textStyles.title1.fontSize,
            fontWeight = FontWeight.SemiBold,
            color = colorScheme.onSurface,
            textAlign = TextAlign.Center,
        )
        Text(
            text = stringResource(R.string.onboarding_welcome_body),
            fontSize = MiuixTheme.textStyles.body2.fontSize,
            color = colorScheme.onSurfaceVariantSummary,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun ServerStep(
    url: String,
    urlMissing: Boolean,
    onUrlChange: (String) -> Unit,
) {
    StepHeader(
        icon = Icons.Rounded.Wifi,
        title = stringResource(R.string.onboarding_server_title),
        body = stringResource(R.string.onboarding_server_body),
    )
    TextField(
        value = url,
        onValueChange = onUrlChange,
        modifier = Modifier.fillMaxWidth(),
        label = stringResource(R.string.onboarding_server_label),
        useLabelAsPlaceholder = true,
    )
    if (urlMissing) {
        Text(
            text = stringResource(R.string.onboarding_url_required),
            fontSize = MiuixTheme.textStyles.body2.fontSize,
            color = colorScheme.error,
            modifier = Modifier.padding(horizontal = 4.dp),
        )
    }
}

@Composable
private fun TokenStep(
    token: String,
    onTokenChange: (String) -> Unit,
) {
    StepHeader(
        icon = Icons.Rounded.Key,
        title = stringResource(R.string.onboarding_token_title),
        body = stringResource(R.string.onboarding_token_body),
    )
    TextField(
        value = token,
        onValueChange = onTokenChange,
        modifier = Modifier.fillMaxWidth(),
        label = stringResource(R.string.onboarding_token_label),
        useLabelAsPlaceholder = true,
    )
    Text(
        text = stringResource(R.string.onboarding_token_hint),
        fontSize = MiuixTheme.textStyles.body2.fontSize,
        color = colorScheme.onSurfaceVariantSummary,
        modifier = Modifier.padding(horizontal = 4.dp),
    )
    Spacer(Modifier.height(4.dp))
}

@Composable
private fun StepHeader(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    title: String,
    body: String,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = colorScheme.primary,
            modifier = Modifier
                .padding(end = 12.dp)
                .size(36.dp),
        )
        Column {
            Text(
                text = title,
                fontSize = MiuixTheme.textStyles.title4.fontSize,
                fontWeight = FontWeight.SemiBold,
                color = colorScheme.onSurface,
            )
            Text(
                text = body,
                fontSize = MiuixTheme.textStyles.body2.fontSize,
                color = colorScheme.onSurfaceVariantSummary,
            )
        }
    }
}
