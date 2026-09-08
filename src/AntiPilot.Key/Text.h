#pragma once

#include <string>
#include <string_view>

namespace AntiPilot::Text
{
    /// <summary>UTF-8 bytes to UTF-16. Invalid sequences become U+FFFD rather than an error.</summary>
    std::wstring FromUtf8(std::string_view utf8);

    std::string ToUtf8(std::wstring_view text);

    /// <summary>Ordinal, case-insensitive: the comparison the config's process names are matched with.</summary>
    bool EqualsIgnoreCase(std::wstring_view left, std::wstring_view right);

    bool EndsWithIgnoreCase(std::wstring_view text, std::wstring_view suffix);

    bool StartsWithIgnoreCase(std::wstring_view text, std::wstring_view prefix);

    std::wstring Trim(std::wstring_view text);

    /// <summary>True for null-or-whitespace in the C# sense, which is what every "is this set" check means.</summary>
    bool IsBlank(std::wstring_view text);

    std::wstring ToLowerInvariant(std::wstring_view text);

    /// <summary>%VAR% expansion. Unknown variables are left as written, as ExpandEnvironmentStrings does.</summary>
    std::wstring ExpandEnvironment(std::wstring_view text);

    /// <summary>The file name at the end of a path, without its extension. "C:\x\chrome.exe" gives "chrome".</summary>
    std::wstring FileNameWithoutExtension(std::wstring_view path);

    /// <summary>Everything before the last separator, or empty when there is none.</summary>
    std::wstring DirectoryName(std::wstring_view path);

    /// <summary>"C:\..." or "\\server\..." — the shapes Path.IsPathRooted accepts.</summary>
    bool IsPathRooted(std::wstring_view path);

    /// <summary>The text of a Win32 error, without the trailing newline FormatMessage adds.</summary>
    std::wstring DescribeError(unsigned long error);
}
