/// YumStudioObject
/// 
/// Custom Object Notation Format, provided by YumStudio.
/// This is free and open-source, but please, credit us (At YumStudio, https://github.com/YumStudioHQ).
/// This code is provided "as is", so you're resonsible of everything can happen by it.
/// Thank you using our format!
/// 
/// Author: Wys (https://github.com/wys-prog)

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace YumStudio
{
  /// <summary>
  /// YSObject represents a collection of named scopes (sections). Each scope contains key/value pairs.
  /// Features:
  /// - Robust parsing from file/string, including multi-line values with triple-quoted syntax ("""...""")
  /// - Supports comments with ';' or '#' (at line start or after content)
  /// - Merges/adds keys via AddKeys
  /// - Exposes safe TryParse and Parse methods
  /// - Produces serialized output via ToString() and Save(path)
  /// - Better error messages and input validation
  /// </summary>
  public class YSObject
  {
    // Use Dictionary for scopes. The global scope holds keys outside any [section].
    public const string GlobalScopeName = "__global";

    // Sections -> (key -> value)
    public Dictionary<string, Dictionary<string, string>> Keys { get; set; }

    public YSObject()
    {
      Keys = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
      {
        [GlobalScopeName] = new Dictionary<string, string>(StringComparer.Ordinal)
      };
    }

    // Indexer for accessing a scope by name (e.g., obj["section"])
    public Dictionary<string, string> this[string scope]
    {
      get
      {
        if (Keys.TryGetValue(scope, out var dict))
          return dict;
        throw new KeyNotFoundException($"Scope '{scope}' not found.");
      }
      set
      {
        Keys[scope] = value;
      }
    }

    /// <summary>
    /// Merge the provided scopes into this object. Existing keys will be overwritten by the incoming ones.
    /// </summary>
    public void AddKeys(Dictionary<string, Dictionary<string, string>> @keys)
    {
      if (@keys == null) return;
      foreach (var scope in @keys)
      {
        if (!Keys.ContainsKey(scope.Key)) Keys[scope.Key] = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in scope.Value)
        {
          Keys[scope.Key][kv.Key] = kv.Value;
        }
      }
    }

    /// <summary>
    /// Returns if given parameter key exists as scope
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool HasScope(string key)
    {
      return Keys.ContainsKey(key);
    }

    /// <summary>
    /// Save to file (UTF-8) using the serializer (ToString()). Overwrites existing file.
    /// </summary>
    public void Save(string path, string header = "")
    {
      File.WriteAllText(path, $"{(header.Trim() == "" ? "" : $"{header}\n")}{ToString().Trim()}", Encoding.UTF8);
    }

    /// <summary>
    /// Serialize back to YSN text. Multi-line values will be written as triple-quoted blocks.
    /// Keys in the global scope are written before other sections.
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      // Global first (if it has any keys)
      if (Keys.ContainsKey(GlobalScopeName) && Keys[GlobalScopeName].Count > 0)
      {
        foreach (var kv in Keys[GlobalScopeName])
        {
          sb.AppendLine($"{kv.Key}: {SerializeValue(kv.Value)}");
        }
        sb.AppendLine();
      }

      foreach (var scope in Keys)
      {
        if (scope.Key == GlobalScopeName) continue;
        sb.AppendLine($"[{scope.Key}]");
        foreach (var kv in scope.Value)
        {
          sb.AppendLine($"{kv.Key}: {SerializeValue(kv.Value)}");
        }
        sb.AppendLine();
      }

      return sb.ToString();
    }

    private static string SerializeValue(string value)
    {
      if (value == null) return string.Empty;
      if (value.Contains('\n'))
      {
        // Use triple-quoted block for multi-line to preserve newlines
        // Escape the closing triple quotes if present inside the value
        var safe = value.Replace("\"\"\"", "\\\"\"\"");
        return $"\"\"\"\n{safe}\n\"\"\"";
      }
      // For single-line values, if it contains leading/trailing spaces or a comment char, quote it
      if (Regex.IsMatch(value, "^\\s|\\s$|[:;#]"))
        return '"' + value.Replace("\"", "\\\"") + '"';
      return value;
    }

    public class YSObjectParser
    {
      private readonly Dictionary<string, Dictionary<string, string>> Keys = new(StringComparer.Ordinal);
      private string currentLabel = GlobalScopeName;

      private static readonly Regex SectionRegex = new("^\\s*\\[([^\n]]+)\\]\\s*(?:;.*)?$", RegexOptions.Compiled);
      private static readonly Regex KeyValRegex = new("^\\s*([^:\\s][^:]*)\\s*:\\s*(.*)$", RegexOptions.Compiled);

      public YSObjectParser()
      {
        Keys[GlobalScopeName] = new Dictionary<string, string>(StringComparer.Ordinal);
      }

      /// <summary>
      /// Parse a single physical line. This does NOT handle multi-line triple-quoted values (that is handled in FromString/FromFile loop).
      /// Returns true if a key/value or section was processed; false if the line was blank/comment.
      /// </summary>
      private bool ParseLine(string rawLine)
      {
        if (rawLine == null) return false;
        var line = rawLine.TrimEnd(); // keep leading spaces for quoted values
        if (line.Length == 0) return false;

        // Remove inline comments starting with ';' or '#' but only when they are preceded by whitespace or start of line
        // We must be careful not to strip comment characters inside quoted strings. We rely on the higher-level multi-line handling to provide already unescaped content.

        // If line starts with comment char, ignore
        var trimmedStart = line.TrimStart();
        if (trimmedStart.StartsWith(';') || trimmedStart.StartsWith('#')) return false;

        // Section header
        var secMatch = SectionRegex.Match(line);
        if (secMatch.Success)
        {
          currentLabel = secMatch.Groups[1].Value.Trim();
          if (!Keys.ContainsKey(currentLabel)) Keys[currentLabel] = new Dictionary<string, string>(StringComparer.Ordinal);
          return true;
        }

        // Key: value
        var m = KeyValRegex.Match(line);
        if (m.Success)
        {
          var k = m.Groups[1].Value.Trim();
          var rest = m.Groups[2].Value;

          // If rest starts with triple-quote, the caller should have handled multi-line block; here we'll handle single-line triple-quoted too.
          string value = UnescapeInlineValue(rest);

          if (!Keys.ContainsKey(currentLabel)) Keys[currentLabel] = new Dictionary<string, string>(StringComparer.Ordinal);
          Keys[currentLabel][k] = value;
          return true;
        }

        // Otherwise it's an orphan value or invalid line; we ignore silently
        return false;
      }

      private static string UnescapeInlineValue(string raw)
      {
        var s = raw.TrimStart();
        // Single-line triple-quoted: """value"""
        if (s.StartsWith("\"\"\"") && s.Length >= 6 && s.EndsWith("\"\"\""))
        {
          var inner = s.Substring(3, s.Length - 6);
          return inner.Replace("\\\"\"\"", "\"\"\"");
        }
        // Quoted single-line
        if (s.StartsWith('"') && s.Length >= 2 && s.EndsWith('"'))
        {
          var inner = s.Substring(1, s.Length - 2);
          return inner.Replace("\\\"", "\"");
        }

        // Remove trailing inline comment started by ' ;' or ' #' (space + comment char)
        // We look for ' ;' or ' #' that is preceded by whitespace to avoid removing hashes in URLs (best-effort)
        var commentPos = -1;
        for (int i = 1; i < s.Length - 1; i++)
        {
          if ((s[i] == ';' || s[i] == '#') && char.IsWhiteSpace(s[i - 1])) { commentPos = i; break; }
        }
        if (commentPos >= 0) s = s.Substring(0, commentPos).TrimEnd();

        return Regex.Unescape(s);
      }

      /// <summary>
      /// Parse from an enumerable of lines. Handles triple-quoted multi-line values.
      /// </summary>
      public void ParseLines(IEnumerable<string> lines)
      {
        using var e = lines.GetEnumerator();
        while (e.MoveNext())
        {
          var line = e.Current ?? string.Empty;

          // Detect key: """ start (single-line or start of block)
          var kvMatch = KeyValRegex.Match(line);
          if (kvMatch.Success)
          {
            var key = kvMatch.Groups[1].Value.Trim();
            var rest = kvMatch.Groups[2].Value.TrimStart();

            if (rest.StartsWith("\"\"\""))
            {
              // Multi-line block
              var valueBuilder = new StringBuilder();
              // If the rest contains closing triple quotes on the same line, handle single-line triple-quoted value
              if (rest.Length >= 6 && rest.EndsWith("\"\"\""))
              {
                var inner = rest.Substring(3, rest.Length - 6);
                valueBuilder.Append(inner.Replace("\\\"\"\"", "\"\"\""));
              }
              else
              {
                // Remove the opening triple quotes and read subsequent lines until a line containing closing triple quotes
                var afterOpen = rest.Substring(3);
                if (afterOpen.Length > 0) valueBuilder.AppendLine(afterOpen);

                bool closed = false;
                while (e.MoveNext())
                {
                  var next = e.Current ?? string.Empty;
                  var idx = next.IndexOf("\"\"\"");
                  if (idx >= 0)
                  {
                    // Append up to the closing
                    if (idx > 0)
                    {
                      valueBuilder.Append(next.AsSpan(0, idx));
                    }
                    closed = true;
                    break;
                  }
                  valueBuilder.AppendLine(next);
                }

                if (!closed)
                  throw new FormatException("Unterminated triple-quoted value (missing \"\"\").");
              }

              var value = valueBuilder.ToString();
              if (!Keys.ContainsKey(currentLabel)) Keys[currentLabel] = new Dictionary<string, string>(StringComparer.Ordinal);
              Keys[currentLabel][key] = value;
              continue; // processed this logical entry
            }
          }

          // Fallback to line parser for normal lines
          ParseLine(line);
        }
      }

      public YSObject FromFile(string path)
      {
        if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);

        var allLines = File.ReadAllLines(path, Encoding.UTF8);
        ParseLines(allLines);

        var obj = new YSObject
        {
          Keys = Keys
        };
        return obj;
      }

      public YSObject FromString(string s)
      {
        var parts = s.Replace("\r\n", "\n").Split('\n');
        ParseLines(parts);
        var obj = new YSObject
        {
          Keys = Keys
        };
        return obj;
      }

      public static YSObject Parse(string source, bool isFile = true)
      {
        var p = new YSObjectParser();
        return isFile ? p.FromFile(source) : p.FromString(source);
      }
    }

    // Convenience static wrappers
    public static YSObject Parse(string source, bool isFile = true)
    {
      return YSObjectParser.Parse(source, isFile);
    }

    public static bool TryParse(string source, out YSObject obj, out Exception error, bool isFile = true)
    {
      try
      {
        obj = Parse(source, isFile);
        error = null;
        return true;
      }
      catch (Exception ex)
      {
        obj = new YSObject();
        error = ex;
        return false;
      }
    }

    public static T FromYSObject<T>(YSObject ys, string scope, bool sensitive = false) where T : new()
    {
      if (!ys.HasScope(scope))
        throw new DataException($"Missing scope \"{scope}\"");

      var section = ys[scope];
      var obj = new T();
      var type = typeof(T);

      foreach (var kv in section)
      {
        var prop = type.GetProperty(
          kv.Key,
          BindingFlags.Public | BindingFlags.Instance | (sensitive ? BindingFlags.IgnoreCase : 0)
        );

        if (prop == null || !prop.CanWrite) continue;

        object value = Convert.ChangeType(kv.Value, prop.PropertyType);
        prop.SetValue(obj, value);
      }

      return obj;
    }

    public static YSObject CreateTemplate(Type type, string section, bool standardize = false)
    {
      YSObject ys = new();
      ys[section] = [];

      var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
      foreach (var prop in props)
      {
        if (prop.CanWrite)
        {
          ys[section][standardize ? prop.Name.ToLower() : prop.Name] = "";
        }
      }

      return ys;
    }

    public static YSObject CreateTemplate<T>(string section, bool standardize = false)
        => CreateTemplate(typeof(T), section, standardize);
  }
}
