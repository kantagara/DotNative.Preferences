package com.dotnative.plugincheck
class HarnessActivity : android.app.Activity() {
 override fun onCreate(state: android.os.Bundle?) { super.onCreate(state); com.dotnative.plugins.PreferencesPlugin(this) }
}
