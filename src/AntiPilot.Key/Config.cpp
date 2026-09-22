#include "Config.h"

#include "Hotkey.h"
#include "Json.h"
#include "Log.h"
#include "Paths.h"
#include "Text.h"

#include <Windows.h>

#include <algorithm>
#include <iterator>
#include <format>

namespace AntiPilot
{
    namespace
    {
        constexpr std::wstring_view KindNames[] = { L"None", L"ShellApp", L"File", L"MenuKey", L"Hotkey", L"Palette" };
        constexpr std::wstring_view BehaviourNames[] = { L"Always", L"FocusIfRunning", L"Toggle" };

        /// <summary>
        /// Reads the members of one object the way System.Text.Json does: names are case-sensitive,
        /// unknown ones are ignored, null means "not set", and a value of the wrong type is a
        /// refusal of the whole document rather than a default.
        /// </summary>
        class ObjectReader
        {
        public:
            ObjectReader(const Json::Value& object, std::wstring& error) : _object(object), _error(error) {}

            bool String(std::wstring_view key, std::wstring& out)
            {
                const Json::Value* member = _object.Get(key);
                if (member == nullptr || member->IsNull())
                {
                    return true;
                }

                if (!member->IsString())
                {
                    return Fail(key, L"a string");
                }

                out = member->string;
                return true;
            }

            bool Boolean(std::wstring_view key, bool& out)
            {
                const Json::Value* member = _object.Get(key);
                if (member == nullptr || member->IsNull())
                {
                    return true;
                }

                if (!member->IsBoolean())
                {
                    return Fail(key, L"true or false");
                }

                out = member->boolean;
                return true;
            }

            bool Int(std::wstring_view key, int& out)
            {
                const Json::Value* member = _object.Get(key);
                if (member == nullptr || member->IsNull())
                {
                    return true;
                }

                if (!member->IsNumber() || member->number != static_cast<double>(static_cast<int>(member->number)))
                {
                    return Fail(key, L"a whole number");
                }

                out = static_cast<int>(member->number);
                return true;
            }

            /// <summary>
            /// An enum serialised with JsonStringEnumConverter: written as its name, read back by
            /// name without regard to case, and a bare number is accepted too because that is what
            /// the converter accepts. A name that is not one of ours is what a file from some other
            /// program looks like, and the converter throws on it, so this refuses as well.
            /// </summary>
            template <typename TEnum, size_t N>
            bool Enum(std::wstring_view key, const std::wstring_view (&names)[N], TEnum& out)
            {
                const Json::Value* member = _object.Get(key);
                if (member == nullptr || member->IsNull())
                {
                    return true;
                }

                if (member->IsNumber())
                {
                    out = static_cast<TEnum>(static_cast<int>(member->number));
                    return true;
                }

                if (!member->IsString())
                {
                    return Fail(key, L"a name");
                }

                for (size_t i = 0; i < N; ++i)
                {
                    if (Text::EqualsIgnoreCase(member->string, names[i]))
                    {
                        out = static_cast<TEnum>(i);
                        return true;
                    }
                }

                _error = std::format(L"'{}' is not a value '{}' can take", member->string, key);
                return false;
            }

            /// <summary>The array member, or null when absent. Anything else is a refusal.</summary>
            const Json::Value* Array(std::wstring_view key)
            {
                const Json::Value* member = _object.Get(key);
                if (member == nullptr || member->IsNull())
                {
                    return nullptr;
                }

                if (!member->IsArray())
                {
                    Fail(key, L"a list");
                    return nullptr;
                }

                return member;
            }

            const Json::Value* Object(std::wstring_view key)
            {
                const Json::Value* member = _object.Get(key);
                if (member == nullptr || member->IsNull())
                {
                    return nullptr;
                }

                if (!member->IsObject())
                {
                    Fail(key, L"an object");
                    return nullptr;
                }

                return member;
            }

            bool Failed() const { return !_error.empty(); }

