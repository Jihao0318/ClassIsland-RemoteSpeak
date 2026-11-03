package com.remotebroadcast.ui.viewmodel

import com.remotebroadcast.data.remote.BroadcastApi
import com.remotebroadcast.data.repository.SettingsRepository
import com.remotebroadcast.ui.screen.broadcast.BroadcastTriggerMode
import com.remotebroadcast.ui.screen.settings.SettingsUiState
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class SettingsViewModelTest {
    @Before
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun blurAndPredictiveBackAreEnabledByDefault() {
        val defaults = SettingsUiState()
        assertTrue(defaults.enableBlur)
        assertTrue(defaults.enablePredictiveBack)
        val viewModel = SettingsViewModel(MemorySettingsRepository())
        assertTrue(viewModel.uiState.value.enableBlur)
        assertTrue(viewModel.uiState.value.enablePredictiveBack)
    }

    @Test
    fun disabledBlurAndPredictiveBackSurviveRecreation() {
        val repository = MemorySettingsRepository()
        val viewModel = SettingsViewModel(repository)
        viewModel.setEnableBlur(false)
        viewModel.setEnablePredictiveBack(false)
        assertFalse(repository.enableBlur)
        assertFalse(repository.enablePredictiveBack)

        val restored = SettingsViewModel(repository)
        restored.refresh()
        assertFalse(restored.uiState.value.enableBlur)
        assertFalse(restored.uiState.value.enablePredictiveBack)
    }

    @Test
    fun gestureChangesSurviveViewModelRecreation() {
        val repository = MemorySettingsRepository()
        val viewModel = SettingsViewModel(repository)
        viewModel.setEnableSwipeDismiss(false)
        viewModel.setPagerInterceptionMode(2)
        assertFalse(viewModel.uiState.value.enableSwipeDismiss)
        assertEquals(2, viewModel.uiState.value.pagerInterceptionMode)

        val restored = SettingsViewModel(repository)
        assertFalse(restored.uiState.value.enableSwipeDismiss)
        assertEquals(2, restored.uiState.value.pagerInterceptionMode)
        assertTrue(restored.uiState.value.enableFloatingBottomBar)
        assertTrue(restored.uiState.value.enableFloatingBottomBarBlur)
    }

    @Test
    fun refreshReadsGestureChangesFromRepository() {
        val repository = MemorySettingsRepository()
        val viewModel = SettingsViewModel(repository)
        repository.enableSwipeDismiss = false
        repository.pagerInterceptionMode = 0
        viewModel.refresh()
        assertFalse(viewModel.uiState.value.enableSwipeDismiss)
        assertEquals(0, viewModel.uiState.value.pagerInterceptionMode)
    }

    @Test
    fun invalidPagerModesAreClampedBeforePersistence() {
        val repository = MemorySettingsRepository()
        val viewModel = SettingsViewModel(repository)
        viewModel.setPagerInterceptionMode(-1)
        assertEquals(0, repository.pagerInterceptionMode)
        assertEquals(0, viewModel.uiState.value.pagerInterceptionMode)
        viewModel.setPagerInterceptionMode(3)
        assertEquals(2, repository.pagerInterceptionMode)
        assertEquals(2, viewModel.uiState.value.pagerInterceptionMode)
    }

    @Test
    fun editingBroadcastSettingsFromPreferencesDismissesTheGuide() {
        val repository = MemorySettingsRepository()
        val viewModel = SettingsViewModel(repository)
        assertFalse(repository.broadcastSetupDone)

        viewModel.setBroadcastToken("secret")
        assertEquals("secret", repository.broadcastToken)
        assertTrue("Configuring in Settings answers the guide for us", repository.broadcastSetupDone)
    }

    @Test
    fun broadcastGestureAndRepeatIndexesMapBackToStoredValues() {
        val repository = MemorySettingsRepository()
        val viewModel = SettingsViewModel(repository)

        viewModel.setBroadcastTriggerIndex(2)
        assertEquals(BroadcastTriggerMode.LONG_PRESS.value, repository.broadcastTriggerMode)
        assertEquals(BroadcastTriggerMode.LONG_PRESS.value, viewModel.uiState.value.broadcastTriggerMode)

        viewModel.setBroadcastTriggerIndex(9)
        assertEquals(BroadcastTriggerMode.CONFIRM.value, repository.broadcastTriggerMode)

        viewModel.setBroadcastRepeatIndex(BroadcastApi.MAX_REPEAT - BroadcastApi.MIN_REPEAT)
        assertEquals(BroadcastApi.MAX_REPEAT, repository.broadcastRepeat)
    }
}

private class MemorySettingsRepository : SettingsRepository {
    private val defaults = SettingsUiState()
    override var checkUpdate = defaults.checkUpdate
    override var themeMode = defaults.themeMode
    override var miuixMonet = defaults.miuixMonet
    override var keyColor = defaults.keyColor
    override var colorStyle = defaults.colorStyle
    override var colorSpec = defaults.colorSpec
    override var enablePredictiveBack = defaults.enablePredictiveBack
    override var enableSwipeDismiss = defaults.enableSwipeDismiss
    override var pagerInterceptionMode = defaults.pagerInterceptionMode
    override var enableBlur = defaults.enableBlur
    override var enableFloatingBottomBar = defaults.enableFloatingBottomBar
    override var enableFloatingBottomBarBlur = defaults.enableFloatingBottomBarBlur
    override var navigationRailExpanded = false
    override var pageScale = defaults.pageScale
    override var broadcastUrl = defaults.broadcastUrl
    override var broadcastToken = defaults.broadcastToken
    override var broadcastRepeat = defaults.broadcastRepeat
    override var broadcastTriggerMode = defaults.broadcastTriggerMode
    override var snapshotAutoRefresh = defaults.snapshotAutoRefresh
    override var broadcastSetupDone = false
}
