#pragma once

#include <cctype>
#include <string>
#include <fstream>
#include <iostream>
#include <algorithm>
#include <stdexcept>
#include <unordered_map>

#define YSO_MULLN_STR "\"\"\""

#include "maw/maw.hpp"

#ifdef MAW_SYSTEM_CXX
#define _y_YsoDeclAtr : public maw::object
#else
#define _y_YsoDeclAtr
#endif

namespace YumStudio {
  class YSOSection _y_YsoDeclAtr {
  private:
    std::unordered_map<std::string, std::string> map;

    inline static std::string pack(const std::string &str) {
      if (str.find('\n') != std::string::npos) {
        return YSO_MULLN_STR + str + YSO_MULLN_STR;
      }

      return str;
    }

  public:
    inline YSOSection() : map() {}
    inline YSOSection(const std::unordered_map<std::string, std::string> &_map)
      : map(_map) {}

    // Returns true if key is in the map.
    inline bool contains(const std::string &key) const {
      return map.find(key) != map.end();
    }

    // Returns a string reference if the key is found.
    inline std::string &operator[](const std::string &key) {
      return map[key];
    }

    // Returns the string at @key. Throws std::out_of_range if the key isn't found.
    inline std::string operator[](const std::string &key) const {
      if (contains(key)) {
        return map.at(key);
      }

      throw std::out_of_range("YumStudioObject: key not found '" + key + "'");
    }

    // Returns a reference to the internal map. Changing it will affect internal map.
    inline std::unordered_map<std::string, std::string> &get_keys() { return map; }
    inline std::unordered_map<std::string, std::string> get_keys() const { return map; }

    // returns a string of the current section
    inline std::string to_string() const {
      std::string s;
      for (const auto &[k, v]:map) s += k + ":" + v + "\n";
    }
  };

  class YumStudioObject _y_YsoDeclAtr {
  private:
    std::unordered_map<std::string, YSOSection> sections;

    inline static std::string trim(const std::string &s) {
      auto wsfront = std::find_if_not(s.begin(), s.end(), [](int c){ return std::isspace(c); });
      auto wsback = std::find_if_not(s.rbegin(), s.rend(), [](int c){ return std::isspace(c); }).base();
      return (wsback <= wsfront ? std::string() : std::string(wsfront, wsback));
    }

  public:
    inline YumStudioObject() : sections() {}
    inline YumStudioObject(const std::unordered_map<std::string, YSOSection> &map)
      : sections(map) {}

    // Returns true if specified section exists.
    inline bool contains(const std::string &section) const { return sections.find(section) != sections.end(); }

    // Returns a string reference if the section is found.
    inline YSOSection &operator[](const std::string &section) {
      return sections[section];
    }

    // Returns the string at @section. Throws std::out_of_range if the key isn't found.
    inline YSOSection operator[](const std::string &section) const {
      if (contains(section)) {
        return sections.at(section);
      }

      throw std::out_of_range("YumStudioObject: key not found '" + section + "'");
    }

    // Saves the current object to path. Note: a new line is added after @header
    inline void save(const std::string &path, const std::string &header = "") const {
      std::ofstream file(path);
      if (!file) throw std::runtime_error("File not found: " + path);

      file << header << "\n";

      for (const auto &section:sections) {
        file << "[" << section.first << "]\n" << section.second.to_string() << std::endl;
      }

      file.close();
    }

    inline static YSOSection parse_section(std::istream &src) {
      YSOSection section = {};

      std::string line;
      while (std::getline(src, line)) {
        line = trim(line);

        if (line.size() >= 0 && line[0] != '#' || line[0] != ';' && line.find(':') != std::string::npos) {
          size_t nameend = line.find(':');
          std::string name = line.substr(0, nameend);
          std::string value = line.substr(nameend + 1);
          if (value.find(YSO_MULLN_STR) != std::string::npos) {
            bool found = false;
            while (std::getline(src, line) && (!found)) {
              if (src.eof() && (!found)) throw std::runtime_error("expected '" YSO_MULLN_STR "'");
              if (line.find(YSO_MULLN_STR) != std::string::npos) {
                size_t end = line.find(YSO_MULLN_STR);
                value += line.substr(0, end);
                found = true;
              } else value += line;
            }
          } else {
            // Non multi-lined string.
            section[name] = trim(value);
          }
        }
      }

      return section;
    }

    inline static YumStudioObject parse(std::istream &src) {
      YumStudioObject object = {};
      
      std::string line;
      while (std::getline(src, line)) {
        line = trim(line);

        if (line.size() > 0 && line[0] != '#' && line[0] != ';' && line.find('[') != std::string::npos) {
          size_t beg = line.find('[');
          size_t end = line.find(']', beg);
          if (end == std::string::npos) throw std::runtime_error("YumStudioObject: Expected ']'");
          std::string sectionname = trim(line.substr(beg + 1, end - (beg + 1)));
          object[sectionname] = parse_section(src);
        }
      }

      return object;
    }

    #ifdef MAW_SYSTEM_CXX
    #pragma region Maw.System and Maw.Cxx reflection

    inline std::shared_ptr<maw::object> activator() const override {
      return std::make_shared<YumStudioObject>();
    }

    inline const maw::type_info &get_type() const override {
      using namespace maw;
      static maw::type_info info {
        m_type_name<YumStudioObject>(),
        [] { return std::make_shared<YumStudioObject>(); },
        {},
        &maw::object().get_type()
      };
      return info;
    }

    #pragma endregion
    #endif
  };
}