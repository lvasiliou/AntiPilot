#include "pch.h"
#include "AppPicker.h"

#include "Strings.h"
#include "Text.h"

#include <winrt/Microsoft.UI.Xaml.Media.Imaging.h>

#include <map>

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace winrt::Microsoft::UI::Xaml::Media::Imaging;
using namespace winrt::Windows::Foundation;
using namespace ::AntiPilot::Shell;

namespace
{
    bool Matches(Apps::Entry const& app, std::wstring const& filter)
    {
        if (filter.empty())
        {
            return true;
        }

        auto lower = ::AntiPilot::Text::ToLowerInvariant(app.name);
        return lower.find(filter) != std::wstring::npos;
    }
}

namespace AntiPilot::Shell::AppPicker
{
    IAsyncOperation<SoftwareBitmapSource> IconFor(std::wstring parsingName, int size)
    {
        auto queue = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
        co_await resume_background();
        auto bitmap = Apps::TryGetIcon(parsingName, size);
        co_await wil::resume_foreground(queue);

        if (!bitmap)
        {
            co_return nullptr;
        }

        SoftwareBitmapSource source;
        co_await source.SetBitmapAsync(bitmap);
        co_return source;
    }

    IAsyncOperation<bool> Show(XamlRoot root, std::wstring current, std::shared_ptr<Apps::Entry> picked)
    {
        auto queue = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
        const int iconSize = static_cast<int>(24 * root.RasterizationScale());

        TextBox search;
        search.PlaceholderText(Strings::Get(L"PickerSearch"));
        Automation::AutomationProperties::SetName(search, Strings::Get(L"PickerSearch"));

        ListView list;
        list.SelectionMode(ListViewSelectionMode::Single);
        list.MinWidth(480);
        list.MaxHeight(380);
        Automation::AutomationProperties::SetName(list, Strings::Get(L"PickerColumnApp"));

        TextBlock status;
        status.Text(Strings::Get(L"PickerLoading"));
        status.Style(Application::Current().Resources().Lookup(box_value(L"CaptionTextBlockStyle")).as<Microsoft::UI::Xaml::Style>());
        status.Foreground(Application::Current().Resources().Lookup(box_value(L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        status.Margin(ThicknessHelper::FromLengths(2, 6, 0, 0));

        StackPanel body;
        body.Spacing(8);
        body.Children().Append(search);
        body.Children().Append(list);
        body.Children().Append(status);

        ContentDialog dialog;
        dialog.XamlRoot(root);
        dialog.Title(box_value(Strings::Get(L"PickerTitle")));
        dialog.Content(body);
        dialog.PrimaryButtonText(Strings::Get(L"PickerSelect"));
        dialog.CloseButtonText(Strings::Get(L"Cancel"));
        dialog.IsPrimaryButtonEnabled(false);
        dialog.DefaultButton(ContentDialogButton::Primary);

        // Shared with the lambdas below; they outlive this frame only until the dialog closes.
        auto apps = std::make_shared<std::vector<Apps::Entry>>();
        auto icons = std::make_shared<std::map<std::wstring, SoftwareBitmapSource>>();
        auto images = std::make_shared<std::map<std::wstring, Image>>();
        auto open = std::make_shared<bool>(true);

        auto fill = [=]
        {
            std::wstring filter = ::AntiPilot::Text::ToLowerInvariant(::AntiPilot::Text::Trim(std::wstring{ search.Text() }));
            list.Items().Clear();
            images->clear();
            int shown = 0;

            for (auto const& app : *apps)
            {
                if (!Matches(app, filter))
                {
                    continue;
                }

                Image icon;
                icon.Width(24);
                icon.Height(24);
                if (auto found = icons->find(app.parsingName); found != icons->end())
                {
                    icon.Source(found->second);
                }
                (*images)[app.parsingName] = icon;

                TextBlock name;
                name.Text(hstring{ app.name });
                name.VerticalAlignment(VerticalAlignment::Center);

                StackPanel row;
                row.Orientation(Orientation::Horizontal);
                row.Spacing(10);
                row.Children().Append(icon);
                row.Children().Append(name);
                row.Tag(box_value(hstring{ app.parsingName }));
                Automation::AutomationProperties::SetName(row, hstring{ app.name });
                list.Items().Append(row);

                if (::AntiPilot::Text::EqualsIgnoreCase(app.parsingName, current))
                {
                    list.SelectedIndex(shown);
                }
                shown++;
            }

            status.Text(shown == 0 && !apps->empty() ? Strings::Get(L"PickerNoMatch") : Strings::Format(L"PickerCount", shown));
        };

        search.TextChanged([fill](auto&&, auto&&) { fill(); });
        list.SelectionChanged([dialog, list](auto&&, auto&&) { dialog.IsPrimaryButtonEnabled(list.SelectedIndex() >= 0); });
        list.DoubleTapped([dialog, list](auto&&, auto&&)
        {
            if (list.SelectedIndex() >= 0)
            {
                dialog.Hide();
            }
        });

        // The list shows up first; icons come from the shell one at a time and arrive as they do.
        auto loadIcons = [=]() -> fire_and_forget
        {
            auto snapshot = *apps;
            for (auto const& app : snapshot)
            {
                if (!*open)
                {
                    co_return;
                }

                co_await resume_background();
                auto bitmap = Apps::TryGetIcon(app.parsingName, iconSize);
                co_await wil::resume_foreground(queue);

                if (!bitmap || !*open)
                {
                    continue;
                }

                SoftwareBitmapSource source;
                co_await source.SetBitmapAsync(bitmap);
                (*icons)[app.parsingName] = source;
                if (auto image = images->find(app.parsingName); image != images->end())
                {
                    image->second.Source(source);
                }
            }
        };

        // Enumerating takes a moment, so the dialog opens first and fills in.
        auto load = [=]() -> fire_and_forget
        {
            co_await resume_background();
            auto found = Apps::Enumerate();
            co_await wil::resume_foreground(queue);

            *apps = std::move(found);
            if (apps->empty())
            {
                status.Text(Strings::Get(L"PickerFailed"));
                co_return;
            }

            fill();
            loadIcons();
        };
        load();

        bool chosen = false;
        auto result = co_await dialog.ShowAsync();
        *open = false;

        // Hide() from a double-tap reports None; the selection is the answer either way.
        if ((result == ContentDialogResult::Primary || result == ContentDialogResult::None) && list.SelectedIndex() >= 0)
        {
            auto tag = unbox_value<hstring>(list.SelectedItem().as<FrameworkElement>().Tag());
            for (auto const& app : *apps)
            {
                if (app.parsingName == std::wstring{ tag })
                {
                    *picked = app;
                    chosen = true;
                    break;
                }
            }
        }

        co_return chosen;
    }
}
