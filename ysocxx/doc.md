# YumStudioObject (YSO) — Documentation for the C++ API

YSO is a small C++ header-only utility to read/write simple named sections containing key:value pairs. Sections look like:

[SectionName]
key1: value1
key2: value2

Multi-line values are wrapped with three double-quotes (""" ... """) and may contain newlines.

## Main classes

- YumStudio::YSOSection
  - operator[](const std::string& key) -> std::string& : get or create a key in the section.
  - operator[](const std::string& key) const -> std::string : read-only access (throws std::out_of_range if missing).
  - contains(key) -> bool
  - get_keys() -> reference or copy of internal map
  - to_string() -> std::string : returns serialized section (used by save)

- YumStudio::YumStudioObject
  - operator[](const std::string& section) -> YSOSection& : get or create a section.
  - operator[](const std::string& section) const -> YSOSection : read-only access (throws if missing).
  - contains(section) -> bool
  - save(path, header="") : write object to file. A newline is written after header.
  - static parse(std::istream&) -> YumStudioObject : parse from stream.

Exceptions:
- Parsing and missing-file operations throw std::runtime_error or std::out_of_range.

Notes:
- The header is a single string written at the top of the file followed by a newline.
- Multi-line values must use the sentinel """ before and after the content.
- The implementation has minimal validation; keep key and section names simple (no ':' in names).

## Small example

Save this as example.cpp and compile with a C++17 compiler:

```cpp
#include <fstream>
#include <iostream>
#include <sstream>
#include "ysocxx.hpp" // adjust include path as needed

using namespace YumStudio;

int main() {
  YumStudioObject o;
  // create section and keys
  o["App"]["name"] = "YSObjectDemo";
  o["App"]["version"] = "0.1";

  // multi-line value
  o["Notes"]["long"] = "Line one\nLine two\nLine three";

  // save to file
  o.save("demo.yso", "# Demo YSO file");

  // read back
  std::ifstream in("demo.yso");
  if (!in) {
    std::cerr << "Failed to open demo.yso\n";
    return 1;
  }
  YumStudioObject parsed = YumStudioObject::parse(in);
  std::cout << "App.name = " << parsed["App"]["name"] << "\n";
  std::cout << "Notes.long = " << parsed["Notes"]["long"] << "\n";
  return 0;
}
```

Compile on macOS:
```bash
g++ -std=c++17 example.cpp -o example
./example
```

If you want, I can add a short unit-test or fix small parsing issues (empty-line checks and to_string missing return).<!-- filepath: /Users/wys/Documents/Yum Studio/YSObject/ysocxx/doc.md -->

# YumStudioObject (YSO) — Documentation

YSO is a small C++ header-only utility to read/write simple named sections containing key:value pairs. Sections look like:

[SectionName]
key1: value1
key2: value2

Multi-line values are wrapped with three double-quotes (""" ... """) and may contain newlines.

## Main classes

- YumStudio::YSOSection
  - operator[](const std::string& key) -> std::string& : get or create a key in the section.
  - operator[](const std::string& key) const -> std::string : read-only access (throws std::out_of_range if missing).
  - contains(key) -> bool
  - get_keys() -> reference or copy of internal map
  - to_string() -> std::string : returns serialized section (used by save)

- YumStudio::YumStudioObject
  - operator[](const std::string& section) -> YSOSection& : get or create a section.
  - operator[](const std::string& section) const -> YSOSection : read-only access (throws if missing).
  - contains(section) -> bool
  - save(path, header="") : write object to file. A newline is written after header.
  - static parse(std::istream&) -> YumStudioObject : parse from stream.

Exceptions:
- Parsing and missing-file operations throw std::runtime_error or std::out_of_range.

Notes:
- The header is a single string written at the top of the file followed by a newline.
- Multi-line values must use the sentinel """ before and after the content.
- The implementation has minimal validation; keep key and section names simple (no ':' in names).

## Small example

Save this as example.cpp and compile with a C++17 compiler:

```cpp
#include <fstream>
#include <iostream>
#include <sstream>
#include "ysocxx.hpp" // adjust include path as needed

using namespace YumStudio;

int main() {
  YumStudioObject o;
  // create section and keys
  o["App"]["name"] = "YSObjectDemo";
  o["App"]["version"] = "0.1";

  // multi-line value
  o["Notes"]["long"] = "Line one\nLine two\nLine three";

  // save to file
  o.save("demo.yso", "# Demo YSO file");

  // read back
  std::ifstream in("demo.yso");
  if (!in) {
    std::cerr << "Failed to open demo.yso\n";
    return 1;
  }
  
  YumStudioObject parsed = YumStudioObject::parse(in);
  std::cout << "App.name = " << parsed["App"]["name"] << "\n";
  std::cout << "Notes.long = " << parsed["Notes"]["long"] << "\n";
  return 0;
}
```