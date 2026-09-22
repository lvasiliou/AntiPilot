#include "pch.h"
#include "SettingsCard.xaml.h"
#if __has_include("SettingsCard.g.cpp")
#include "SettingsCard.g.cpp"
#endif

#include <winrt/Microsoft.UI.Xaml.Automation.h>

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Media;

namespace winrt::AntiPilot::Shell::implementation
{
    void SettingsCard::InitializeComponent()
    {
        SettingsCardT::InitializeComponent();
        _ready = true;
        Apply();
    }

    void SettingsCard::Apply()
    {
        if (!_ready)
        {
            return;
        }

        IconGlyph().Glyph(_glyph);
        IconGlyph().Visibility(_glyph.empty() ? Visibility::Collapsed : Visibility::Visible);
        TitleText().Text(_title);
        DescriptionText().Text(_description);
        DescriptionText().Visibility(_description.empty() ? Visibility::Collapsed : Visibility::Visible);
        ActionHost().Content(_action);

        // The title is what a screen reader should announce for a control that has no words of its
        // own: a toggle, a slider, a combo. A button already says what it is from its content, and
        // borrowing the title made "Open Windows settings" announce itself as the sentence beside it.
        if (_action && Automation::AutomationProperties::GetName(_action).empty())
        {
            auto content = _action.try_as<Controls::ContentControl>();
            if (!content || !content.Content())
            {
                Automation::AutomationProperties::SetName(_action, _title);
            }
        }

        if (_flat)
        {
            Surface().Background(nullptr);
            Surface().BorderThickness(ThicknessHelper::FromUniformLength(0));
        }
    }
}
