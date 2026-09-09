#pragma once

#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace AntiPilot
{
    enum class ActionKind
    {
        /// <summary>Do nothing at all.</summary>
        None = 0,

        /// <summary>Launch an entry from the Start menu / Apps folder (Store apps included), by AUMID.</summary>
        ShellApp = 1,

        /// <summary>Launch an arbitrary file, shortcut, folder or URL through the shell.</summary>
        File = 2,

        /// <summary>Behave like the old Menu / context-menu key (VK_APPS).</summary>
        MenuKey = 3,

        /// <summary>Synthesise any keyboard chord into whatever has focus.</summary>
        Hotkey = 4,

        /// <summary>Show the quick-launch palette.</summary>
        Palette = 5,
    };

    /// <summary>What to do when the target of a launch action already has a window open.</summary>
    enum class LaunchBehaviour
    {
        /// <summary>Hand the target to the shell every time, however many copies that leaves running.</summary>
        Always = 0,

        /// <summary>Bring an existing window to the front instead of starting a second copy.</summary>
        FocusIfRunning = 1,

        /// <summary>Focus it, or minimise it when it is already the foreground window.</summary>
        Toggle = 2,
    };

    /// <summary>The name a kind is written under in the config, which is also how the log refers to it.</summary>
    std::wstring_view KindName(ActionKind kind);

    struct KeyAction
    {
        ActionKind kind = ActionKind::None;

        /// <summary>Parsing name of the Apps-folder item; an AUMID for packaged apps.</summary>
        std::wstring aumid;

        std::wstring displayName;
        std::wstring path;
        std::wstring arguments;
        std::wstring workingDirectory;

        /// <summary>A chord in the form HotkeyDefinition round-trips, e.g. "Ctrl+Shift+Escape".</summary>
        std::wstring hotkey;

        /// <summary>Only consulted for ShellApp and File.</summary>
        LaunchBehaviour behaviour = LaunchBehaviour::Always;

        std::wstring label;

        /// <summary>True when the action will actually do something. "Nothing" is not configured; see OutcomeFor.</summary>
        bool IsConfigured() const;
    };

    /// <summary>One "while this app is in front, do that instead" override.</summary>
    struct AppRule
    {
        /// <summary>Process executable name, with or without the extension. Matched case-insensitively.</summary>
        std::wstring processName;

        KeyAction action;

        bool IsUsable() const;

        /// <summary>True when this rule is the one to apply to the process in front. Empty means nothing is.</summary>
        bool Matches(std::wstring_view foregroundProcess) const;

        /// <summary>Strips the extension so "chrome" and "chrome.exe" are the same rule.</summary>
        static std::wstring Normalise(std::wstring_view value);
    };

    /// <summary>What a press of the key should actually do, once the configuration has had its say.</summary>
    enum class KeyPressOutcome
    {
        /// <summary>Nothing has ever been set up, so show the user where to set it up.</summary>
        OpenSettings,

        /// <summary>The user chose "Nothing". Honour that and stay out of the way.</summary>
        DoNothing,

        /// <summary>Carry out the configured action.</summary>
        RunAction,
    };

    /// <summary>
    /// The settings file, read the way the .NET side writes it. This side never writes it: the
    /// settings window owns the file, and the key path only needs to know what it says.
    /// </summary>
    struct AppConfig
    {
        static constexpr int CurrentSchema = 2;

        /// <summary>Below this a second press cannot be hit reliably; above it every press feels broken.</summary>
        static constexpr int MinDoubleTapWindowMs = 200;
        static constexpr int MaxDoubleTapWindowMs = 1000;

        int schema = CurrentSchema;
        KeyAction tap;
        KeyAction doubleTap;
        bool doubleTapEnabled = false;
        int doubleTapWindowMs = 350;
        std::vector<AppRule> appRules;
        std::vector<KeyAction> palette;
        bool trayIntroShown = false;
        std::wstring language;

        /// <summary>
        /// True when these settings came from a file the user has actually saved. This is what
        /// separates "never set up" from "set up as Nothing", which ActionKind::None alone cannot.
        /// The existence of the file is the signal, so there is nothing stored for it.
        /// </summary>
        bool hasBeenSaved = false;

        /// <summary>The file on disk, or a fresh unsaved config when there is none or it cannot be read.</summary>
        static AppConfig Load();

        /// <summary>Reads a config file. Nothing when it is missing, unreadable or not one of ours.</summary>
        static std::optional<AppConfig> LoadFrom(const std::wstring& path);

        /// <summary>
        /// Reads config text. Marked as saved, since text only ever comes from a file. Refuses
        /// anything the .NET serialiser would refuse — a member of the wrong type, an enum name it
        /// has never heard of — so a corrupt file is a clear "not ours" rather than a wrong answer.
        /// </summary>
        static std::optional<AppConfig> FromJson(std::string_view utf8, std::wstring* error = nullptr);

        /// <summary>Fills in whatever an older or hand-edited file left out, and clamps what it got wrong.</summary>
        void Normalise();

        /// <summary>The action a press should run, once foreground-app rules have had their say.</summary>
        const KeyAction& ResolveTap(std::wstring_view foregroundProcess) const;

        /// <summary>
        /// Decides what a press should do. Issue #1 was this decision being made from
        /// IsConfigured alone, which cannot tell a chosen "Nothing" from a fresh install.
        /// </summary>
        KeyPressOutcome OutcomeFor(const KeyAction& action) const;
    };
}
