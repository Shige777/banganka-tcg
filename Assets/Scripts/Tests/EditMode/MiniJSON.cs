/*
 * MiniJSON — Minimal JSON parser/serializer for Unity
 * Based on the public domain MiniJSON by Calvin Rien
 * https://gist.github.com/darktable/1411710
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Banganka.Tests.EditMode
{
    public static class Json
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }

        sealed class Parser : IDisposable
        {
            const string WORD_BREAK = "{}[],:\"";

            StringReader _json;

            Parser(string jsonString)
            {
                _json = new StringReader(jsonString);
            }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                {
                    return instance.ParseValue();
                }
            }

            public void Dispose()
            {
                _json.Dispose();
                _json = null;
            }

            char PeekChar => Convert.ToChar(_json.Peek());
            char NextChar => Convert.ToChar(_json.Read());

            string NextWord
            {
                get
                {
                    var word = new StringBuilder();
                    while (!IsWordBreak(PeekChar))
                    {
                        word.Append(NextChar);
                        if (_json.Peek() == -1) break;
                    }
                    return word.ToString();
                }
            }

            enum TOKEN
            {
                NONE,
                CURLY_OPEN,
                CURLY_CLOSE,
                SQUARED_OPEN,
                SQUARED_CLOSE,
                COLON,
                COMMA,
                STRING,
                NUMBER,
                TRUE,
                FALSE,
                NULL
            }

            bool IsWordBreak(char c) => char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;

            void EatWhitespace()
            {
                while (char.IsWhiteSpace(PeekChar))
                {
                    _json.Read();
                    if (_json.Peek() == -1) break;
                }
            }

            TOKEN NextToken
            {
                get
                {
                    EatWhitespace();
                    if (_json.Peek() == -1) return TOKEN.NONE;

                    switch (PeekChar)
                    {
                        case '{': return TOKEN.CURLY_OPEN;
                        case '}': _json.Read(); return TOKEN.CURLY_CLOSE;
                        case '[': return TOKEN.SQUARED_OPEN;
                        case ']': _json.Read(); return TOKEN.SQUARED_CLOSE;
                        case ',': _json.Read(); return TOKEN.COMMA;
                        case '"': return TOKEN.STRING;
                        case ':': return TOKEN.COLON;
                        case '-':
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            return TOKEN.NUMBER;
                    }

                    switch (NextWord)
                    {
                        case "false": return TOKEN.FALSE;
                        case "true": return TOKEN.TRUE;
                        case "null": return TOKEN.NULL;
                    }
                    return TOKEN.NONE;
                }
            }

            object ParseValue()
            {
                var token = NextToken;
                return ParseByToken(token);
            }

            object ParseByToken(TOKEN token)
            {
                switch (token)
                {
                    case TOKEN.STRING: return ParseString();
                    case TOKEN.NUMBER: return ParseNumber();
                    case TOKEN.CURLY_OPEN: return ParseObject();
                    case TOKEN.SQUARED_OPEN: return ParseArray();
                    case TOKEN.TRUE: return true;
                    case TOKEN.FALSE: return false;
                    case TOKEN.NULL: return null;
                    default: return null;
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                _json.Read(); // {

                while (true)
                {
                    var token = NextToken;
                    switch (token)
                    {
                        case TOKEN.NONE: return null;
                        case TOKEN.COMMA: continue;
                        case TOKEN.CURLY_CLOSE: return table;
                        default:
                            string name = ParseString();
                            if (name == null) return null;

                            token = NextToken;
                            if (token != TOKEN.COLON) return null;
                            _json.Read(); // :

                            table[name] = ParseValue();
                            break;
                    }
                }
            }

            List<object> ParseArray()
            {
                var array = new List<object>();
                _json.Read(); // [

                var parsing = true;
                while (parsing)
                {
                    var token = NextToken;
                    switch (token)
                    {
                        case TOKEN.NONE: return null;
                        case TOKEN.COMMA: continue;
                        case TOKEN.SQUARED_CLOSE: parsing = false; break;
                        default:
                            array.Add(ParseByToken(token));
                            break;
                    }
                }
                return array;
            }

            string ParseString()
            {
                _json.Read(); // "
                var s = new StringBuilder();
                bool parsing = true;

                while (parsing)
                {
                    if (_json.Peek() == -1)
                    {
                        parsing = false;
                        break;
                    }

                    char c = NextChar;
                    switch (c)
                    {
                        case '"': parsing = false; break;
                        case '\\':
                            if (_json.Peek() == -1)
                            {
                                parsing = false;
                                break;
                            }
                            c = NextChar;
                            switch (c)
                            {
                                case '"':
                                case '\\':
                                case '/': s.Append(c); break;
                                case 'b': s.Append('\b'); break;
                                case 'f': s.Append('\f'); break;
                                case 'n': s.Append('\n'); break;
                                case 'r': s.Append('\r'); break;
                                case 't': s.Append('\t'); break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default: s.Append(c); break;
                    }
                }
                return s.ToString();
            }

            object ParseNumber()
            {
                string number = NextWord;
                if (number.IndexOf('.') == -1 && number.IndexOf('E') == -1 && number.IndexOf('e') == -1)
                {
                    if (long.TryParse(number, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out long parsedLong))
                        return parsedLong;
                }
                if (double.TryParse(number, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsedDouble))
                    return parsedDouble;
                return null;
            }
        }

        sealed class Serializer
        {
            StringBuilder _builder;

            Serializer() { _builder = new StringBuilder(); }

            public static string Serialize(object obj)
            {
                var instance = new Serializer();
                instance.SerializeValue(obj);
                return instance._builder.ToString();
            }

            void SerializeValue(object value)
            {
                if (value == null) { _builder.Append("null"); return; }

                if (value is string s) { SerializeString(s); return; }
                if (value is bool b) { _builder.Append(b ? "true" : "false"); return; }

                if (value is IDictionary dict) { SerializeObject(dict); return; }
                if (value is IList list) { SerializeArray(list); return; }

                if (value is char c) { SerializeString(c.ToString()); return; }

                SerializeOther(value);
            }

            void SerializeObject(IDictionary obj)
            {
                bool first = true;
                _builder.Append('{');
                foreach (object e in obj.Keys)
                {
                    if (!first) _builder.Append(',');
                    SerializeString(e.ToString());
                    _builder.Append(':');
                    SerializeValue(obj[e]);
                    first = false;
                }
                _builder.Append('}');
            }

            void SerializeArray(IList array)
            {
                _builder.Append('[');
                bool first = true;
                foreach (object obj in array)
                {
                    if (!first) _builder.Append(',');
                    SerializeValue(obj);
                    first = false;
                }
                _builder.Append(']');
            }

            void SerializeString(string str)
            {
                _builder.Append('\"');
                foreach (var c in str)
                {
                    switch (c)
                    {
                        case '"': _builder.Append("\\\""); break;
                        case '\\': _builder.Append("\\\\"); break;
                        case '\b': _builder.Append("\\b"); break;
                        case '\f': _builder.Append("\\f"); break;
                        case '\n': _builder.Append("\\n"); break;
                        case '\r': _builder.Append("\\r"); break;
                        case '\t': _builder.Append("\\t"); break;
                        default:
                            int codepoint = Convert.ToInt32(c);
                            if (codepoint >= 32 && codepoint <= 126)
                                _builder.Append(c);
                            else
                                _builder.Append("\\u").Append(codepoint.ToString("x4"));
                            break;
                    }
                }
                _builder.Append('\"');
            }

            void SerializeOther(object value)
            {
                if (value is float f)
                    _builder.Append(f.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                else if (value is double d)
                    _builder.Append(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                else if (value is int || value is uint || value is long || value is sbyte
                    || value is byte || value is short || value is ushort || value is ulong)
                    _builder.Append(value);
                else if (value is decimal dec)
                    _builder.Append(dec.ToString(System.Globalization.CultureInfo.InvariantCulture));
                else
                    SerializeString(value.ToString());
            }
        }
    }
}
