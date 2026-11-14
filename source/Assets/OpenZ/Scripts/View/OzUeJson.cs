using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): a small strict JSON reader and writer for the setup file of the
    /// SAVE / LOAD / SHARE buttons (DESIGN_SPEC section 10). JsonUtility cannot read objects keyed by unit id, so the file goes
    /// through plain trees: Dictionary&lt;string, object&gt; (key order kept as written), List&lt;object&gt;, double, string, bool and
    /// null. Pure C#, no engine types: the kit tool tools/p4_json_selftest.py compiles this very file and runs its round trip.
    /// Rules: RFC 8259 JSON only (no comments, no trailing commas, no NaN), at most 64 levels deep, a repeated key keeps its last
    /// value; the writer gives whole numbers without a fraction, other numbers in their shortest round-trip form, NaN and
    /// infinities as null, text as UTF-16 with only the required escapes (quotes, backslash, control characters).
    /// </summary>
    public static class OzUeJson
    {
        public const int MaxDepth = 64;

        /// <summary>The tree of a JSON text, or null when the text is not JSON (TryParse says why). A JSON "null" is null too.</summary>
        public static object Parse(string text) => TryParse(text, out var tree, out _) ? tree : null;

        /// <summary>Parses a JSON text; false with the reason (and the character position) when it is not valid JSON.</summary>
        public static bool TryParse(string text, out object tree, out string error)
        {
            tree = null; error = null;
            if (text == null) { error = "no text"; return false; }
            var r = new Reader(text);
            try
            {
                r.SkipBom();
                r.Ws();
                tree = r.Value(0);
                r.Ws();
                if (!r.End) throw r.Fail("text after the end of the JSON value");
                return true;
            }
            catch (FormatException e) { tree = null; error = e.Message; return false; }
        }

        /// <summary>The JSON text of a tree; pretty = two-space indents and one member per line (as the prototype's file).</summary>
        public static string Write(object tree, bool pretty)
        {
            var sb = new StringBuilder(4096);
            WriteValue(sb, tree, pretty, 0);
            if (pretty) sb.Append('\n');
            return sb.ToString();
        }

        // ------------------------------------------------------------------ reader
        sealed class Reader
        {
            readonly string s; int i;
            public Reader(string text) { s = text; }
            public bool End => i >= s.Length;
            public FormatException Fail(string what) => new FormatException(what + " at character " + i);
            public void SkipBom() { if (i < s.Length && s[i] == (char)0xFEFF) i++; }
            public void Ws() { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++; }

            public object Value(int depth)
            {
                if (depth > MaxDepth) throw Fail("nested deeper than " + MaxDepth + " levels");
                if (End) throw Fail("the text ended where a value was expected");
                char c = s[i];
                switch (c)
                {
                    case '{': return Obj(depth);
                    case '[': return Arr(depth);
                    case '"': return Str();
                    case 't': Word("true"); return true;
                    case 'f': Word("false"); return false;
                    case 'n': Word("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return Num();
                        throw Fail("unexpected '" + c + "'");
                }
            }

            void Word(string w)
            {
                if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) throw Fail("unexpected word");
                i += w.Length;
            }

            Dictionary<string, object> Obj(int depth)
            {
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                i++; Ws();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws();
                    if (End || s[i] != '"') throw Fail("a member name in quotes was expected");
                    string k = Str();
                    Ws();
                    if (End || s[i] != ':') throw Fail("':' was expected after a member name");
                    i++; Ws();
                    object v = Value(depth + 1);
                    d[k] = v;                                                                                // a repeated key keeps its last value (in its first place)
                    Ws();
                    if (End) throw Fail("the text ended inside an object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw Fail("',' or '}' was expected");
                }
            }

            List<object> Arr(int depth)
            {
                var l = new List<object>();
                i++; Ws();
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    Ws();
                    l.Add(Value(depth + 1));
                    Ws();
                    if (End) throw Fail("the text ended inside a list");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw Fail("',' or ']' was expected");
                }
            }

            string Str()
            {
                i++;                                                                                        // the opening quote
                StringBuilder b = null; int run = i;
                while (true)
                {
                    if (End) throw Fail("the text ended inside a string");
                    char c = s[i];
                    if (c == '"')
                    {
                        string tail = s.Substring(run, i - run); i++;
                        return b == null ? tail : b.Append(tail).ToString();
                    }
                    if (c < ' ') throw Fail("a control character inside a string");
                    if (c != '\\') { i++; continue; }
                    b ??= new StringBuilder();
                    b.Append(s, run, i - run);
                    i++;
                    if (End) throw Fail("the text ended inside an escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': b.Append('"'); break;
                        case '\\': b.Append('\\'); break;
                        case '/': b.Append('/'); break;
                        case 'b': b.Append('\b'); break;
                        case 'f': b.Append('\f'); break;
                        case 'n': b.Append('\n'); break;
                        case 'r': b.Append('\r'); break;
                        case 't': b.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Fail("a short \\u escape");
                            int code = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                char h = s[i++]; int v = h >= '0' && h <= '9' ? h - '0' : h >= 'a' && h <= 'f' ? h - 'a' + 10 : h >= 'A' && h <= 'F' ? h - 'A' + 10 : -1;
                                if (v < 0) throw Fail("a bad \\u escape");
                                code = code * 16 + v;
                            }
                            b.Append((char)code);                                                            // surrogate pairs arrive as two escapes, as UTF-16 keeps them
                            break;
                        default: throw Fail("an unknown escape \\" + e);
                    }
                    run = i;
                }
            }

            double Num()
            {
                int a = i;
                if (s[i] == '-') i++;
                if (End) throw Fail("a number without digits");
                if (s[i] == '0') i++;
                else if (s[i] >= '1' && s[i] <= '9') { while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; }
                else throw Fail("a number without digits");
                if (i < s.Length && s[i] == '.')
                {
                    i++; int f = i;
                    while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                    if (i == f) throw Fail("a number without digits after the point");
                }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++;
                    if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    int f = i;
                    while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                    if (i == f) throw Fail("a number without digits in its exponent");
                }
                if (!double.TryParse(s.Substring(a, i - a), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsInfinity(v) || double.IsNaN(v))
                    throw Fail("a number out of range");
                return v;
            }
        }

        // ------------------------------------------------------------------ writer
        static void WriteValue(StringBuilder sb, object v, bool pretty, int depth)
        {
            if (depth > MaxDepth) { sb.Append("null"); return; }
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string str: WriteString(sb, str); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: WriteNumber(sb, d); return;
                case float f: WriteNumber(sb, f); return;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case short h: sb.Append(h.ToString(CultureInfo.InvariantCulture)); return;
                case byte y: sb.Append(y.ToString(CultureInfo.InvariantCulture)); return;
                case uint u: sb.Append(u.ToString(CultureInfo.InvariantCulture)); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case IDictionary<string, object> map: WriteObject(sb, map, pretty, depth); return;
                case IDictionary raw:
                    var copy = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (DictionaryEntry e in raw) copy[Convert.ToString(e.Key, CultureInfo.InvariantCulture) ?? ""] = e.Value;
                    WriteObject(sb, copy, pretty, depth); return;
                case IEnumerable list: WriteList(sb, list, pretty, depth); return;
                default: WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""); return;
            }
        }

        static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            if (Math.Abs(d) < 9007199254740992.0 && Math.Floor(d) == d)
            {
                if (d == 0) { sb.Append('0'); return; }                                                    // -0 too
                sb.Append(((long)d).ToString(CultureInfo.InvariantCulture)); return;
            }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            int run = 0;
            for (int k = 0; k < s.Length; k++)
            {
                char c = s[k]; string esc = null;
                if (c == '"') esc = "\\\""; else if (c == '\\') esc = "\\\\";
                else if (c == '\n') esc = "\\n"; else if (c == '\r') esc = "\\r"; else if (c == '\t') esc = "\\t";
                else if (c == '\b') esc = "\\b"; else if (c == '\f') esc = "\\f";
                else if (c < ' ' || c == (char)0x2028 || c == (char)0x2029) esc = "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture);
                if (esc == null) continue;
                sb.Append(s, run, k - run).Append(esc); run = k + 1;
            }
            sb.Append(s, run, s.Length - run);
            sb.Append('"');
        }

        static void Indent(StringBuilder sb, int depth) { sb.Append('\n'); sb.Append(' ', depth * 2); }

        static void WriteObject(StringBuilder sb, IDictionary<string, object> map, bool pretty, int depth)
        {
            if (map.Count == 0) { sb.Append("{}"); return; }
            sb.Append('{'); bool first = true;
            foreach (var kv in map)
            {
                if (!first) sb.Append(',');
                first = false;
                if (pretty) Indent(sb, depth + 1);
                WriteString(sb, kv.Key ?? "");
                sb.Append(pretty ? ": " : ":");
                WriteValue(sb, kv.Value, pretty, depth + 1);
            }
            if (pretty) Indent(sb, depth);
            sb.Append('}');
        }

        static void WriteList(StringBuilder sb, IEnumerable list, bool pretty, int depth)
        {
            bool first = true; bool any = false;
            foreach (var x in list)
            {
                if (!any) { sb.Append('['); any = true; }
                if (!first) sb.Append(',');
                first = false;
                if (pretty) Indent(sb, depth + 1);
                WriteValue(sb, x, pretty, depth + 1);
            }
            if (!any) { sb.Append("[]"); return; }
            if (pretty) Indent(sb, depth);
            sb.Append(']');
        }

        // ------------------------------------------------------------------ readers for the loader (plain trees only)
        /// <summary>The member of an object, or null.</summary>
        public static object Get(object tree, string key) => tree is Dictionary<string, object> d && key != null && d.TryGetValue(key, out var v) ? v : null;
        /// <summary>A finite number member, else the fallback.</summary>
        public static double Num(object tree, string key, double fallback) => Get(tree, key) is double v && !double.IsNaN(v) && !double.IsInfinity(v) ? v : fallback;
        /// <summary>A string member, else the fallback.</summary>
        public static string Str(object tree, string key, string fallback) => Get(tree, key) is string v ? v : fallback;
    }
}
