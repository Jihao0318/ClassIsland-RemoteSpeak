package com.remotebroadcast.ui.screen.status

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import com.remotebroadcast.R
import com.remotebroadcast.ui.component.miuix.InfoRow
import com.remotebroadcast.ui.screen.broadcast.stringForFailure
import com.remotebroadcast.ui.util.formatTimeOfDay
import top.yukonga.miuix.kmp.basic.Card

/**
 * The server verdict in list form: service name, address, connection, latency, last check and the
 * last failure. Shared by the home page and the standalone status page so both always agree.
 */
@Composable
fun ServerStatusDetails(state: ServerStatusUiState) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        ConnectionAdviceCard(state)

        Card(modifier = Modifier.fillMaxWidth()) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
            ) {
                InfoRow(
                    label = stringResource(R.string.status_service),
                    value = state.serviceName
                        ?: stringResource(R.string.status_service_unknown),
                )
                InfoRow(
                    label = stringResource(R.string.status_address),
                    value = state.baseUrl.ifBlank { stringResource(R.string.status_address_unset) },
                )
                InfoRow(
                    label = stringResource(R.string.status_connection),
                    value = stringForConnection(state.connection),
                    isWarning = state.connection == ConnectionStatus.OFFLINE,
                )
                state.latencyMs?.let { latency ->
                    InfoRow(
                        label = stringResource(R.string.status_latency),
                        value = stringResource(R.string.status_latency_value, latency),
                    )
                }
                InfoRow(
                    label = stringResource(R.string.status_last_check),
                    value = state.lastCheckedAtMillis
                        ?.let { formatTimeOfDay(it) }
                        ?: stringResource(R.string.status_never_checked),
                )
                state.failure?.let { failure ->
                    InfoRow(
                        label = stringResource(R.string.status_failure),
                        value = stringForFailure(failure) +
                            state.serverMessage?.let { " · $it" }.orEmpty(),
                        isWarning = true,
                    )
                }
            }
        }
    }
}
