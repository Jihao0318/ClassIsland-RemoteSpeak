package com.remotebroadcast.ui.util

import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

fun formatTimeOfDay(millis: Long): String =
    SimpleDateFormat("HH:mm:ss", Locale.getDefault()).format(Date(millis))
