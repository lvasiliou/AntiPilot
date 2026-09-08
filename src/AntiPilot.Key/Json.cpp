#include "Json.h"

#include "Text.h"

#include <cmath>
#include <cstdlib>
#include <format>

namespace AntiPilot::Json
{
    namespace
    {
        /// <summary>Deep enough for any config, shallow enough that a hostile file cannot exhaust the stack.</summary>
        constexpr int MaxDepth = 64;

        class Reader
        {
        public:
            explicit Reader(std::string_view text) : _text(text)
            {
                if (_text.size() >= 3 &&
                    static_cast<unsigned char>(_text[0]) == 0xEF &&
                    static_cast<unsigned char>(_text[1]) == 0xBB &&
                    static_cast<unsigned char>(_text[2]) == 0xBF)
                {
                    _position = 3;
                }
            }

            std::optional<Value> ReadDocument()
            {
                SkipWhitespace();
                Value value;
                if (!ReadValue(value, 0))
                {
                    return std::nullopt;
                }

                SkipWhitespace();
                if (_position != _text.size())
                {
                    return Fail(L"unexpected text after the document");
                }

                return value;
            }

            const std::wstring& Error() const { return _error; }

        private:
            std::string_view _text;
            size_t _position = 0;
            std::wstring _error;

            std::optional<Value> Fail(std::wstring_view message)
            {
                if (_error.empty())
                {
                    _error = std::format(L"{} at offset {}", message, _position);
                }

                return std::nullopt;
            }

            bool FailValue(std::wstring_view message)
            {
                Fail(message);
                return false;
            }

            bool AtEnd() const { return _position >= _text.size(); }

            char Peek() const { return AtEnd() ? '\0' : _text[_position]; }

