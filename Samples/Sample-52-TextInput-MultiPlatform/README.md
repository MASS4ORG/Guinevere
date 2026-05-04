# Sample-52-TextInput-MultiPlatform

This sample serves as a cross-platform and cross-backend test for text input functionality in Guinevere. It allows running the same UI code using different graphics integrations.

## Supported Integrations
- **OpenTK** (`opentk`)
- **SilkNET OpenGL** (`silknet-opengl` or `silknet`)
- **SilkNET Vulkan** (`silknet-vulkan` or `vulkan`)
- **Raylib** (`raylib`)

## Usage
Run the sample with the desired integration as an argument:
```bash
dotnet run opentk
dotnet run silknet-opengl
dotnet run silknet-vulkan
dotnet run raylib
```

## Features Tested
- `TextInput`: Single-line text processing and cursor management.
- `PasswordInput`: Character masking and secure entry.
- `TextArea`: Multi-line text handling, newline support, and internal scrolling.
- Focus management across different windowing backends.
- Special character support (Unicode, emojis, accented characters).
- Keyboard shortcuts (Arrows, Home, End, Backspace, Delete).
