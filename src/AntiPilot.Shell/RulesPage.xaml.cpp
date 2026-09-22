#include "pch.h"
#include "RulesPage.xaml.h"
#if __has_include("RulesPage.g.cpp")
#include "RulesPage.g.cpp"
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
    // A two-column row for the list, matching the header above it.
    Grid Row(hstring const& first, hstring const& second)
    {
        Grid row;
        row.ColumnDefinitions().Append([] { ColumnDefinition c; c.Width(GridLengthHelper::FromPixels(220)); return c; }());
        row.ColumnDefinitions().Append([] { ColumnDefinition c; c.Width(GridLengthHelper::FromValueAndType(1, GridUnitType::Star)); return c; }());

        TextBlock a; a.Text(first); a.TextTrimming(TextTrimming::CharacterEllipsis);
        TextBlock b; b.Text(second); b.TextTrimming(TextTrimming::CharacterEllipsis);
        Grid::SetColumn(b, 1);
        row.Children().Append(a);
        row.Children().Append(b);
        Automation::AutomationProperties::SetName(row, hstring{ std::wstring{ first } + L", " + std::wstring{ second } });
        return row;
    }
}

namespace winrt::AntiPilot::Shell::implementation
{
    void RulesPage::Load(::AntiPilot::AppConfig& config, HWND owner, std::function<void()> onDirty)
    {
        _config = &config;
        _owner = owner;
        _dirty = std::move(onDirty);
        Refresh();
    }

    void RulesPage::Refresh()
    {
        Rules().Items().Clear();
        for (auto const& rule : _config->appRules)
        {
            Rules().Items().Append(Row(hstring{ rule.processName }, Actions::Describe(rule.action)));
        }

        bool none = _config->appRules.empty();
        Empty().Visibility(none ? Visibility::Visible : Visibility::Collapsed);
        Rules().Visibility(none ? Visibility::Collapsed : Visibility::Visible);
    }

    fire_and_forget RulesPage::OnAdd(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        if (_config) co_await EditRule(-1);
    }

    fire_and_forget RulesPage::OnEdit(IInspectable const&, RoutedEventArgs const&)
    {
        auto strong = get_strong();
        if (Rules().SelectedIndex() >= 0)
        {
            co_await EditRule(Rules().SelectedIndex());
        }
    }

    void RulesPage::OnRemove(IInspectable const&, RoutedEventArgs const&)
    {
        int index = _config ? Rules().SelectedIndex() : -1;
        if (index < 0)
        {
            return;
        }

        _config->appRules.erase(_config->appRules.begin() + index);
        Refresh();
        _dirty();
    }

