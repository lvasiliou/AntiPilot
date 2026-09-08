#pragma once

#include <string>
#include <string_view>

namespace AntiPilot::Log
{
    /// <summary>
    /// Appends one line to the shared log. Never throws and never blocks a key press for long: a
    /// line that cannot be written inside two seconds is dropped.
    /// </summary>
    void Write(std::wstring_view message);

    std::wstring Path();

    /// <summary>The previous log. Kept so a rotation does not throw away the evidence.</summary>
    std::wstring PreviousPath();
}
