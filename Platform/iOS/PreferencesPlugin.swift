import Foundation

@MainActor final class PreferencesPlugin {
  private static var instance: PreferencesPlugin?
  static func register() { if instance == nil { instance = PreferencesPlugin() } }
  private init() {
    let channel = NativeChannels.channel("dotnative.preferences")
    for method in ["get", "set", "contains", "remove", "clear"] {
      channel.handle(method) { args, reply in
        let fields = args.fields
        guard let app = fields["applicationId"]?.string,
          app.range(of: "^[A-Za-z0-9_.-]{1,128}$", options: .regularExpression) != nil,
          let defaults = UserDefaults(suiteName: "dotnative.preferences." + app)
        else {
          reply.failure("invalid_arguments", "Invalid preference namespace")
          return
        }
        if method == "clear" {
          defaults.removePersistentDomain(forName: "dotnative.preferences." + app)
          reply.success(.null)
          return
        }
        guard let key = fields["key"]?.string, !key.isEmpty, key.utf16.count <= 1024,
          !key.contains("\0")
        else {
          reply.failure("invalid_arguments", "Invalid preference key")
          return
        }
        if method == "remove" {
          defaults.removeObject(forKey: key)
          reply.success(.null)
          return
        }
        if method == "contains" {
          reply.success(.bool(defaults.object(forKey: key) != nil))
          return
        }
        if method == "get" {
          guard let encoded = defaults.data(forKey: key) else {
            reply.success(.null)
            return
          }
          do {
            guard let stored = try JSONSerialization.jsonObject(with: encoded) as? [String: Any],
              let type = stored["type"] as? String
            else { throw CocoaError(.coderInvalidValue) }
            switch type {
            case "string":
              guard let value = stored["value"] as? String else {
                throw CocoaError(.coderInvalidValue)
              }
              reply.success(.string(value))
            case "bool":
              guard let value = stored["value"] as? Bool else {
                throw CocoaError(.coderInvalidValue)
              }
              reply.success(.bool(value))
            case "integer":
              guard let value = stored["value"] as? String, let number = Int64(value) else {
                throw CocoaError(.coderInvalidValue)
              }
              reply.success(.integer(number))
            case "double":
              guard let value = stored["value"] as? Double else {
                throw CocoaError(.coderInvalidValue)
              }
              reply.success(.double(value))
            case "list":
              guard let value = stored["value"] as? [String] else {
                throw CocoaError(.coderInvalidValue)
              }
              reply.success(.list(value.map { .string($0) }))
            default: throw CocoaError(.coderInvalidValue)
            }
          } catch { reply.failure("invalid_value", "Invalid preference value") }
          return
        }
        guard let value = fields["value"] else {
          reply.failure("invalid_value", "Missing preference value")
          return
        }
        let type: String
        let stored: Any
        switch value {
        case .string(let value):
          type = "string"
          stored = value
        case .bool(let value):
          type = "bool"
          stored = value
        case .integer(let value):
          type = "integer"
          stored = String(value)
        case .double(let value) where value.isFinite:
          type = "double"
          stored = value
        case .list(let values):
          let strings = values.compactMap { $0.string }
          guard strings.count == values.count else {
            reply.failure("invalid_value", "String array required")
            return
          }
          type = "list"
          stored = strings
        default:
          reply.failure("invalid_value", "Unsupported preference type")
          return
        }
        do {
          let encoded = try JSONSerialization.data(withJSONObject: ["type": type, "value": stored])
          guard encoded.count <= 1_048_576 else {
            reply.failure("invalid_value", "Preference exceeds size limit")
            return
          }
          defaults.set(encoded, forKey: key)
          reply.success(.null)
        } catch { reply.failure("preferences_failed", "Could not encode preference") }
      }
    }
  }
}
