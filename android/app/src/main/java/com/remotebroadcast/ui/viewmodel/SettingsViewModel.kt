package com.remotebroadcast.ui.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.withContext
import kotlinx.coroutines.launch
import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.data.remote.BroadcastOutcome
import com.remotebroadcast.data.repository.SettingsRepository
import com.remotebroadcast.data.repository.SettingsRepositoryImpl
import com.remotebroadcast.ui.screen.broadcast.BroadcastTriggerMode
import com.remotebroadcast.ui.screen.settings.SettingsUiState
import com.remotebroadcast.BuildConfig
import com.remotebroadcast.ui.theme.ColorMode
import com.remotebroadcast.ui.util.checkNewVersion
import com.remotebroadcast.ui.util.downloadApk
import com.remotebroadcast.ui.util.installApk

class SettingsViewModel(
    private val repo: SettingsRepository = SettingsRepositoryImpl()
) : ViewModel() {

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            val checkUpdate = repo.checkUpdate
            val themeMode = repo.themeMode
            val miuixMonet = repo.miuixMonet
            val keyColor = repo.keyColor
            val enablePredictiveBack = repo.enablePredictiveBack
            val enableSwipeDismiss = repo.enableSwipeDismiss
            val pagerInterceptionMode = repo.pagerInterceptionMode
            val enableBlur = repo.enableBlur
            val enableFloatingBottomBar = repo.enableFloatingBottomBar
            val enableFloatingBottomBarBlur = repo.enableFloatingBottomBarBlur
            val pageScale = repo.pageScale
            val colorStyle = repo.colorStyle
            val colorSpec = repo.colorSpec
            val broadcastUrl = repo.broadcastUrl
            val broadcastToken = repo.broadcastToken
            val broadcastRepeat = repo.broadcastRepeat
            val broadcastTriggerMode = repo.broadcastTriggerMode
            val snapshotAutoRefresh = repo.snapshotAutoRefresh

            _uiState.update {
                it.copy(
                    checkUpdate = checkUpdate,
                    themeMode = themeMode,
                    miuixMonet = miuixMonet,
                    keyColor = keyColor,
                    enablePredictiveBack = enablePredictiveBack,
                    enableSwipeDismiss = enableSwipeDismiss,
                    pagerInterceptionMode = pagerInterceptionMode,
                    enableBlur = enableBlur,
                    enableFloatingBottomBar = enableFloatingBottomBar,
                    enableFloatingBottomBarBlur = enableFloatingBottomBarBlur,
                    pageScale = pageScale,
                    colorStyle = colorStyle,
                    colorSpec = colorSpec,
                    broadcastUrl = broadcastUrl,
                    broadcastToken = broadcastToken,
                    broadcastRepeat = broadcastRepeat,
                    broadcastTriggerMode = broadcastTriggerMode,
                    snapshotAutoRefresh = snapshotAutoRefresh,
                )
            }

            // 没有取景权限时，「画面自动刷新」要灰掉而不是消失。
            val outcome = withContext(Dispatchers.IO) {
                BroadcastApi.permissions(broadcastUrl, broadcastToken)
            }
            if (outcome is BroadcastOutcome.Success) {
                _uiState.update { it.copy(snapshotAllowed = outcome.value.snapshot) }
            }
        }
    }

    fun checkUpdateNow() {
        viewModelScope.launch {
            _uiState.update { it.copy(updateMessage = "正在检查…") }
            val info = withContext(Dispatchers.IO) { checkNewVersion() }
            if (info.versionCode <= BuildConfig.VERSION_CODE) {
                _uiState.update { it.copy(updateMessage = "已经是最新版本") }
                return@launch
            }

            _uiState.update { it.copy(updateMessage = "发现新版本 ${info.versionCode}，正在下载…") }
            val file = withContext(Dispatchers.IO) { downloadApk(info.downloadUrl) {} }
            if (file == null) {
                _uiState.update { it.copy(updateMessage = "下载失败，请稍后再试") }
                return@launch
            }

            _uiState.update { it.copy(updateMessage = null) }
            runCatching { installApk(file) }
                .onFailure { _uiState.update { it.copy(updateMessage = "打不开安装器：${it.message}") } }
        }
    }

    fun setCheckUpdate(enabled: Boolean) {
        repo.checkUpdate = enabled
        _uiState.update { it.copy(checkUpdate = enabled) }
    }

    fun setThemeMode(mode: Int) {
        val effectiveMode = if (_uiState.value.miuixMonet && mode < 3) mode + 3 else mode
        repo.themeMode = effectiveMode
        _uiState.update { it.copy(themeMode = effectiveMode) }
    }

    fun setColorMode(mode: ColorMode) {
        repo.themeMode = mode.value
        _uiState.update { it.copy(themeMode = mode.value) }
    }

    fun setMiuixMonet(enabled: Boolean) {
        val currentThemeMode = repo.themeMode
        val colorMode = ColorMode.fromValue(currentThemeMode)
        val newThemeMode = if (enabled) {
            if (!colorMode.isMonet) colorMode.toMonetMode() else currentThemeMode
        } else {
            if (colorMode.isMonet) colorMode.toNonMonetMode() else currentThemeMode
        }
        repo.miuixMonet = enabled
        repo.themeMode = newThemeMode
        _uiState.update { it.copy(miuixMonet = enabled, themeMode = newThemeMode) }
    }

    fun setKeyColor(color: Int) {
        repo.keyColor = color
        _uiState.update { it.copy(keyColor = color) }
    }

    fun setColorStyle(style: String) {
        repo.colorStyle = style
        _uiState.update { it.copy(colorStyle = style) }
    }

    fun setColorSpec(spec: String) {
        repo.colorSpec = spec
        _uiState.update { it.copy(colorSpec = spec) }
    }

    fun setEnablePredictiveBack(enabled: Boolean) {
        repo.enablePredictiveBack = enabled
        _uiState.update { it.copy(enablePredictiveBack = enabled) }
    }

    fun setEnableSwipeDismiss(enabled: Boolean) {
        repo.enableSwipeDismiss = enabled
        _uiState.update { it.copy(enableSwipeDismiss = enabled) }
    }

    fun setPagerInterceptionMode(mode: Int) {
        val effectiveMode = mode.coerceIn(0, 2)
        repo.pagerInterceptionMode = effectiveMode
        _uiState.update { it.copy(pagerInterceptionMode = effectiveMode) }
    }

    fun setEnableBlur(enabled: Boolean) {
        repo.enableBlur = enabled
        _uiState.update { it.copy(enableBlur = enabled) }
    }

    fun setEnableFloatingBottomBar(enabled: Boolean) {
        repo.enableFloatingBottomBar = enabled
        _uiState.update { it.copy(enableFloatingBottomBar = enabled) }
    }

    fun setEnableFloatingBottomBarBlur(enabled: Boolean) {
        repo.enableFloatingBottomBarBlur = enabled
        _uiState.update { it.copy(enableFloatingBottomBarBlur = enabled) }
    }

    fun setPageScale(scale: Float) {
        repo.pageScale = scale
        _uiState.update { it.copy(pageScale = scale) }
    }

    fun setBroadcastUrl(value: String) {
        repo.broadcastUrl = value
        repo.broadcastSetupDone = true
        _uiState.update { it.copy(broadcastUrl = repo.broadcastUrl) }
    }

    fun setBroadcastToken(value: String) {
        repo.broadcastToken = value
        repo.broadcastSetupDone = true
        _uiState.update { it.copy(broadcastToken = repo.broadcastToken) }
    }

    fun setBroadcastTriggerIndex(index: Int) {
        val mode = BroadcastTriggerMode.entries.getOrElse(index) { BroadcastTriggerMode.CONFIRM }
        repo.broadcastTriggerMode = mode.value
        _uiState.update { it.copy(broadcastTriggerMode = repo.broadcastTriggerMode) }
    }

    fun setBroadcastRepeatIndex(index: Int) {
        repo.broadcastRepeat = BroadcastApi.MIN_REPEAT + index
        _uiState.update { it.copy(broadcastRepeat = repo.broadcastRepeat) }
    }

    fun setSnapshotAutoRefresh(enabled: Boolean) {
        repo.snapshotAutoRefresh = enabled
        _uiState.update { it.copy(snapshotAutoRefresh = enabled) }
    }

}
