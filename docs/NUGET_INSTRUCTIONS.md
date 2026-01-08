# LibPhext NuGet Package Instructions

## Sprint 4, Day 741 - Publishing libphext-cs to NuGet

### Prerequisites

1. **.NET 8 SDK** installed
2. **NuGet.org account** at https://www.nuget.org/
3. **API key** from NuGet.org (Account Settings → API Keys)

---

### Step 1: Build the Package

```powershell
cd libphext-cs/src/Phext

# Clean and build in Release mode
dotnet clean
dotnet build -c Release

# The package will be generated automatically in:
# bin/Release/LibPhext.0.3.0.nupkg
# bin/Release/LibPhext.0.3.0.snupkg (symbols)
```

### Step 2: Verify the Package

```powershell
# List package contents
dotnet nuget locals all --list

# Or use NuGet Package Explorer (GUI tool)
# Download: https://github.com/NuGetPackageExplorer/NuGetPackageExplorer
```

### Step 3: Create NuGet API Key

1. Go to https://www.nuget.org/account/apikeys
2. Click **Create**
3. Set:
   - **Key Name**: `LibPhext-Push`
   - **Expiration**: 365 days
   - **Glob Pattern**: `LibPhext*`
   - **Scopes**: Push new packages and package versions
4. Click **Create** and **copy the key immediately** (shown only once)

### Step 4: Push to NuGet.org

```powershell
# Set your API key (replace YOUR_API_KEY)
dotnet nuget push bin/Release/LibPhext.0.3.0.nupkg \
    --api-key YOUR_API_KEY \
    --source https://api.nuget.org/v3/index.json

# Push symbols package for debugging support
dotnet nuget push bin/Release/LibPhext.0.3.0.snupkg \
    --api-key YOUR_API_KEY \
    --source https://api.nuget.org/v3/index.json
```

### Step 5: Verify Publication

- Package will appear at: https://www.nuget.org/packages/LibPhext/
- Takes ~15-30 minutes for indexing
- Once indexed, can be installed via:

```powershell
dotnet add package LibPhext --version 0.3.0
```

---

### Local Development (Alternative to NuGet.org)

For faster iteration without publishing to NuGet.org:

```powershell
# Create local NuGet feed
mkdir C:\LocalNuGet

# Copy package to local feed
copy bin/Release/LibPhext.0.3.0.nupkg C:\LocalNuGet\

# Add local source to NuGet.config (already done in bruce project)
# See bruce/NuGet.config

# Install from local source
dotnet add package LibPhext --version 0.3.0 --source C:\LocalNuGet
```

---

### Version Bumping

For future releases, update `Version` in `Phext.csproj`:

```xml
<Version>0.4.0</Version>
```

Follow semver:
- **MAJOR** (1.0.0): Breaking API changes
- **MINOR** (0.4.0): New features, backward compatible
- **PATCH** (0.3.1): Bug fixes, backward compatible

---

### Package Metadata Checklist

The .csproj is configured with:

- [x] PackageId: `LibPhext`
- [x] Version: `0.3.0`
- [x] Authors: `Will Bickford`
- [x] Description: Full description with key features
- [x] License: MIT
- [x] Repository URL: GitHub link
- [x] Tags: discoverable keywords
- [x] README: Included in package
- [x] Symbols: `.snupkg` for debugging
- [x] Source Link: For navigating to source during debugging

---

### Updating Bruce to Use NuGet Package

Once published, update `Bruce.Core.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="LibPhext" Version="0.3.0" />
</ItemGroup>
```

Remove the project reference:

```xml
<!-- Remove this -->
<ProjectReference Include="..\..\..\libphext-cs\src\Phext\Phext.csproj" />
```

---

### Troubleshooting

**"Package already exists"**
- Bump the version number; NuGet doesn't allow overwriting

**"API key invalid"**
- Regenerate key at nuget.org
- Ensure glob pattern matches package ID

**"Package validation failed"**
- Check README.md exists at specified path
- Ensure all required metadata is present

**Build warnings about missing XML docs**
- Add `<NoWarn>CS1591</NoWarn>` to PropertyGroup, or
- Add XML comments to public members

---

## Quick Reference

```powershell
# Full publish workflow
cd libphext-cs/src/Phext
dotnet clean
dotnet build -c Release
dotnet nuget push bin/Release/LibPhext.0.3.0.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```