        private:
            const Json::Value& _object;
            std::wstring& _error;

            bool Fail(std::wstring_view key, std::wstring_view expected)
            {
                _error = std::format(L"'{}' should be {}", key, expected);
                return false;
            }
        };

        bool ReadAction(const Json::Value& value, KeyAction& action, std::wstring& error)
        {
            if (!value.IsObject())
            {
                error = L"an action should be an object";
                return false;
            }

            ObjectReader reader(value, error);
            return reader.Enum(L"Kind", KindNames, action.kind)
                && reader.String(L"Aumid", action.aumid)
                && reader.String(L"DisplayName", action.displayName)
                && reader.String(L"Path", action.path)
                && reader.String(L"Arguments", action.arguments)
                && reader.String(L"WorkingDirectory", action.workingDirectory)
                && reader.String(L"Hotkey", action.hotkey)
                && reader.Enum(L"Behaviour", BehaviourNames, action.behaviour)
                && reader.String(L"Label", action.label);
        }

        bool ReadRule(const Json::Value& value, AppRule& rule, std::wstring& error)
        {
            if (!value.IsObject())
            {
                error = L"a rule should be an object";
                return false;
            }

            ObjectReader reader(value, error);
            if (!reader.String(L"ProcessName", rule.processName))
            {
                return false;
            }

            const Json::Value* action = reader.Object(L"Action");
            if (reader.Failed())
            {
                return false;
            }

            return action == nullptr || ReadAction(*action, rule.action, error);
        }

        std::optional<std::string> ReadWholeFile(const std::wstring& path)
        {
            HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            if (file == INVALID_HANDLE_VALUE)
            {
                return std::nullopt;
            }

            std::string content;
            LARGE_INTEGER size{};
            if (GetFileSizeEx(file, &size) && size.QuadPart > 0 && size.QuadPart < (16 << 20))
            {
                content.resize(static_cast<size_t>(size.QuadPart));
                DWORD read = 0;
                if (!ReadFile(file, content.data(), static_cast<DWORD>(content.size()), &read, nullptr))
                {
                    CloseHandle(file);
                    return std::nullopt;
                }

                content.resize(read);
            }

            CloseHandle(file);
            return content;
        }
    }

    std::wstring_view KindName(ActionKind kind)
    {
        auto index = static_cast<size_t>(kind);
        return index < std::size(KindNames) ? KindNames[index] : std::wstring_view(L"?");
    }

    bool KeyAction::IsConfigured() const
    {
        switch (kind)
        {
        case ActionKind::None: return false;
        case ActionKind::ShellApp: return !Text::IsBlank(aumid);
        case ActionKind::File: return !Text::IsBlank(path);
        case ActionKind::MenuKey: return true;
        case ActionKind::Hotkey: return HotkeyDefinition::TryParse(hotkey).has_value();
        case ActionKind::Palette: return true;
        default: return false;
        }
    }

    std::wstring AppRule::Normalise(std::wstring_view value)
    {
        std::wstring trimmed = Text::Trim(value);
        if (Text::EndsWithIgnoreCase(trimmed, L".exe"))
        {
            trimmed.resize(trimmed.size() - 4);
        }

        return trimmed;
    }

    bool AppRule::IsUsable() const
    {
        return !Text::IsBlank(processName) && action.IsConfigured();
    }

    bool AppRule::Matches(std::wstring_view foregroundProcess) const
    {
        if (Text::IsBlank(processName) || Text::IsBlank(foregroundProcess))
        {
            return false;
        }

        return Text::EqualsIgnoreCase(Normalise(processName), Normalise(foregroundProcess));
    }

    AppConfig AppConfig::Load()
    {
        return LoadFrom(Paths::ConfigPath()).value_or(AppConfig{});
    }

