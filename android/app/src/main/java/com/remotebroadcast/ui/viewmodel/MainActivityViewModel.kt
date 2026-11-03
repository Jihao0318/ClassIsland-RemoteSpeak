package com.remotebroadcast.ui.viewmodel

import android.content.Context
import android.content.SharedPreferences
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import com.remotebroadcast.data.repository.SettingsRepository
import com.remotebroadcast.data.repository.SettingsRepositoryImpl
import com.remotebroadcast.templateApp
import com.remotebroadcast.ui.theme.ThemeController

class MainActivityViewModel(
    savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val prefs = templateApp.getSharedPreferences("settings", Context.MODE_PRIVATE)
    private val settingRepo: SettingsRepository = SettingsRepositoryImpl()
    private val mainPageState = MainPageState(savedStateHandle)
    private val listener = SharedPreferences.OnSharedPreferenceChangeListener { _, key ->
        if (key == null || key in observedKeys) {
            _uiState.value = readUiState()
        }
    }

    private val _uiState = MutableStateFlow(readUiState())
    val uiState: StateFlow<MainActivityUiState> = _uiState.asStateFlow()
    val selectedMainPage: StateFlow<Int> = mainPageState.selectedPage

    init {
        prefs.registerOnSharedPreferenceChangeListener(listener)
    }

    override fun onCleared() {
        prefs.unregisterOnSharedPreferenceChangeListener(listener)
    }

    fun setSelectedMainPage(page: Int) {
        mainPageState.updateSelectedPage(page)
    }

    /** 首次向导完成后直接落到喊话页，省得用户还要自己往右划。 */
    fun completeOnboarding(url: String, token: String) {
        settingRepo.broadcastUrl = url
        settingRepo.broadcastToken = token
        settingRepo.broadcastSetupDone = true
        mainPageState.updateSelectedPage(1)
    }

    fun skipOnboarding() {
        settingRepo.broadcastSetupDone = true
    }

    private fun readUiState(): MainActivityUiState {
        return MainActivityUiState(
            appSettings = ThemeController.getAppSettings(templateApp),
            pageScale = settingRepo.pageScale,
            enableBlur = settingRepo.enableBlur,
            enableFloatingBottomBar = settingRepo.enableFloatingBottomBar,
            enableFloatingBottomBarBlur = settingRepo.enableFloatingBottomBarBlur,
            enableSwipeDismiss = settingRepo.enableSwipeDismiss,
            pagerInterceptionMode = settingRepo.pagerInterceptionMode,
            onboardingDone = settingRepo.broadcastSetupDone,
        )
    }

    private companion object {
        val observedKeys = setOf(
            "color_mode",
            "key_color",
            "color_style",
            "color_spec",
            "page_scale",
            "enable_blur",
            "enable_floating_bottom_bar",
            "enable_floating_bottom_bar_blur",
            "enable_swipe_dismiss",
            "pager_interception_mode",
            "broadcast_setup_done",
        )
    }
}

private const val SELECTED_MAIN_PAGE_KEY = "selected_main_page"

private class MainPageState(
    private val savedStateHandle: SavedStateHandle,
) {
    init {
        val savedPage = savedStateHandle.get<Int>(SELECTED_MAIN_PAGE_KEY) ?: 0
        savedStateHandle[SELECTED_MAIN_PAGE_KEY] = MainPagerConfig.coercePage(savedPage)
    }

    val selectedPage: StateFlow<Int> = savedStateHandle.getStateFlow(SELECTED_MAIN_PAGE_KEY, 0)

    fun updateSelectedPage(page: Int) {
        savedStateHandle[SELECTED_MAIN_PAGE_KEY] = MainPagerConfig.coercePage(page)
    }
}

object MainPagerConfig {
    const val PAGE_COUNT = 3
    const val LAST_PAGE_INDEX = PAGE_COUNT - 1

    fun coercePage(page: Int): Int = page.coerceIn(0, LAST_PAGE_INDEX)
}
