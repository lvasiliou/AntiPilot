#include "pch.h"
#include "PalettePage.xaml.h"
#if __has_include("PalettePage.g.cpp")
#include "PalettePage.g.cpp"
#endif

#include "Actions.h"
#include "Strings.h"
#include "Text.h"

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace winrt::Windows::Foundation;
using namespace ::AntiPilot::Shell;

namespace
{
    ColumnDefinition Column(double pixels)
    {
        ColumnDefinition c;
        c.Width(pixels > 0 ? GridLengthHelper::FromPixels(pixels) : GridLengthHelper::FromValueAndType(1, GridUnitType::Star));
        return c;
    }

    Grid Row(hstring const& number, hstring const& label, hstring const& target)
    {
        Grid row;
        row.ColumnDefinitions().Append(Column(40));
        row.ColumnDefinitions().Append(Column(220));
        row.ColumnDefinitions().Append(Column(0));

        int column = 0;
        for (auto const& text : { number, label, target })
        {
            TextBlock block;
            block.Text(text);
            block.TextTrimming(TextTrimming::CharacterEllipsis);
            Grid::SetColumn(block, column++);
            row.Children().Append(block);
        }

        Automation::AutomationProperties::SetName(row, hstring{ std::wstring{ number } + L" " + std::wstring{ label } + L", " + std::wstring{ target } });
        return row;
    }
}

namespace winrt::AntiPilot::Shell::implementation
{
    void PalettePage::Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty)
    {
        _config = &config;
        _owner = owner;
        _dirty = std::move(onDirty);
        Refresh();
    }

    void PalettePage::Refresh()
    {
        Entries().Items().Clear();
        for (size_t i = 0; i < _config->palette.size(); i++)
        {
            auto const& entry = _config->palette[i];
            // Only the first nine get a number, because only those can be run by pressing one.
            hstring number = i < 9 ? hstring{ std::to_wstring(i + 1) } : hstring{};
            Entries().Items().Append(Row(number, Actions::Describe(entry), Actions::DescribeTarget(entry)));
        }

        bool none = _config->palette.empty();
        Empty().Visibility(none ? Visibility::Visible : Visibility::Collapsed);
        Entries().Visibility(none ? Visibility::Collapsed : Visibility::Visible);
    }

    fire_and_forget PalettePage::OnAdd(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        if (_config) co_await EditEntry(-1);
    }

    fire_and_forget PalettePage::OnEdit(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        if (Entries().SelectedIndex() >= 0)
        {
            co_await EditEntry(Entries().SelectedIndex());
        }
    }

    void PalettePage::OnRemove(IInspectable const&, RoutedEventArgs const&)
    {
        int index = _config ? Entries().SelectedIndex() : -1;
        if (index < 0)
        {
            return;
        }

        _config->palette.erase(_config->palette.begin() + index);
        Refresh();
        _dirty();
    }

    void PalettePage::OnMoveUp(IInspectable const&, RoutedEventArgs const&) { Move(-1); }
    void PalettePage::OnMoveDown(IInspectable const&, RoutedEventArgs const&) { Move(+1); }

    void PalettePage::Move(int delta)
    {
        int index = _config ? Entries().SelectedIndex() : -1;
        int target = index + delta;
        if (index < 0 || target < 0 || target >= static_cast<int>(_config->palette.size()))
        {
            return;
        }

        std::swap(_config->palette[static_cast<size_t>(index)], _config->palette[static_cast<size_t>(target)]);
        Refresh();
        Entries().SelectedIndex(target);
        _dirty();
    }

    IAsyncAction PalettePage::EditEntry(int index)
    {
        auto strong = get_strong();
        ::AntiPilot::KeyAction entry = index < 0 ? ::AntiPilot::KeyAction{} : _config->palette[static_cast<size_t>(index)];

        TextBox label;
        label.Text(hstring{ entry.label });
        label.PlaceholderText(Actions::Describe(entry));
        Automation::AutomationProperties::SetName(label, Strings::Get(L"PaletteLabelCaption"));

        TextBlock problem;
        problem.Text(Strings::Get(L"NothingConfiguredYet"));
        problem.Foreground(Application::Current().Resources().Lookup(box_value(L"SystemFillColorCriticalBrush")).as<Media::Brush>());
        problem.Visibility(Visibility::Collapsed);

        // A palette entry that opened the palette would be a loop, so that mode is not on offer.
        Shell::ActionEditor editor;
        auto editorImpl = get_self<ActionEditor>(editor);
        editorImpl->AllowPalette(false);
        editorImpl->Owner(_owner);
        editorImpl->NothingHint(Strings::Get(L"NothingHintTap"));
        editorImpl->Action(entry);

        Border card;
        card.Style(Application::Current().Resources().Lookup(box_value(L"Card")).as<Microsoft::UI::Xaml::Style>());
        card.Padding(ThicknessHelper::FromUniformLength(14));
        card.Margin(ThicknessHelper::FromLengths(0, 10, 0, 0));
        card.Child(editor);

        StackPanel body;
        body.Spacing(4);
        body.MinWidth(520);
        TextBlock caption; caption.Text(Strings::Get(L"PaletteLabelCaption"));
        body.Children().Append(caption);
        body.Children().Append(label);
        body.Children().Append(problem);
        body.Children().Append(card);

        ScrollViewer scroller;
        scroller.Content(body);

        ContentDialog dialog;
        dialog.XamlRoot(XamlRoot());
        dialog.Title(box_value(Strings::Get(index < 0 ? L"PaletteAdd" : L"Edit")));
        dialog.Content(scroller);
        dialog.PrimaryButtonText(Strings::Get(L"Ok"));
        dialog.CloseButtonText(Strings::Get(L"Cancel"));
        dialog.DefaultButton(ContentDialogButton::Primary);

        // An entry that does nothing is not worth a row; keep the dialog open and say so.
        dialog.PrimaryButtonClick([editorImpl, problem](auto&&, ContentDialogButtonClickEventArgs const& e)
        {
            bool unconfigured = !editorImpl->Action().IsConfigured();
            problem.Visibility(unconfigured ? Visibility::Visible : Visibility::Collapsed);
            e.Cancel(unconfigured);
        });

        if (co_await dialog.ShowAsync() != ContentDialogResult::Primary)
        {
            co_return;
        }

        entry = editorImpl->Action();
        // An empty name is not an error: Describe already produces something sensible.
        entry.label = ::AntiPilot::Text::Trim(std::wstring{ label.Text() });

        if (index < 0)
        {
            _config->palette.push_back(entry);
        }
        else
        {
            _config->palette[static_cast<size_t>(index)] = entry;
        }

        Refresh();
        _dirty();
    }
}
