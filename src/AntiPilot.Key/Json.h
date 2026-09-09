#pragma once

#include <optional>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace AntiPilot::Json
{
    /// <summary>
    /// One parsed JSON value. A small tree rather than a streaming reader, because the config is a
    /// few hundred bytes and being able to ask "what is at Tap.Kind" is worth more than the copy.
    /// </summary>
    struct Value
    {
        enum class Type
        {
            Null,
            Boolean,
            Number,
            String,
            Array,
            Object,
        };

        Type type = Type::Null;
        bool boolean = false;
        double number = 0;
        std::wstring string;
        std::vector<Value> array;

        /// <summary>Insertion order is kept; lookups are linear, which is right for objects this small.</summary>
        std::vector<std::pair<std::wstring, Value>> object;

        bool IsNull() const { return type == Type::Null; }
        bool IsObject() const { return type == Type::Object; }
        bool IsArray() const { return type == Type::Array; }
        bool IsString() const { return type == Type::String; }
        bool IsNumber() const { return type == Type::Number; }
        bool IsBoolean() const { return type == Type::Boolean; }

        /// <summary>The member of that name, or null when this is not an object or has no such member.</summary>
        const Value* Get(std::wstring_view key) const;

        /// <summary>A string member, or the fallback when it is missing, null, or not a string.</summary>
        std::wstring GetString(std::wstring_view key, std::wstring_view fallback = {}) const;

        bool GetBoolean(std::wstring_view key, bool fallback) const;

        int GetInt(std::wstring_view key, int fallback) const;
    };

    /// <summary>
    /// Parses UTF-8 text as strict RFC 8259 JSON. The only producer of this text is AntiPilot's own
    /// serialiser, so there is nothing to gain by tolerating comments or trailing commas, and a
    /// reader that accepted them would hide a corrupt file behind a wrong answer instead of a clear
    /// refusal. A leading byte-order mark is skipped.
    /// </summary>
    /// <returns>The document, or nothing with <paramref name="error"/> describing the first problem.</returns>
    std::optional<Value> Parse(std::string_view utf8, std::wstring* error = nullptr);
}