    std::optional<AppConfig> AppConfig::LoadFrom(const std::wstring& path)
    {
        auto content = ReadWholeFile(path);
        if (!content)
        {
            return std::nullopt;
        }

        std::wstring error;
        auto config = FromJson(*content, &error);
        if (!config)
        {
            Log::Write(std::format(L"Failed to read config from '{}': {}", path, error));
        }

        return config;
    }

    std::optional<AppConfig> AppConfig::FromJson(std::string_view utf8, std::wstring* errorOut)
    {
        std::wstring error;
        auto document = Json::Parse(utf8, &error);

        AppConfig config;
        bool ok = document.has_value();

        if (ok && document->IsNull())
        {
            // The serialiser reads a literal null as "no config", which Load turns into a fresh one.
            ok = false;
            error = L"the document is null";
        }

        if (ok && !document->IsObject())
        {
            ok = false;
            error = L"the document is not an object";
        }

        if (ok)
        {
            ObjectReader reader(*document, error);

            ok = reader.Int(L"Schema", config.schema)
                && reader.Boolean(L"DoubleTapEnabled", config.doubleTapEnabled)
                && reader.Int(L"DoubleTapWindowMs", config.doubleTapWindowMs)
                && reader.Boolean(L"TrayIntroShown", config.trayIntroShown)
                && reader.String(L"Language", config.language);

            if (ok)
            {
                if (const Json::Value* tap = reader.Object(L"Tap"))
                {
                    ok = ReadAction(*tap, config.tap, error);
                }

                ok = ok && !reader.Failed();
            }

            if (ok)
            {
                if (const Json::Value* doubleTap = reader.Object(L"DoubleTap"))
                {
                    ok = ReadAction(*doubleTap, config.doubleTap, error);
                }

                ok = ok && !reader.Failed();
            }

            if (ok)
            {
                if (const Json::Value* rules = reader.Array(L"AppRules"))
                {
                    for (const Json::Value& item : rules->array)
                    {
                        if (item.IsNull())
                        {
                            // Normalise drops these on the .NET side; there is nothing to keep.
                            continue;
                        }

                        AppRule rule;
                        if (!ReadRule(item, rule, error))
                        {
                            ok = false;
                            break;
                        }

                        config.appRules.push_back(std::move(rule));
                    }
                }

                ok = ok && !reader.Failed();
            }

            if (ok)
            {
                if (const Json::Value* palette = reader.Array(L"Palette"))
                {
                    for (const Json::Value& item : palette->array)
                    {
                        if (item.IsNull())
                        {
                            continue;
                        }

                        KeyAction entry;
                        if (!ReadAction(item, entry, error))
                        {
                            ok = false;
                            break;
                        }

                        config.palette.push_back(std::move(entry));
                    }
                }

                ok = ok && !reader.Failed();
            }
        }

        if (!ok)
        {
            if (errorOut != nullptr)
            {
                *errorOut = error;
            }

            return std::nullopt;
        }

        config.Normalise();
        config.hasBeenSaved = true;
        return config;
    }

    void AppConfig::Normalise()
    {
        doubleTapWindowMs = std::clamp(doubleTapWindowMs, MinDoubleTapWindowMs, MaxDoubleTapWindowMs);
        schema = CurrentSchema;
    }

    const KeyAction& AppConfig::ResolveTap(std::wstring_view foregroundProcess) const
    {
        for (const AppRule& rule : appRules)
        {
            if (rule.IsUsable() && rule.Matches(foregroundProcess))
            {
                Log::Write(std::format(L"App rule for '{}' matched.", rule.processName));
                return rule.action;
            }
        }

        return tap;
    }

    KeyPressOutcome AppConfig::OutcomeFor(const KeyAction& action) const
    {
        if (!hasBeenSaved)
        {
            return KeyPressOutcome::OpenSettings;
        }

        if (action.kind == ActionKind::None)
        {
            return KeyPressOutcome::DoNothing;
        }

        // A kind was chosen but its target never was — "launch an app" with no app. The user meant
        // something, so send them back to finish it.
        return action.IsConfigured() ? KeyPressOutcome::RunAction : KeyPressOutcome::OpenSettings;
    }
}