            void SkipWhitespace()
            {
                while (!AtEnd())
                {
                    char c = _text[_position];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        ++_position;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            bool Expect(char wanted)
            {
                if (Peek() != wanted)
                {
                    return FailValue(std::format(L"expected '{}'", static_cast<wchar_t>(wanted)));
                }

                ++_position;
                return true;
            }

            bool ReadValue(Value& value, int depth)
            {
                if (depth > MaxDepth)
                {
                    return FailValue(L"nested too deeply");
                }

                switch (Peek())
                {
                case '{':
                    return ReadObject(value, depth);
                case '[':
                    return ReadArray(value, depth);
                case '"':
                    value.type = Value::Type::String;
                    return ReadString(value.string);
                case 't':
                    return ReadLiteral("true", value, Value::Type::Boolean, true);
                case 'f':
                    return ReadLiteral("false", value, Value::Type::Boolean, false);
                case 'n':
                    return ReadLiteral("null", value, Value::Type::Null, false);
                default:
                    return ReadNumber(value);
                }
            }

            bool ReadLiteral(std::string_view word, Value& value, Value::Type type, bool boolean)
            {
                if (_text.substr(_position, word.size()) != word)
                {
                    return FailValue(L"unrecognised value");
                }

                _position += word.size();
                value.type = type;
                value.boolean = boolean;
                return true;
            }

            bool ReadObject(Value& value, int depth)
            {
                value.type = Value::Type::Object;
                ++_position;
                SkipWhitespace();

                if (Peek() == '}')
                {
                    ++_position;
                    return true;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"')
                    {
                        return FailValue(L"expected a member name");
                    }

                    std::wstring key;
                    if (!ReadString(key))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (!Expect(':'))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    Value member;
                    if (!ReadValue(member, depth + 1))
                    {
                        return false;
                    }

                    value.object.emplace_back(std::move(key), std::move(member));

                    SkipWhitespace();
                    if (Peek() == ',')
                    {
                        ++_position;
                        continue;
                    }

                    return Expect('}');
                }
            }

            bool ReadArray(Value& value, int depth)
            {
                value.type = Value::Type::Array;
                ++_position;
                SkipWhitespace();

                if (Peek() == ']')
                {
                    ++_position;
                    return true;
                }

                while (true)
                {
                    SkipWhitespace();
                    Value item;
                    if (!ReadValue(item, depth + 1))
                    {
                        return false;
                    }

                    value.array.push_back(std::move(item));

                    SkipWhitespace();
                    if (Peek() == ',')
                    {
                        ++_position;
                        continue;
                    }

                    return Expect(']');
                }
            }

            bool ReadHex4(unsigned& code)
            {
                if (_position + 4 > _text.size())
                {
                    return FailValue(L"truncated \\u escape");
                }

                code = 0;
                for (int i = 0; i < 4; ++i)
                {
                    char c = _text[_position++];
                    unsigned digit;
                    if (c >= '0' && c <= '9') { digit = static_cast<unsigned>(c - '0'); }
                    else if (c >= 'a' && c <= 'f') { digit = static_cast<unsigned>(c - 'a' + 10); }
                    else if (c >= 'A' && c <= 'F') { digit = static_cast<unsigned>(c - 'A' + 10); }
                    else { return FailValue(L"bad \\u escape"); }

                    code = (code << 4) | digit;
                }

                return true;
            }

            /// <summary>
            /// Reads a string into UTF-16. The raw bytes between escapes are UTF-8 and are converted
            /// in one go per run; escapes are decoded here, including surrogate pairs written as two
            /// \u escapes, which is how the serialiser writes anything outside the BMP.
            /// </summary>
            bool ReadString(std::wstring& out)
            {
                ++_position;
                out.clear();

                size_t runStart = _position;
                auto flushRun = [&]()
                {
                    if (_position > runStart)
                    {
                        out += Text::FromUtf8(_text.substr(runStart, _position - runStart));
                    }
                };

                while (true)
                {
                    if (AtEnd())
                    {
                        return FailValue(L"unterminated string");
                    }

                    char c = _text[_position];

                    if (c == '"')
                    {
                        flushRun();
                        ++_position;
                        return true;
                    }

                    if (static_cast<unsigned char>(c) < 0x20)
                    {
                        return FailValue(L"control character in string");
                    }

                    if (c != '\\')
                    {
                        ++_position;
                        continue;
                    }

                    flushRun();
                    ++_position;
                    if (AtEnd())
                    {
                        return FailValue(L"unterminated escape");
                    }

                    char escape = _text[_position++];
                    switch (escape)
                    {
                    case '"': out += L'"'; break;
                    case '\\': out += L'\\'; break;
                    case '/': out += L'/'; break;
                    case 'b': out += L'\b'; break;
                    case 'f': out += L'\f'; break;
                    case 'n': out += L'\n'; break;
                    case 'r': out += L'\r'; break;
                    case 't': out += L'\t'; break;
                    case 'u':
                    {
                        unsigned code;
                        if (!ReadHex4(code))
                        {
                            return false;
                        }

                        if (code >= 0xD800 && code <= 0xDBFF)
                        {
                            // A high surrogate must be followed by its low half as another escape.
                            if (_text.substr(_position, 2) != "\\u")
                            {
                                return FailValue(L"lone high surrogate");
                            }

                            _position += 2;
                            unsigned low;
                            if (!ReadHex4(low) || low < 0xDC00 || low > 0xDFFF)
                            {
                                return FailValue(L"bad low surrogate");
                            }

                            out += static_cast<wchar_t>(code);
                            out += static_cast<wchar_t>(low);
                        }
                        else if (code >= 0xDC00 && code <= 0xDFFF)
                        {
                            return FailValue(L"lone low surrogate");
                        }
                        else
                        {
                            out += static_cast<wchar_t>(code);
                        }

                        break;
                    }
                    default:
                        return FailValue(L"unknown escape");
                    }

                    runStart = _position;
                }
            }

            bool ReadNumber(Value& value)
            {
                size_t start = _position;

                if (Peek() == '-')
                {
                    ++_position;
                }

                if (Peek() == '0')
                {
                    ++_position;
                }
                else if (Peek() >= '1' && Peek() <= '9')
                {
                    while (Peek() >= '0' && Peek() <= '9') { ++_position; }
                }
                else
                {
                    return FailValue(L"unrecognised value");
                }

                if (Peek() == '.')
                {
                    ++_position;
                    if (Peek() < '0' || Peek() > '9')
                    {
                        return FailValue(L"digits expected after the decimal point");
                    }

                    while (Peek() >= '0' && Peek() <= '9') { ++_position; }
                }

                if (Peek() == 'e' || Peek() == 'E')
                {
                    ++_position;
                    if (Peek() == '+' || Peek() == '-') { ++_position; }
                    if (Peek() < '0' || Peek() > '9')
                    {
                        return FailValue(L"digits expected in the exponent");
                    }

                    while (Peek() >= '0' && Peek() <= '9') { ++_position; }
                }

                // The token has been validated character by character, so strtod cannot read past it.
                std::string token(_text.substr(start, _position - start));
                value.type = Value::Type::Number;
                value.number = std::strtod(token.c_str(), nullptr);
                return std::isfinite(value.number) || FailValue(L"number out of range");
            }
        };
    }

    const Value* Value::Get(std::wstring_view key) const
    {
        if (type != Type::Object)
        {
            return nullptr;
        }

        for (const auto& [name, member] : object)
        {
            if (name == key)
            {
                return &member;
            }
        }

        return nullptr;
    }

    std::wstring Value::GetString(std::wstring_view key, std::wstring_view fallback) const
    {
        const Value* member = Get(key);
        return member != nullptr && member->IsString() ? member->string : std::wstring(fallback);
    }

    bool Value::GetBoolean(std::wstring_view key, bool fallback) const
    {
        const Value* member = Get(key);
        return member != nullptr && member->IsBoolean() ? member->boolean : fallback;
    }

    int Value::GetInt(std::wstring_view key, int fallback) const
    {
        const Value* member = Get(key);
        if (member == nullptr || !member->IsNumber())
        {
            return fallback;
        }

        double n = member->number;
        if (n < -2147483648.0 || n > 2147483647.0)
        {
            return fallback;
        }

        return static_cast<int>(n);
    }

    std::optional<Value> Parse(std::string_view utf8, std::wstring* error)
    {
        Reader reader(utf8);
        auto result = reader.ReadDocument();
        if (!result && error != nullptr)
        {
            *error = reader.Error();
        }

        return result;
    }
}
