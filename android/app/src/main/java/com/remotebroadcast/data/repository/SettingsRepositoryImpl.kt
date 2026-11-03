package com.remotebroadcast.data.repository

import android.content.Context
import androidx.core.content.edit
import com.materialkolor.PaletteStyle
import com.materialkolor.dynamiccolor.ColorSpec
import com.remotebroadcast.templateApp

class SettingsRepositoryImpl : SettingsRepository {

    private val prefs by lazy {
        templateApp.getSharedPreferences("settings", Context.MODE_PRIVATE)
    }

    override var checkUpdate: Boolean
        get() = prefs.getBoolean("check_update", true)
        set(value) = prefs.edit { putBoolean("check_update", value) }

    override var themeMode: Int
        get() = prefs.getInt("color_mode", 0)
        set(value) = prefs.edit { putInt("color_mode", value) }

    override var miuixMonet: Boolean
        get() = prefs.getBoolean("miuix_monet", false)
        set(value) = prefs.edit { putBoolean("miuix_monet", value) }

    override var keyColor: Int
        get() = prefs.getInt("key_color", 0)
        set(value) = prefs.edit { putInt("key_color", value) }

    override var colorStyle: String
        get() = prefs.getString("color_style", PaletteStyle.TonalSpot.name) ?: PaletteStyle.TonalSpot.name
        set(value) = prefs.edit { putString("color_style", value) }

    override var colorSpec: String
        get() = prefs.getString("color_spec", ColorSpec.SpecVersion.Default.name) ?: ColorSpec.SpecVersion.Default.name
        set(value) = prefs.edit { putString("color_spec", value) }

    override var enablePredictiveBack: Boolean
        get() = prefs.getBoolean("enable_predictive_back", true)
        set(value) = prefs.edit { putBoolean("enable_predictive_back", value) }

    override var enableSwipeDismiss: Boolean
        get() = prefs.getBoolean("enable_swipe_dismiss", true)
        set(value) = prefs.edit { putBoolean("enable_swipe_dismiss", value) }

    override var pagerInterceptionMode: Int
        get() = prefs.getInt("pager_interception_mode", 1).coerceIn(0, 2)
        set(value) = prefs.edit { putInt("pager_interception_mode", value.coerceIn(0, 2)) }

    override var enableBlur: Boolean
        get() = prefs.getBoolean("enable_blur", true)
        set(value) = prefs.edit { putBoolean("enable_blur", value) }

    override var enableFloatingBottomBar: Boolean
        get() = prefs.getBoolean("enable_floating_bottom_bar", true)
        set(value) = prefs.edit { putBoolean("enable_floating_bottom_bar", value) }

    override var enableFloatingBottomBarBlur: Boolean
        get() = prefs.getBoolean("enable_floating_bottom_bar_blur", true)
        set(value) = prefs.edit { putBoolean("enable_floating_bottom_bar_blur", value) }

    override var navigationRailExpanded: Boolean
        get() = prefs.getBoolean("navigation_rail_expanded", false)
        set(value) = prefs.edit { putBoolean("navigation_rail_expanded", value) }

    override var pageScale: Float
        get() = prefs.getFloat("page_scale", 1.0f)
        set(value) = prefs.edit { putFloat("page_scale", value) }

    override var broadcastUrl: String
        get() = prefs.getString("broadcast_url", "") ?: ""
        set(value) = prefs.edit { putString("broadcast_url", value.trim()) }

    override var broadcastToken: String
        get() = prefs.getString("broadcast_token", "") ?: ""
        set(value) = prefs.edit { putString("broadcast_token", value.trim()) }

    override var broadcastRepeat: Int
        get() = prefs.getInt("broadcast_repeat", 1).coerceIn(MIN_REPEAT, MAX_REPEAT)
        set(value) = prefs.edit { putInt("broadcast_repeat", value.coerceIn(MIN_REPEAT, MAX_REPEAT)) }

    override var broadcastTriggerMode: Int
        get() = prefs.getInt("broadcast_trigger_mode", 1).coerceIn(0, 2)
        set(value) = prefs.edit { putInt("broadcast_trigger_mode", value.coerceIn(0, 2)) }

    override var snapshotAutoRefresh: Boolean
        get() = prefs.getBoolean("snapshot_auto_refresh", false)
        set(value) = prefs.edit { putBoolean("snapshot_auto_refresh", value) }

    override var broadcastSetupDone: Boolean
        get() = prefs.getBoolean("broadcast_setup_done", false)
        set(value) = prefs.edit { putBoolean("broadcast_setup_done", value) }

    private companion object {
        const val MIN_REPEAT = 1
        const val MAX_REPEAT = 5
    }
}