    IAsyncAction RulesPage::EditRule(int index)
    {
        auto strong = get_strong();
        ::AntiPilot::AppRule rule = index < 0 ? ::AntiPilot::AppRule{} : _config->appRules[static_cast<size_t>(index)];

        // The dialog: process name with a "pick a running app" flyout, then the action editor.
        TextBox process;
        process.Text(hstring{ rule.processName });
        process.PlaceholderText(L"chrome");
        Automation::AutomationProperties::SetName(process, Strings::Get(L"PerAppProcess"));

        Button pick;
        pick.Content(box_value(Strings::Get(L"PerAppPickRunning")));
        pick.Click([process](auto&& sender, auto&&)
        {
            MenuFlyout menu;
            for (auto const& [name, title] : Actions::ListWindowedApps())
            {
                MenuFlyoutItem item;
                item.Text(hstring{ title.empty() ? name : name + L"  —  " + title });
                item.Click([process, name](auto&&, auto&&) { process.Text(hstring{ name }); });
                menu.Items().Append(item);
            }

            if (menu.Items().Size() == 0)
            {
                MenuFlyoutItem item;
                item.Text(Strings::Get(L"PerAppNoRules"));
                item.IsEnabled(false);
                menu.Items().Append(item);
            }

            menu.ShowAt(sender.template as<FrameworkElement>());
        });

        Grid nameRow;
        nameRow.ColumnDefinitions().Append([] { ColumnDefinition c; c.Width(GridLengthHelper::FromValueAndType(1, GridUnitType::Star)); return c; }());
        nameRow.ColumnDefinitions().Append([] { ColumnDefinition c; c.Width(GridLengthHelper::Auto()); return c; }());
        nameRow.ColumnSpacing(8);
        Grid::SetColumn(pick, 1);
        nameRow.Children().Append(process);
        nameRow.Children().Append(pick);

        TextBlock hint;
        hint.Text(Strings::Get(L"PerAppProcessHint"));
        hint.TextWrapping(TextWrapping::Wrap);
        hint.Style(Application::Current().Resources().Lookup(box_value(L"CaptionTextBlockStyle")).as<Microsoft::UI::Xaml::Style>());
        hint.Foreground(Application::Current().Resources().Lookup(box_value(L"TextFillColorSecondaryBrush")).as<Media::Brush>());

        TextBlock problem;
        problem.Text(Strings::Get(L"PerAppProcessHint"));
        problem.Foreground(Application::Current().Resources().Lookup(box_value(L"SystemFillColorCriticalBrush")).as<Media::Brush>());
        problem.Visibility(Visibility::Collapsed);

        Shell::ActionEditor editor;
        auto editorImpl = get_self<ActionEditor>(editor);
        editorImpl->Owner(_owner);
        editorImpl->NothingHint(Strings::Get(L"NothingHintTap"));
        editorImpl->Action(rule.action);

        Border card;
        card.Style(Application::Current().Resources().Lookup(box_value(L"Card")).as<Microsoft::UI::Xaml::Style>());
        card.Padding(ThicknessHelper::FromUniformLength(14));
        card.Margin(ThicknessHelper::FromLengths(0, 10, 0, 0));
        card.Child(editor);

        StackPanel body;
        body.Spacing(4);
        body.MinWidth(520);
        TextBlock caption; caption.Text(Strings::Get(L"PerAppProcess"));
        body.Children().Append(caption);
        body.Children().Append(nameRow);
        body.Children().Append(hint);
        body.Children().Append(problem);
        body.Children().Append(card);

        ScrollViewer scroller;
        scroller.Content(body);

        ContentDialog dialog;
        dialog.XamlRoot(XamlRoot());
        dialog.Title(box_value(Strings::Get(index < 0 ? L"PerAppAdd" : L"PerAppEditAction")));
        dialog.Content(scroller);
        dialog.PrimaryButtonText(Strings::Get(L"Ok"));
        dialog.CloseButtonText(Strings::Get(L"Cancel"));
        dialog.DefaultButton(ContentDialogButton::Primary);

        // A blank process name keeps the dialog open with the hint shown as the reason.
        dialog.PrimaryButtonClick([process, problem](auto&&, ContentDialogButtonClickEventArgs const& e)
        {
            bool blank = ::AntiPilot::Text::IsBlank(std::wstring{ process.Text() });
            problem.Visibility(blank ? Visibility::Visible : Visibility::Collapsed);
            e.Cancel(blank);
        });

        if (co_await dialog.ShowAsync() != ContentDialogResult::Primary)
        {
            co_return;
        }

        rule.processName = ::AntiPilot::AppRule::Normalise(std::wstring{ process.Text() });
        rule.action = editorImpl->Action();

        if (index < 0)
        {
            for (auto const& existing : _config->appRules)
            {
                if (existing.Matches(rule.processName))
                {
                    co_await Tell(Strings::Get(L"PerAppDuplicate"));
                    co_return;
                }
            }

            _config->appRules.push_back(rule);
        }
        else
        {
            _config->appRules[static_cast<size_t>(index)] = rule;
        }

        Refresh();
        _dirty();
    }

    IAsyncAction RulesPage::Tell(hstring body)
    {
        ContentDialog dialog;
        dialog.XamlRoot(XamlRoot());
        dialog.Title(box_value(Strings::Get(L"AppName")));
        dialog.Content(box_value(body));
        dialog.CloseButtonText(Strings::Get(L"Ok"));
        co_await dialog.ShowAsync();
    }
}
