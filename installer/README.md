# DocHelper WiX v4 Installer Documentation

## Overview

This installer uses WiX Toolset v4 (modern version) to create a professional MSI installer for DocHelper. The installer follows best practices with explicit component definitions, proper GUID management, and clean upgrade paths.

## Directory Structure

```
installer/
├── DocHelper.Installer.wixproj    # MSBuild project file
├── Package.wxs                     # Main package definition
├── Components/
│   ├── Core.wxs                   # Core application files
│   ├── Dependencies.wxs           # Third-party dependencies
│   └── Registry.wxs               # Registry entries
├── Fragments/
│   ├── Shortcuts.wxs              # Desktop and Start Menu shortcuts
│   └── UI.wxs                     # UI customizations
└── Assets/
    ├── DocHelper.ico              # Application icon
    ├── License.rtf                # License agreement
    ├── Banner.bmp                 # Installer banner (493x58)
    └── Dialog.bmp                 # Installer dialog (493x312)
```

## Key Features

### WiX v4 Improvements

- **Modern XML Schema**: Uses `http://wixtoolset.org/schemas/v4/wxs`
- **Simplified Syntax**: Combined Package element (no separate Product)
- **StandardDirectory**: Built-in directory references
- **Better Validation**: No need for ICE suppressions

### Installation Details

- **Location**: `%LocalAppData%\DiaTech\DocHelper` (per-user, no admin required)
- **Upgrade Code**: `A5B6C7D8-E9F0-4123-8456-789ABCDEF012` (DO NOT CHANGE)
- **Version Management**: Dynamic from Git tags or build parameter

### Component Organization

#### Core Components (Core.wxs)

- Main executable (DocHelper.exe)
- Application DLL (DocHelper.dll)
- Configuration files (appsettings.json, schemas)
- WPF native dependencies
- Directory creation (logs, config)

#### Dependencies (Dependencies.wxs)

- Prism Framework
- DocumentFormat.OpenXml
- Velopack (auto-update)
- System dependencies
- Runtime libraries

#### Registry (Registry.wxs)

- Installation path
- Version information
- Auto-update configuration
- Application settings
- File associations (optional)

## Building the Installer

### Prerequisites

1. Install WiX v5 toolset:

```powershell
dotnet tool install --global wix --version 5.0.0
```

2. Build the application first:

```powershell
dotnet publish DocHelper/DocHelper.csproj --configuration Release --output ./publish --self-contained true --runtime win-x64
```

### Build MSI

```powershell
cd installer
dotnet build DocHelper.Installer.wixproj --configuration Release /p:ProductVersion=2.0.0
```

The MSI will be created at: `installer/bin/Release/DocHelper-Setup.msi`

## Version Management

### Setting Version

The installer version can be set in multiple ways (priority order):

1. **Build Parameter**: `/p:ProductVersion=2.1.0`
2. **Git Tag**: Automatically extracted in CI/CD from `v*` tags
3. **Default**: Falls back to version in .wixproj (2.0.0)

### Version Format

Use semantic versioning: `MAJOR.MINOR.PATCH`

- MSI only uses first 3 numbers (4th is ignored)
- Example: `2.1.0` ✓, `2.1.0.0` (4th number ignored)

## Adding/Removing Files

### Adding a New File

1. **Identify the component group**:

   - Core application → `Core.wxs`
   - Third-party DLL → `Dependencies.wxs`

2. **Add the component**:

```xml
<Component Id="NewFile" Directory="BinFolder">
  <File Source="$(var.PublishDir)\NewFile.dll" KeyPath="yes" />
</Component>
```

3. **Use explicit GUIDs only for registry components**:
   - Files: Let WiX auto-generate
   - Registry: Use explicit GUID

### Removing a File

Simply remove the component from the appropriate .wxs file.

## GUID Management

### Fixed GUIDs (DO NOT CHANGE)

- **Upgrade Code**: `A5B6C7D8-E9F0-4123-8456-789ABCDEF012`
- **Registry Components**: All have explicit GUIDs
- **Directory Creation**: Explicit GUIDs

### Auto-Generated GUIDs

- **File Components**: No GUID specified (WiX generates stable ones)

## Customization

### Change Installation Directory

Edit in `Package.wxs`:

```xml
<StandardDirectory Id="LocalAppDataFolder">
  <Directory Id="CompanyFolder" Name="DiaTech">
    <Directory Id="INSTALLFOLDER" Name="DocHelper">
```

### Add/Remove Shortcuts

Edit `Fragments/Shortcuts.wxs`:

- Desktop shortcut is optional (controlled by property)
- Start Menu shortcuts include uninstall option

### UI Customization

1. Replace images in `Assets/`:

   - `Banner.bmp`: 493x58 pixels
   - `Dialog.bmp`: 493x312 pixels

2. Update text in `Fragments/UI.wxs`

## Testing

### Local Testing

1. Build the MSI
2. Install: `msiexec /i DocHelper-Setup.msi`
3. Verify installation at `%LocalAppData%\DiaTech\DocHelper`
4. Test upgrade: Build new version and install over existing

### Upgrade Testing

1. Install version 2.0.0
2. Build version 2.1.0
3. Install 2.1.0 over 2.0.0
4. Verify: Old version removed, new version installed

### Uninstall Testing

- Via Control Panel: Add/Remove Programs
- Via command line: `msiexec /x DocHelper-Setup.msi`

## Troubleshooting

### Common Issues

#### "A newer version is already installed"

- Check version numbers
- Ensure new version is higher than installed

#### Missing files in MSI

- Verify files exist in `publish/` directory
- Check component definitions in .wxs files

#### Registry not cleaned on uninstall

- Ensure registry components have KeyPath="yes"
- Check GUID consistency

### Debug Commands

View MSI contents:

```powershell
msiexec /a DocHelper-Setup.msi /qb TARGETDIR=C:\Temp\Extract
```

Enable logging:

```powershell
msiexec /i DocHelper-Setup.msi /l*v install.log
```

## CI/CD Integration

The GitHub Actions workflow:

1. Extracts version from Git tag
2. Builds and publishes the application
3. Creates installer assets (icons, bitmaps)
4. Builds MSI with WiX v4
5. Creates portable ZIP
6. Generates checksums
7. Creates GitHub release with both packages

## Best Practices

### DO

- ✅ Keep Upgrade Code constant
- ✅ Use explicit GUIDs for registry entries
- ✅ Test upgrades before release
- ✅ Version using semantic versioning
- ✅ Document any custom components

### DON'T

- ❌ Change the Upgrade Code
- ❌ Use heat.exe for harvesting
- ❌ Mix per-user and per-machine installs
- ❌ Ignore ICE warnings (fix them properly)
- ❌ Hard-code paths

## Maintenance Checklist

Before each release:

- [ ] Update version number
- [ ] Add new files to appropriate component groups
- [ ] Remove obsolete components
- [ ] Test clean install
- [ ] Test upgrade from previous version
- [ ] Test uninstall
- [ ] Verify registry cleanup
- [ ] Update documentation for changes

## References

- [WiX v4 Documentation](https://wixtoolset.org/docs/fourthree/)
- [WiX v4 Migration Guide](https://wixtoolset.org/docs/fourthree/faqs/#migrating-from-wix-v3)
- [MSI Best Practices](https://docs.microsoft.com/en-us/windows/win32/msi/best-practices)
