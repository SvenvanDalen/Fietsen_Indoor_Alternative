using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FietsApp.Accounts
{
    /// <summary>
    /// Kleine JSON-lezer voor de antwoorden van GitHub (story 7.1/7.2). <c>JsonUtility</c> kan geen sleutels met een punt
    /// aan (zoals de bestandsnaam in een Gist) en geen losse lijsten, vandaar deze. Geeft objecten als
    /// <see cref="Dictionary{TKey,TValue}"/>, lijsten als <see cref="List{T}"/>, getallen als double.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            var value = Value(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException($"Onverwacht teken op positie {i}.");
            return value;
        }

        /// <summary>Tekst als JSON-string, met aanhalingstekens.</summary>
        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Veld van een object, of null als het er niet is (of het geen object is).</summary>
        public static object Get(object obj, string key) =>
            obj is Dictionary<string, object> d && d.TryGetValue(key, out var v) ? v : null;

        public static string GetString(object obj, string key) => Get(obj, key) as string;

        public static double? GetNumber(object obj, string key) => Get(obj, key) is double d ? d : (double?)null;

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON houdt te vroeg op.");
            char c = s[i];
            if (c == '{') return Object(s, ref i);
            if (c == '[') return Array(s, ref i);
            if (c == '"') return String(s, ref i);
            if (Word(s, ref i, "true")) return true;
            if (Word(s, ref i, "false")) return false;
            if (Word(s, ref i, "null")) return null;
            return Number(s, ref i);
        }

        static Dictionary<string, object> Object(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; // {
            Skip(s, ref i);
            if (Peek(s, i) == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                if (Peek(s, i) != '"') throw new FormatException($"Sleutel verwacht op positie {i}.");
                string key = String(s, ref i);
                Skip(s, ref i);
                Expect(s, ref i, ':');
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (Peek(s, i) == ',') { i++; continue; }
                Expect(s, ref i, '}');
                return d;
            }
        }

        static List<object> Array(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            Skip(s, ref i);
            if (Peek(s, i) == ']') { i++; return list; }
            while (true)
            {
                list.Add(Value(s, ref i));
                Skip(s, ref i);
                if (Peek(s, i) == ',') { i++; continue; }
                Expect(s, ref i, ']');
                return list;
            }
        }

        static string String(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("Halve \\u-code.");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break; // \" \\ \/
                }
            }
            throw new FormatException("String zonder eind.");
        }

        static double Number(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException($"Onverwacht teken '{s[i]}' op positie {i}.");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static bool Word(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

        static void Expect(string s, ref int i, char c)
        {
            if (Peek(s, i) != c) throw new FormatException($"'{c}' verwacht op positie {i}.");
            i++;
        }
    }
}
