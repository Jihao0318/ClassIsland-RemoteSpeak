package com.remotebroadcast.ui.viewmodel

import android.content.Context
import android.os.Build
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import com.remotebroadcast.templateApp
import com.remotebroadcast.ui.screen.home.HomeUiState
import com.remotebroadcast.ui.screen.home.SystemInfo
import com.remotebroadcast.ui.screen.home.getAppVersion
import com.remotebroadcast.ui.util.LatestVersionInfo
import com.remotebroadcast.ui.util.checkNewVersion
import com.remotebroadcast.ui.util.downloadApk
import com.remotebroadcast.ui.util.installApk
import com.remotebroadcast.ui.util.resolveDeviceName

class HomeViewModel : ViewModel() {

    private val _uiState = MutableStateFlow(buildState())
    val uiState: StateFlow<HomeUiState> = _uiState.asStateFlow()
    private var refreshJob: Job? = null

    fun downloadUpdate() {
        val info = _uiState.value.latestVersionInfo
        if (info.downloadUrl.isEmpty() || _uiState.value.downloading) return

        viewModelScope.launch {
            _uiState.update { it.copy(downloading = true, downloadProgress = 0f, downloadError = null) }
            val file = withContext(Dispatchers.IO) {
                downloadApk(info.downloadUrl) { progress ->
                    _uiState.update { it.copy(downloadProgress = progress) }
                }
            }
            _uiState.update { it.copy(downloading = false) }
            if (file == null) {
                _uiState.update { it.copy(downloadError = "更新包下载失败，请稍后再试") }
                return@launch
            }
            runCatching { installApk(file) }
                .onFailure { _uiState.update { it.copy(downloadError = "打不开安装器：${it.message}") } }
        }
    }

    fun refresh() {
        refreshJob?.cancel()
        refreshJob = viewModelScope.launch {
            val baseState = withContext(Dispatchers.IO) { buildState() }
            _uiState.update { baseState }
            if (baseState.checkUpdateEnabled) {
                val latestVersionInfo = withContext(Dispatchers.IO) { checkNewVersion() }
                _uiState.update { it.copy(latestVersionInfo = latestVersionInfo) }
            }
        }
    }

    private fun buildState(): HomeUiState {
        val appVersion = getAppVersion(templateApp)

        return HomeUiState(
            checkUpdateEnabled = templateApp.getSharedPreferences("settings", Context.MODE_PRIVATE)
                .getBoolean("check_update", true),
            latestVersionInfo = LatestVersionInfo(),
            currentAppVersionCode = appVersion.versionCode,
            systemInfo = SystemInfo(
                appVersion = "${appVersion.versionName} (${appVersion.versionCode})",
                deviceModel = resolveDeviceName(),
                androidVersion = "${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})",
            ),
        )
    }
}
