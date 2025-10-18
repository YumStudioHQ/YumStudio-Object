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
  public partial class YSObject
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

    public void Merge(YSObject[] objects)
    {
      foreach (var o in objects) AddKeys(o.Keys);
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
      if (Keys.TryGetValue(GlobalScopeName, out Dictionary<string, string> value) && value.Count > 0)
      {
        foreach (var kv in value)
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
        var safe = value.Replace("\"\"\"", "\\\"\\\"\\\"");
        return $"\"\"\"\n{safe}\n\"\"\"";
      }

      return value;
    }

    public class YSObjectParser
    {
      public static YSObject FromStream(TextReader reader)
      {
        var obj = new YSObject();
        var section = "__global";

        string line;
        while ((line = reader.ReadLine()) != null)
        {
          line = line.Trim();
          if (line.StartsWith(';') || line.StartsWith('#')) continue;
          else if (line.StartsWith('['))
          {
            if (!line.Contains(']')) throw new FormatException("expected ']'");
            var beg = line.IndexOf('[');
            var end = line.IndexOf(']', beg + 1);
            section = line[(beg + 1)..end];
            if (!obj.HasScope(section)) obj[section] = [];
          }
          else if (line.Contains(':'))
          {
            var col = line.IndexOf(':');
            var key = line[0..col];
            var val = line[(col + 1)..].TrimStart();

            if (val.StartsWith("\"\"\""))
            {
              val = val[3..];
              bool found = false;
              while ((line = reader.ReadLine()) != null && !found)
              {
                var endp = line.IndexOf("\"\"\"");
                if (endp != -1)
                {
                  val += line[0..endp];
                  found = true;
                }
                else val += line + "\n";
              }

              if (!found) throw new FormatException("expected '\"\"\"'");
              val = val[..(val.Length-1)];
            }
            else val = val.Trim();
            obj[scope: section][key: key] = val;
          }
        }

        return obj;
      }

      public static YSObject FromFile(string path)
      {
        using var reader = new StreamReader(path);
        return FromStream(reader);
      }

      public static YSObject FromString(string s)
      {
        return FromStream(new StringReader(s));
      }

      public static YSObject Parse(string source, bool isFile = true)
      {
        return isFile ? FromFile(source) : FromString(source);
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
