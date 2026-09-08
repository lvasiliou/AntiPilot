#pragma once

#include <string>
#include <vector>

namespace AntiPilot::Activation
{
    /// <summary>The arguments this process was started with, program name excluded.</summary>
    std::vector<std::wstring> Arguments();

    /// <summary>
    /// The press state Windows reported. A tap by AUMID arrives with no arguments at all; press-and-
    /// hold arrives as a URI, antipilot-key:?state=Down then state=Up. Anything unrecognised is a Tap,
    /// which is the only state that matters.
    /// </summary>
    std::wstring State(const std::vector<std::wstring>& arguments);

    /// <summary>The manifest entry this process was started under, or empty when unpackaged.</summary>
    std::wstring CurrentAumid();
}
