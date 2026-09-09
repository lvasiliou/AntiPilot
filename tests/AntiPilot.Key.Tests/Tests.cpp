// The native key path's tests. These mirror the xUnit suite for the same logic on the .NET side,
// case for case where the case applies, so that the two halves cannot quietly disagree about what
// a chord means or which rule wins.

#include "Activation.h"
#include "Config.h"
#include "Hotkey.h"
#include "Json.h"
#include "Tap.h"
#include "Text.h"

#include <Windows.h>

#include <cstdio>
#include <functional>
#include <string>
#include <thread>
#include <tuple>
#include <utility>
#include <vector>

namespace
{
    using namespace AntiPilot;

    struct Test
    {
        const char* name;
        std::function<void()> body;
    };

    std::vector<Test>& Registry()
    {
        static std::vector<Test> tests;
        return tests;
    }

    struct Registration
    {
        Registration(const char* name, std::function<void()> body)
        {
            Registry().push_back({ name, std::move(body) });
        }
    };

    struct Failure
    {
        std::string message;
    };

#define TEST(name) \
    void name(); \
    const Registration name##_registration(#name, name); \
    void name()

#define CHECK(condition) \
    do { if (!(condition)) { throw Failure{ std::string("line ") + std::to_string(__LINE__) + ": " #condition }; } } while (false)

#define CHECK_EQUAL(expected, actual) \
    do { auto e_ = (expected); auto a_ = (actual); if (!(e_ == a_)) { throw Failure{ std::string("line ") + std::to_string(__LINE__) + ": " #actual " != " #expected }; } } while (false)

    // ---- hotkeys -----------------------------------------------------------

    TEST(RoundTripsThroughText)
    {
        for (const wchar_t* chord : { L"Ctrl+Shift+Escape", L"Alt+F4", L"Win+V", L"PrintScreen",
            L"Ctrl+Alt+Shift+Win+Delete", L"MediaPlayPause", L"Num5", L"F13", L"Win+." })
        {
            auto parsed = HotkeyDefinition::TryParse(chord);
            CHECK(parsed.has_value());
            CHECK_EQUAL(std::wstring(chord), parsed->Format());

            // The formatted form has to parse back to something equal, or config would drift on save.
            auto again = HotkeyDefinition::TryParse(parsed->Format());
            CHECK(again.has_value());
            CHECK(*parsed == *again);
        }
    }

    TEST(NormalisesAliasesAndOrder)
    {
        const std::pair<const wchar_t*, const wchar_t*> cases[] =
        {
            { L"control+shift+ESCAPE", L"Ctrl+Shift+Escape" },
            { L"CTRL + ALT + del", L"Ctrl+Alt+Delete" },
            { L"meta+e", L"Win+E" },
            { L"Esc", L"Escape" },
            { L"PgDn", L"PageDown" },
            { L"Shift+Ctrl+A", L"Ctrl+Shift+A" },
        };

        for (const auto& [input, expected] : cases)
        {
            auto parsed = HotkeyDefinition::TryParse(input);
            CHECK(parsed.has_value());
            CHECK_EQUAL(std::wstring(expected), parsed->Format());
        }
    }

    TEST(RejectsWhatCannotBeSent)
    {
        for (const wchar_t* input : { L"", L"   ", L"Ctrl", L"Ctrl+Shift", L"Win", L"NotAKey", L"Ctrl+A+B" })
        {
            CHECK(!HotkeyDefinition::TryParse(input).has_value());
        }
    }

    TEST(ModifiersAloneAreNeverAShortcut)
    {
        // A chord whose key is itself a modifier would press and release nothing meaningful.
        CHECK(HotkeyDefinition::IsModifierKey(0x10));
        CHECK(HotkeyDefinition::IsModifierKey(0x5B));
        CHECK(!HotkeyDefinition::IsModifierKey(0x1B));
    }

    TEST(MarksTheKeysThatNeedTheExtendedPrefix)
    {
        // Arrows and the keypad share virtual-key codes; only this flag tells them apart, so a
        // regression here would silently send Num4 where Left was meant.
        const std::pair<const wchar_t*, bool> cases[] =
        {
            { L"Left", true }, { L"Delete", true }, { L"Home", true }, { L"VolumeUp", true },
            { L"A", false }, { L"F5", false }, { L"Num4", false },
        };

        for (const auto& [chord, extended] : cases)
        {
            auto parsed = HotkeyDefinition::TryParse(chord);
            CHECK(parsed.has_value());
            CHECK_EQUAL(extended, parsed->IsExtended());
        }
    }

    TEST(EqualityIgnoresNothing)
    {
        HotkeyDefinition baseline;
        baseline.virtualKey = 0x41;
        baseline.control = true;

        HotkeyDefinition same = baseline;
        HotkeyDefinition shifted = baseline;
        shifted.shift = true;
        HotkeyDefinition otherKey = baseline;
        otherKey.virtualKey = 0x42;

        CHECK(baseline == same);
        CHECK(!(baseline == shifted));
        CHECK(!(baseline == otherKey));
    }

    // ---- rules -------------------------------------------------------------

    TEST(MatchesRegardlessOfHowTheNameWasTyped)
    {
        const std::tuple<const wchar_t*, const wchar_t*, bool> cases[] =
        {
            { L"chrome", L"chrome", true },
            { L"chrome.exe", L"chrome", true },
            { L"chrome", L"chrome.exe", true },
            { L"CHROME.EXE", L"chrome", true },
            { L"  chrome  ", L"chrome", true },
            { L"chrome", L"firefox", false },
            { L"chrome", L"", false },
        };

        for (const auto& [ruleName, foreground, expected] : cases)
        {
            AppRule rule;
            rule.processName = ruleName;
            CHECK_EQUAL(expected, rule.Matches(foreground));
        }
    }

    TEST(AnEmptyRuleMatchesNothing)
    {
        CHECK(!AppRule{}.Matches(L"chrome"));

        AppRule blank;
        blank.processName = L"   ";
        CHECK(!blank.Matches(L"chrome"));
    }

    TEST(IsUsableNeedsBothHalves)
    {
        AppRule nameOnly;
        nameOnly.processName = L"chrome";
        CHECK(!nameOnly.IsUsable());

        AppRule actionOnly;
        actionOnly.action.kind = ActionKind::MenuKey;
        CHECK(!actionOnly.IsUsable());

        AppRule both;
        both.processName = L"chrome";
        both.action.kind = ActionKind::MenuKey;
        CHECK(both.IsUsable());
    }

    AppRule HotkeyRule(const wchar_t* process, const wchar_t* hotkey)
    {
        AppRule rule;
        rule.processName = process;
        rule.action.kind = ActionKind::Hotkey;
        rule.action.hotkey = hotkey;
        return rule;
    }

    TEST(ResolveTapPrefersTheFirstMatchingRule)
    {
        AppConfig config;
        config.tap.kind = ActionKind::MenuKey;
        config.appRules = { HotkeyRule(L"code", L"Ctrl+P"), HotkeyRule(L"code", L"Ctrl+B") };

        CHECK_EQUAL(std::wstring(L"Ctrl+P"), config.ResolveTap(L"code").hotkey);
    }

    TEST(ResolveTapFallsThroughToTheSinglePressAction)
    {
        AppConfig config;
        config.tap.kind = ActionKind::MenuKey;
        config.appRules = { HotkeyRule(L"code", L"Ctrl+P") };

        CHECK(config.ResolveTap(L"notepad").kind == ActionKind::MenuKey);
        CHECK(config.ResolveTap(L"").kind == ActionKind::MenuKey);
    }

    TEST(AnUnusableRuleIsSkippedRatherThanApplied)
    {
        // A rule pointing at nothing must not shadow the single-press action, or the key would go
        // dead in that one app with no explanation.
        AppConfig config;
        config.tap.kind = ActionKind::MenuKey;
        AppRule empty;
        empty.processName = L"code";
        config.appRules = { empty };

        CHECK(!config.appRules[0].IsUsable());
        CHECK(config.ResolveTap(L"code").kind == ActionKind::MenuKey);
    }

    // ---- what a press means ------------------------------------------------

    /// <summary>The exact text the .NET serialiser writes for a config with the given tap action.</summary>
    std::string Serialised(const char* tapBody)
    {
        return std::string("{\r\n  \"Schema\": 2,\r\n  \"Tap\": ") + tapBody +
            ",\r\n  \"DoubleTap\": { \"Kind\": \"None\", \"Aumid\": null, \"DisplayName\": null, \"Path\": null, "
            "\"Arguments\": null, \"WorkingDirectory\": null, \"Hotkey\": null, \"Behaviour\": \"Always\", \"Label\": null },\r\n"
            "  \"DoubleTapEnabled\": false,\r\n  \"DoubleTapWindowMs\": 350,\r\n  \"AppRules\": [],\r\n"
            "  \"Palette\": [],\r\n  \"TrayIntroShown\": false,\r\n  \"Language\": null\r\n}\r\n";
    }

    TEST(AFreshInstallOpensSettings)
    {
        // No file on disk: the user has never been to the settings window, and a key that appears
        // to do nothing on a brand new install is how people conclude the app is broken.
        AppConfig config;
        CHECK(!config.hasBeenSaved);
        CHECK(config.OutcomeFor(config.tap) == KeyPressOutcome::OpenSettings);
    }

    TEST(ChoosingNothingDoesNothing)
    {
        // Issue #1: this used to open the settings window on every single press.
        auto config = AppConfig::FromJson(Serialised("{ \"Kind\": \"None\" }"));
        CHECK(config.has_value());
        CHECK(config->hasBeenSaved);
        CHECK(config->OutcomeFor(config->tap) == KeyPressOutcome::DoNothing);
    }

    TEST(AConfiguredActionRuns)
    {
        for (const char* kind : { "MenuKey", "Palette" })
        {
            auto config = AppConfig::FromJson(Serialised((std::string("{ \"Kind\": \"") + kind + "\" }").c_str()));
            CHECK(config.has_value());
            CHECK(config->OutcomeFor(config->tap) == KeyPressOutcome::RunAction);
        }
    }

    TEST(AKindChosenWithoutATargetGoesBackToSettings)
    {
        // "Launch an app" with no app picked. The user meant something and did not finish.
        auto config = AppConfig::FromJson(Serialised("{ \"Kind\": \"ShellApp\" }"));
        CHECK(config.has_value());
        CHECK(config->OutcomeFor(config->tap) == KeyPressOutcome::OpenSettings);
    }

    TEST(TheRejectedFixWouldHaveBrokenPerAppRules)
    {
        // If None counted as configured, a rule with no action would become usable and shadow the
        // single-press action, so the key would go dead in that one app with no explanation.
        AppConfig config;
        config.tap.kind = ActionKind::MenuKey;
        AppRule empty;
        empty.processName = L"code";
        config.appRules = { empty };

        CHECK(!config.appRules[0].IsUsable());
        CHECK(config.ResolveTap(L"code").kind == ActionKind::MenuKey);
    }

    // ---- the file --------------------------------------------------------

    TEST(ReadsWhatTheSettingsWindowWrites)
    {
        const char* json =
            "{\r\n"
            "  \"Schema\": 2,\r\n"
            "  \"Tap\": { \"Kind\": \"File\", \"Aumid\": null, \"DisplayName\": null, \"Path\": \"C:\\\\Tools\\\\note pad.exe\", "
            "\"Arguments\": \"--new \\\"Untitled\\\"\", \"WorkingDirectory\": null, \"Hotkey\": null, \"Behaviour\": \"Toggle\", \"Label\": null },\r\n"
            "  \"DoubleTap\": { \"Kind\": \"Hotkey\", \"Hotkey\": \"Ctrl+Shift+Escape\", \"Behaviour\": \"Always\" },\r\n"
            "  \"DoubleTapEnabled\": true,\r\n"
            "  \"DoubleTapWindowMs\": 420,\r\n"
            "  \"AppRules\": [ { \"ProcessName\": \"chrome\", \"Action\": { \"Kind\": \"MenuKey\" } } ],\r\n"
            "  \"Palette\": [ { \"Kind\": \"File\", \"Path\": \"calc.exe\", \"Label\": \"Calculator \\u2014 \\ud83e\\uddee\" } ],\r\n"
            "  \"TrayIntroShown\": true,\r\n"
            "  \"Language\": \"el\"\r\n"
            "}\r\n";

        std::wstring error;
        auto config = AppConfig::FromJson(json, &error);
        CHECK(config.has_value());

        CHECK(config->tap.kind == ActionKind::File);
        CHECK_EQUAL(std::wstring(L"C:\\Tools\\note pad.exe"), config->tap.path);
        CHECK_EQUAL(std::wstring(L"--new \"Untitled\""), config->tap.arguments);
        CHECK(config->tap.behaviour == LaunchBehaviour::Toggle);
        CHECK(config->doubleTap.kind == ActionKind::Hotkey);
        CHECK(config->doubleTapEnabled);
        CHECK_EQUAL(420, config->doubleTapWindowMs);
        CHECK_EQUAL(size_t{ 1 }, config->appRules.size());
        CHECK(config->appRules[0].action.kind == ActionKind::MenuKey);
        CHECK_EQUAL(size_t{ 1 }, config->palette.size());
        CHECK_EQUAL(std::wstring(L"Calculator \u2014 \U0001F9EE"), config->palette[0].label);
        CHECK(config->trayIntroShown);
        CHECK_EQUAL(std::wstring(L"el"), config->language);
    }

    TEST(MissingFileIsNotAnError)
    {
        CHECK(!AppConfig::LoadFrom(L"C:\\this\\does\\not\\exist\\config.json").has_value());
    }

    TEST(GarbageIsRejectedRatherThanThrown)
    {
        for (const char* text : { "", "not json", "[1, 2]", "null", "{\"Tap\": 5}", "{\"DoubleTapWindowMs\": \"soon\"}",
            "{\"Tap\": {\"Kind\": \"Teleport\"}}", "{\"AppRules\": [7]}" })
        {
            CHECK(!AppConfig::FromJson(text).has_value());
        }
    }

    TEST(FillsInWhatAnOlderFileLeftOut)
    {
        auto config = AppConfig::FromJson("{ \"Tap\": { \"Kind\": \"MenuKey\" } }");
        CHECK(config.has_value());
        CHECK_EQUAL(AppConfig::CurrentSchema, config->schema);
        CHECK_EQUAL(350, config->doubleTapWindowMs);
        CHECK(!config->doubleTapEnabled);
        CHECK(config->appRules.empty());
        CHECK(config->palette.empty());
        CHECK(config->doubleTap.kind == ActionKind::None);
    }

    TEST(ClampsAHandEditedDoublePressWindow)
    {
        auto low = AppConfig::FromJson("{ \"DoubleTapWindowMs\": 5 }");
        auto high = AppConfig::FromJson("{ \"DoubleTapWindowMs\": 99999 }");
        CHECK(low.has_value() && high.has_value());
        CHECK_EQUAL(AppConfig::MinDoubleTapWindowMs, low->doubleTapWindowMs);
        CHECK_EQUAL(AppConfig::MaxDoubleTapWindowMs, high->doubleTapWindowMs);
    }

    TEST(ReadsEnumsTheWayTheConverterDoes)
    {
        // Names without regard to case, and bare numbers, both of which JsonStringEnumConverter accepts.
        auto byName = AppConfig::FromJson("{ \"Tap\": { \"Kind\": \"menukey\", \"Behaviour\": \"TOGGLE\" } }");
        auto byNumber = AppConfig::FromJson("{ \"Tap\": { \"Kind\": 3, \"Behaviour\": 2 } }");
        CHECK(byName.has_value() && byNumber.has_value());
        CHECK(byName->tap.kind == ActionKind::MenuKey && byName->tap.behaviour == LaunchBehaviour::Toggle);
        CHECK(byNumber->tap.kind == ActionKind::MenuKey && byNumber->tap.behaviour == LaunchBehaviour::Toggle);
    }

    TEST(MemberNamesAreCaseSensitive)
    {
        // System.Text.Json's default. A lower-case "tap" is an unknown member and is ignored.
        auto config = AppConfig::FromJson("{ \"tap\": { \"Kind\": \"MenuKey\" } }");
        CHECK(config.has_value());
        CHECK(config->tap.kind == ActionKind::None);
    }

    TEST(NullRulesAreDroppedNotFatal)
    {
        auto config = AppConfig::FromJson("{ \"AppRules\": [ null, { \"ProcessName\": \"x\", \"Action\": { \"Kind\": \"MenuKey\" } } ], \"Palette\": [ null ] }");
        CHECK(config.has_value());
        CHECK_EQUAL(size_t{ 1 }, config->appRules.size());
        CHECK(config->palette.empty());
    }

    // ---- json --------------------------------------------------------------

    TEST(JsonIsStrict)
    {
        for (const char* text : { "{\"a\": 1,}", "[1,]", "{'a': 1}", "{\"a\": 1} // note", "{\"a\": 01}", "{\"a\": \"tab\there\"}",
            "{\"a\": \"\\x41\"}", "{\"a\": \"\\ud83e\"}", "\"unterminated", "{\"a\" 1}", "tru", "{\"a\": .5}" })
        {
            CHECK(!Json::Parse(text).has_value());
        }
    }

    TEST(JsonSkipsAByteOrderMark)
    {
        auto value = Json::Parse("\xEF\xBB\xBF{\"a\": true}");
        CHECK(value.has_value());
        CHECK(value->GetBoolean(L"a", false));
    }

    TEST(JsonDecodesEscapesAndUtf8)
    {
        auto value = Json::Parse("{\"s\": \"caf\xC3\xA9 \\u00e9 \\n \\\\ \\/ \\ud83d\\ude00\", \"n\": -12.5e1, \"z\": 0, \"list\": [1, [2, {}], null]}");
        CHECK(value.has_value());
        CHECK_EQUAL(std::wstring(L"caf\u00e9 \u00e9 \n \\ / \U0001F600"), value->GetString(L"s"));
        CHECK_EQUAL(-125.0, value->Get(L"n")->number);
        CHECK_EQUAL(0, value->GetInt(L"z", 9));
        CHECK_EQUAL(size_t{ 3 }, value->Get(L"list")->array.size());
        CHECK(value->Get(L"list")->array[2].IsNull());
        CHECK(value->Get(L"missing") == nullptr);
    }

    TEST(JsonRefusesRunawayNesting)
    {
        std::string deep(200, '[');
        deep += std::string(200, ']');
        CHECK(!Json::Parse(deep).has_value());
    }

    // ---- activation --------------------------------------------------------

    TEST(TheStateComesFromTheUriOrDefaultsToTap)
    {
        CHECK_EQUAL(std::wstring(L"Tap"), Activation::State({}));
        CHECK_EQUAL(std::wstring(L"Tap"), Activation::State({ L"--key" }));
        CHECK_EQUAL(std::wstring(L"Down"), Activation::State({ L"antipilot-key:?state=Down" }));
        CHECK_EQUAL(std::wstring(L"Up"), Activation::State({ L"ANTIPILOT-KEY://?other=1&state=Up" }));
        CHECK_EQUAL(std::wstring(L"Tap"), Activation::State({ L"antipilot-key://?state=" }));
        CHECK_EQUAL(std::wstring(L"Tap"), Activation::State({ L"antipilot-key:" }));
    }

    // ---- text --------------------------------------------------------------

    TEST(TextHelpersBehaveLikeTheirDotNetCounterparts)
    {
        CHECK_EQUAL(std::wstring(L"chrome"), Text::FileNameWithoutExtension(L"C:\\a\\chrome.exe"));
        CHECK_EQUAL(std::wstring(L"foo.bar"), Text::FileNameWithoutExtension(L"foo.bar.exe"));
        CHECK_EQUAL(std::wstring(L"C:\\a"), Text::DirectoryName(L"C:\\a\\chrome.exe"));
        CHECK(Text::IsPathRooted(L"C:\\x") && Text::IsPathRooted(L"\\\\server\\share") && !Text::IsPathRooted(L"notepad"));
        CHECK(Text::IsBlank(L"") && Text::IsBlank(L" \t") && !Text::IsBlank(L" x "));
        CHECK(Text::EqualsIgnoreCase(L"CHROME", L"chrome") && !Text::EqualsIgnoreCase(L"chrome", L"chrom"));

        SetEnvironmentVariableW(L"ANTIPILOT_TEST", L"expanded");
        CHECK_EQUAL(std::wstring(L"a expanded b"), Text::ExpandEnvironment(L"a %ANTIPILOT_TEST% b"));
        CHECK_EQUAL(std::wstring(L"%NOPE_NOT_SET%"), Text::ExpandEnvironment(L"%NOPE_NOT_SET%"));
    }

    // ---- double press ------------------------------------------------------

    constexpr int Window = 400;

    TEST(ALonePressIsASinglePress)
    {
        CHECK(Tap::Classify(Window) == Tap::Press::Single);
    }

    TEST(ASecondPressInsideTheWindowMakesADouble)
    {
        // The coordinator talks between processes through named kernel objects, and those are
        // visible across threads of one process in exactly the same way — so a second thread here
        // stands in for the second key press without needing to spawn anything.
        Tap::Press first = Tap::Press::Single;
        HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);

        std::thread firstPress([&]()
        {
            SetEvent(ready);
            first = Tap::Classify(Window);
        });

        WaitForSingleObject(ready, 5000);
        CloseHandle(ready);

        // Comfortably inside the window, and long enough after it that the first press is
        // certainly the one holding the mutex.
        Sleep(60);
        Tap::Press second = Tap::Classify(Window);

        firstPress.join();
        CHECK(first == Tap::Press::Double);
        CHECK(second == Tap::Press::Handled);
    }

    TEST(APressAfterTheWindowClosesIsItsOwnSinglePress)
    {
        CHECK(Tap::Classify(200) == Tap::Press::Single);

        // The real regression this guards: a stale signal left set by a late second press used to
        // make the *next* press look like a double. It must not.
        CHECK(Tap::Classify(200) == Tap::Press::Single);
        CHECK(Tap::Classify(200) == Tap::Press::Single);
    }

    TEST(TheWaitIsRoughlyTheWindowItWasGiven)
    {
        // This delay is the entire cost of the feature, so it is worth asserting that it is the
        // number the user chose and not, say, twice it.
        ULONGLONG started = GetTickCount64();
        Tap::Classify(300);
        ULONGLONG elapsed = GetTickCount64() - started;
        CHECK(elapsed >= 250 && elapsed <= 1500);
    }
}

int main()
{
    int failures = 0;

    for (const Test& test : Registry())
    {
        try
        {
            test.body();
            std::printf("  ok    %s\n", test.name);
        }
        catch (const Failure& failure)
        {
            ++failures;
            std::printf("  FAIL  %s: %s\n", test.name, failure.message.c_str());
        }
        catch (const std::exception& ex)
        {
            ++failures;
            std::printf("  FAIL  %s: threw %s\n", test.name, ex.what());
        }
    }

    std::printf("\n%zu tests, %d failed\n", Registry().size(), failures);
    return failures;
}
