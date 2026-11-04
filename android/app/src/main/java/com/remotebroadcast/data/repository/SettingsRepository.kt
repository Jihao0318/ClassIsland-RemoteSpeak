package com.remotebroadcast.data.repository

interface SettingsRepository {
    var checkUpdate: Boolean
    var themeMode: Int
    var miuixMonet: Boolean
    var keyColor: Int
    var colorStyle: String
    var colorSpec: String
    var enablePredictiveBack: Boolean
    var enableSwipeDismiss: Boolean
    var pagerInterceptionMode: Int
    var enableBlur: Boolean
    var enableFloatingBottomBar: Boolean
    var enableFloatingBottomBarBlur: Boolean
    var navigationRailExpanded: Boolean
    var pageScale: Float
    var broadcastUrl: String
    var broadcastToken: String
    var broadcastRepeat: Int
    var broadcastTriggerMode: Int
    var snapshotAutoRefresh: Boolean
    var broadcastSetupDone: Boolean
    var hideViewOnlyHint: Boolean
}
