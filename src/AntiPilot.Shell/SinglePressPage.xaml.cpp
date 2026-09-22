#include "pch.h"
#include "SinglePressPage.xaml.h"
#if __has_include("SinglePressPage.g.cpp")
#include "SinglePressPage.g.cpp"
#endif

#include "Strings.h"

namespace winrt::AntiPilot::Shell::implementation
{
    void SinglePressPage::Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty)
    {
        auto editor = get_self<ActionEditor>(Editor());
        editor->Owner(owner);
        editor->NothingHint(::AntiPilot::Shell::Strings::Get(L"NothingHintTap"));
        editor->Action(config.tap);
        editor->OnChanged([&config, editor, onDirty = std::move(onDirty)]
        {
            config.tap = editor->Action();
            onDirty();
        });
    }
}
