package com.dotnative.plugins

import android.app.Activity
import android.content.Context
import android.util.Base64

class PreferencesPlugin(activity: Activity) {
    init {
        val channel = NativeChannels.channel("dotnative.preferences")
        for (method in listOf("get", "set", "contains", "remove", "clear")) {
            channel.handle(method) { args, reply ->
                val fields = args as? Map<*, *>
                val app = fields?.get("applicationId") as? String
                if (app == null || !app.matches(Regex("[A-Za-z0-9_.-]{1,128}"))) {
                    reply.failure("invalid_arguments", "Invalid preference namespace")
                } else {
                    val preferences =
                        activity.getSharedPreferences(
                            "dotnative.preferences.$app",
                            Context.MODE_PRIVATE,
                        )
                    val key = fields["key"] as? String
                    if (
                        method != "clear" &&
                            (key.isNullOrBlank() || key.length > 1024 || key.contains('\u0000'))
                    ) {
                        reply.failure("invalid_arguments", "Invalid preference key")
                    } else {
                        when (method) {
                            "get" -> {
                                val encoded = preferences.getString(key, null)
                                reply.success(
                                    if (encoded == null) null
                                    else PluginCodec.decode(Base64.decode(encoded, Base64.NO_WRAP)),
                                )
                            }
                            "contains" -> reply.success(preferences.contains(key))
                            else -> {
                                val editor = preferences.edit()
                                when (method) {
                                    "clear" -> editor.clear()
                                    "remove" -> editor.remove(key)
                                    "set" -> {
                                        editor.putString(
                                            key,
                                            Base64.encodeToString(
                                                PluginCodec.encode(fields["value"]),
                                                Base64.NO_WRAP,
                                            ),
                                        )
                                    }
                                }
                                if (editor.commit()) reply.success(null)
                                else
                                    reply.failure(
                                        "preferences_failed",
                                        "Could not persist preferences",
                                    )
                            }
                        }
                    }
                }
            }
        }
    }
}