// ---- writing ---------------------------------------------------------------

namespace AntiPilot
{
    namespace
    {
        /// <summary>A string member the way the .NET side writes an unset one: null, not "".</summary>
        Json::Value StringOrNull(std::wstring_view value)
        {
            return value.empty() ? Json::Null() : Json::String(value);
        }

        Json::Value WriteAction(const KeyAction& action)
        {
            auto object = Json::Object();
            Json::Set(object, L"Kind", Json::String(KindName(action.kind)));
            Json::Set(object, L"Aumid", StringOrNull(action.aumid));
            Json::Set(object, L"DisplayName", StringOrNull(action.displayName));
            Json::Set(object, L"Path", StringOrNull(action.path));
            Json::Set(object, L"Arguments", StringOrNull(action.arguments));
            Json::Set(object, L"WorkingDirectory", StringOrNull(action.workingDirectory));
            Json::Set(object, L"Hotkey", StringOrNull(action.hotkey));
            Json::Set(object, L"Behaviour", Json::String(BehaviourNames[static_cast<int>(action.behaviour)]));
            Json::Set(object, L"Label", StringOrNull(action.label));
            return object;
        }

        std::wstring WriteWholeFile(const std::wstring& path, std::string_view content)
        {
            HANDLE file = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
            if (file == INVALID_HANDLE_VALUE)
            {
                return Text::DescribeError(GetLastError());
            }

            DWORD written = 0;
            const bool ok = WriteFile(file, content.data(), static_cast<DWORD>(content.size()), &written, nullptr)
                && written == content.size();
            const DWORD error = ok ? 0 : GetLastError();
            CloseHandle(file);
            return ok ? std::wstring{} : Text::DescribeError(error);
        }
    }

    std::string AppConfig::ToJson() const
    {
        auto root = Json::Object();
        Json::Set(root, L"Schema", Json::Number(CurrentSchema));
        Json::Set(root, L"Tap", WriteAction(tap));
        Json::Set(root, L"DoubleTap", WriteAction(doubleTap));
        Json::Set(root, L"DoubleTapEnabled", Json::Boolean(doubleTapEnabled));
        Json::Set(root, L"DoubleTapWindowMs", Json::Number(doubleTapWindowMs));

        auto rules = Json::Array();
        for (const AppRule& rule : appRules)
        {
            auto object = Json::Object();
            Json::Set(object, L"ProcessName", StringOrNull(rule.processName));
            Json::Set(object, L"Action", WriteAction(rule.action));
            rules.array.push_back(std::move(object));
        }
        Json::Set(root, L"AppRules", std::move(rules));

        auto entries = Json::Array();
        for (const KeyAction& entry : palette)
        {
            entries.array.push_back(WriteAction(entry));
        }
        Json::Set(root, L"Palette", std::move(entries));

        Json::Set(root, L"TrayIntroShown", Json::Boolean(trayIntroShown));
        Json::Set(root, L"Language", StringOrNull(language));
        return Json::Serialize(root);
    }

    std::wstring AppConfig::SaveTo(const std::wstring& path)
    {
        schema = CurrentSchema;

        const std::wstring directory = Text::DirectoryName(path);
        if (!directory.empty() && !Paths::EnsureDirectory(directory))
        {
            return std::format(L"could not create '{}'", directory);
        }

        const std::wstring temporary = path + L".tmp";
        if (std::wstring error = WriteWholeFile(temporary, ToJson()); !error.empty())
        {
            return error;
        }

        if (!MoveFileExW(temporary.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
        {
            std::wstring error = Text::DescribeError(GetLastError());
            DeleteFileW(temporary.c_str());
            return error;
        }

        hasBeenSaved = true;
        return {};
    }

    std::wstring AppConfig::Save()
    {
        return SaveTo(Paths::ConfigPath());
    }
}
