// The key path, natively.
//
// Windows starts a fresh process for every press of the Copilot key, so everything here is paid
// per press, and the point of this executable is that it pays almost nothing: no runtime to start,
// no window, no framework. It reads the settings file, decides what the press means, does it, and
// exits. Anything that needs a window — the palette, the settings window, a balloon saying an
// action failed — is handed to the .NET executable next door, which is good at windows and only
// has to start on the presses that need one.

#include "Activation.h"
#include "Config.h"
#include "Focus.h"
#include "Hotkey.h"
#include "Input.h"
#include "Launch.h"
#include "Log.h"
#include "Tap.h"
#include "Text.h"

#include <Windows.h>

#include <format>

namespace
{
    using namespace AntiPilot;

    /// <summary>Carries out a configured action. Any failure is logged and shown as a balloon, because a silent failure just looks like a dead key.</summary>
    void Run(const KeyAction& action)
    {
        std::wstring failure;

        switch (action.kind)
        {
        case ActionKind::MenuKey:
            Input::SendMenuKey();
            return;

        case ActionKind::Hotkey:
        {
            auto hotkey = HotkeyDefinition::TryParse(action.hotkey);
            if (!hotkey)
            {
                Log::Write(std::format(L"Cannot parse the shortcut '{}'.", action.hotkey));
                return;
            }

            Input::SendHotkey(*hotkey);
            return;
        }

        case ActionKind::Palette:
            Log::Write(L"Handing the palette to the settings executable.");
            Launch::Delegate({ L"--palette" });
            return;

        case ActionKind::ShellApp:
            Input::ReleaseStuckModifiers();
            if (action.behaviour != LaunchBehaviour::Always &&
                Focus::TryFocus(action.aumid, {}, action.behaviour == LaunchBehaviour::Toggle))
            {
                return;
            }

            failure = Launch::AppsFolderItem(action.aumid);
            break;

        case ActionKind::File:
        {
            Input::ReleaseStuckModifiers();

            // Focusing only makes sense for a program. A folder or a URL has no window of its own
            // that could be brought forward, so those always go to the shell.
            std::wstring fileName = Text::ExpandEnvironment(action.path);
            if (action.behaviour != LaunchBehaviour::Always &&
                Text::EndsWithIgnoreCase(fileName, L".exe") &&
                Focus::TryFocus({}, fileName, action.behaviour == LaunchBehaviour::Toggle))
            {
                return;
            }

            failure = Launch::File(action);
            break;
        }

        default:
            return;
        }

        if (!failure.empty())
        {
            Log::Write(std::format(L"Action '{}' failed: {}", KindName(action.kind), failure));
            Launch::Delegate({ L"--notify", failure });
        }
    }

    void RunAction(const AppConfig& config, const KeyAction& action)
    {
        switch (config.OutcomeFor(action))
        {
        case KeyPressOutcome::OpenSettings:
            // Nothing set up yet — the friendliest thing to do is show the settings window.
            Log::Write(L"Nothing has been set up yet; opening settings.");
            Launch::Delegate({ L"--settings" });
            return;

        case KeyPressOutcome::DoNothing:
            Log::Write(L"The key is set to do nothing.");
            return;

        default:
            Run(action);
            return;
        }
    }

    int HandleKeyPress()
    {
        auto arguments = Activation::Arguments();
        std::wstring state = Activation::State(arguments);
        std::wstring aumid = Activation::CurrentAumid();

        Log::Write(std::format(L"Key press: state={}, aumid={} (native)", state, aumid.empty() ? L"(unpackaged)" : aumid));

        // A long press arrives as Down then Up when Windows uses URI activation, so act on Down and
        // ignore Up to avoid running twice.
        if (Text::EqualsIgnoreCase(state, L"Up"))
        {
            return 0;
        }

        AppConfig config = AppConfig::Load();

        // Read who is in front before anything else: the double-press wait below takes no focus,
        // but the answer belongs to the moment the key was pressed.
        std::wstring foreground = Focus::GetForegroundProcessName();
        Log::Write(std::format(L"Foreground app: {}", foreground.empty() ? L"(none)" : foreground));

        // Nothing to detect unless a double press has somewhere to go, and detecting costs every
        // single press the width of the window — so this stays switched off until it is asked for.
        if (config.doubleTapEnabled && config.doubleTap.IsConfigured())
        {
            switch (Tap::Classify(config.doubleTapWindowMs))
            {
            case Tap::Press::Handled:
                return 0;

            case Tap::Press::Double:
                RunAction(config, config.doubleTap);
                return 0;

            default:
                break;
            }
        }

        RunAction(config, config.ResolveTap(foreground));
        return 0;
    }
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    return HandleKeyPress();
}
