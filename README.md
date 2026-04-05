# AFTERLiFE — AI-Powered Antivirus for Windows

A native Windows antivirus desktop application built with **WinUI 3 / .NET 8**, featuring a triple-engine malware detection pipeline (Signature + YARA + AI/ML), real-time file monitoring, and a premium frosted-glass cyberpunk UI.

---

## Table of Contents

- [Features](#features)
- [Architecture Overview](#architecture-overview)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Environment Setup](#environment-setup)
- [Database & Resource Acquisition](#database--resource-acquisition)
- [Building & Compiling](#building--compiling)
- [Running the Application](#running-the-application)
- [Project Structure](#project-structure)
- [Detection Engines](#detection-engines)
- [AI Model Pipeline](#ai-model-pipeline)
- [Configuration & Data Paths](#configuration--data-paths)
- [Logging](#logging)
- [Troubleshooting](#troubleshooting)

---

## Features

| Feature | Description |
|---|---|
| **Signature Engine** | LMDB-backed hash database with MD5, SHA-1, and SHA-256 lookups |
| **YARA Engine** | Native `libyara64.dll` integration for pattern-based rule matching |
| **AI Detection** | Ensemble of 6 LightGBM ONNX models (histogram + string features) for heuristic PE analysis |
| **Real-Time Monitoring** | `FileSystemWatcher`-based directory monitoring with debounced scanning |
| **On-Demand Scanning** | File, multi-file, and recursive directory scans with cancellation support |
| **Threat History** | Persistent JSON-backed threat log with confidence scores |
| **Quarantine** | Isolated quarantine folder for detected threats |
| **Smart False-Positive Filtering** | Trust heuristics for signed executables, known legitimate software, and installer patterns |
| **Frosted Glass UI** | WinUI 3 with animated neon orbs, glassmorphism sidebar, HLSL shader effects |
| **MVVM Architecture** | CommunityToolkit.Mvvm for clean data binding and commands |
| **Activity Logging** | Timestamped activity log for monitoring, scans, and threats |
| **Configurable Settings** | Toggle individual engines, set themes, manage whitelists and monitored directories |

---

## Architecture Overview

```
┌────────────────────────────────────────────────────────┐
│                     WinUI 3 UI Layer                   │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐ │
│  │Dashboard │ │ Threats  │ │ Activity │ │ Settings  │ │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └─────┬─────┘ │
│       └─────────────┴────────────┴─────────────┘       │
│                         │                              │
│              ┌──────────▼──────────┐                   │
│              │    MalwareScanner   │  (Unified Scanner) │
│              └──┬──────┬──────┬───┘                    │
│        ┌────────▼──┐ ┌─▼────┐ ┌▼──────────┐           │
│        │Signature  │ │ YARA │ │AI Detection│           │
│        │Engine     │ │Engine│ │Engine      │           │
│        │(LMDB)     │ │(.dll)│ │(ONNX)      │           │
│        └───────────┘ └──────┘ └────────────┘           │
│                                                        │
│  ┌──────────────────┐  ┌─────────────────────────┐     │
│  │DirectoryMonitor  │  │ ScanService (async)      │     │
│  │(Real-Time Watch) │  │ (File/Dir/Multi scan)    │     │
│  └──────────────────┘  └─────────────────────────┘     │
└────────────────────────────────────────────────────────┘
```

---

## Tech Stack

| Component | Technology |
|---|---|
| Framework | WinUI 3 / Windows App SDK |
| Runtime | .NET 8.0 (`net8.0-windows10.0.19041.0`) |
| Language | C# 12+ (latest) |
| MVVM | CommunityToolkit.Mvvm 8.x |
| Graphics | Microsoft.Graphics.Win2D 1.x + custom HLSL shaders |
| Signature DB | LMDB via LightningDB 0.16.0 |
| YARA | Native `libyara64.dll` (dnYara 2.1.0 + custom P/Invoke) |
| AI/ML | Microsoft.ML.OnnxRuntime 1.19.x |
| Logging | Serilog 4.x + Serilog.Sinks.File 6.x |
| Icons | System.Drawing.Common 8.x |
| Packaging | Unpackaged (MSIX tooling enabled, `WindowsPackageType=None`) |

---

## Prerequisites

### Required

| Tool | Version | Purpose |
|---|---|---|
| **Windows 10/11** | Build 17763+ (1809+) | Minimum OS version |
| **Visual Studio 2022** | 17.8+ | IDE (recommended) |
| **.NET 8 SDK** | 8.0+ | Build & run (`winget install Microsoft.DotNet.SDK.8`) |
| **Windows App SDK** | 1.x | WinUI 3 runtime (auto-restored via NuGet) |

### Visual Studio Workloads

Install the following via the Visual Studio Installer:

1. **.NET Desktop Development**
2. **Windows application development** (includes Windows App SDK / WinUI 3 templates)

### Optional (for AI model training / database operations)

| Tool | Version | Purpose |
|---|---|---|
| **Python** | 3.9+ | AI model training & database conversion scripts |
| **LightGBM** | — | ML model training (`pip install lightgbm`) |
| **ONNX tools** | — | Model conversion (`pip install onnxmltools skl2onnx onnxruntime numpy`) |
| **pefile** | — | PE feature extraction (`pip install pefile`) |
| **lmdb + mmh3** | — | SQLite→LMDB conversion (`pip install lmdb mmh3`) |

---

## Environment Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd AFTERLiFEv2/AfterlifeWinUI
```

### 2. Install .NET 8 SDK

```powershell
# Via winget
winget install Microsoft.DotNet.SDK.8

# Verify installation
dotnet --version
```

### 3. Restore NuGet Packages

```powershell
cd AfterlifeWinUI
dotnet restore
```

This automatically downloads all NuGet dependencies:
- `Microsoft.WindowsAppSDK`
- `LightningDB` (LMDB)
- `dnYara` / `dnYara.Interop`
- `Microsoft.ML.OnnxRuntime`
- `CommunityToolkit.Mvvm`
- `Serilog` / `Serilog.Sinks.File`
- `Microsoft.Graphics.Win2D`
- `System.Drawing.Common`

### 4. Verify Native DLL

The project requires `libyara64.dll` in the project root directory. This file is already included in the repository and is automatically copied to the output directory during build. Verify it exists:

```
AfterlifeWinUI/libyara64.dll   (~2.3 MB)
```

---

## Database & Resource Acquisition

The application uses three detection databases that must be present in the `resources/` folder at the **solution root** (`AFTERLiFEv2/resources/`). The build process automatically copies them to the output directory.

### Required Resources

| Resource | Location | Size | Description |
|---|---|---|---|
| **Signature DB** | `resources/malware.lmdb/data.mdb` | ~9 GB | LMDB hash database (MD5/SHA1/SHA256) |
| **YARA Rules** | `resources/yara_rules.yar` | ~44 MB | YARA rule definitions |
| **AI Models** | `AfterlifeWinUI/resources/ai_models/*.onnx` | ~28 MB total | 6 LightGBM ONNX models |

### Signature Database (LMDB)

The LMDB database contains malware hash signatures in three sub-databases (`md5`, `sha1`, `sha256`). Each entry maps a binary hash to a malware name string.

**Option A — Use the pre-built LMDB database:**

Place the `malware.lmdb/` folder (containing `data.mdb` and optionally `lock.mdb`) into `AFTERLiFEv2/resources/`.

**Option B — Convert from a SQLite source database:**

If you have a SQLite database with a `hash_signatures` table (`hash_type`, `hash_value`, `name`), convert it:

```powershell
# Install dependencies
pip install lmdb mmh3

# Run the conversion script
python scripts/convert_to_lmdb.py resources/malware.db resources/malware.lmdb
```

The SQLite schema expected:
```sql
CREATE TABLE hash_signatures (
    hash_type TEXT,     -- 'md5', 'sha1', or 'sha256'
    hash_value TEXT,    -- hex-encoded hash string
    name TEXT           -- malware family/variant name
);
```

### YARA Rules

Place your YARA rules file at `resources/yara_rules.yar`. The engine compiles rules on first launch and caches the compiled output as `yara_rules.yarc` for faster subsequent loads.

### AI / ONNX Models

The project ships with 6 pre-trained LightGBM models converted to ONNX format, located in `AfterlifeWinUI/resources/ai_models/`:

| Model File | Type | Features |
|---|---|---|
| `batch1_histogram.onnx` | Histogram | 512 (byte histogram + byte-entropy) |
| `batch1_string.onnx` | String | 243 (string stats + patterns) |
| `batch2_histogram.onnx` | Histogram | 512 |
| `batch2_string.onnx` | String | 243 |
| `batch3_histogram.onnx` | Histogram | 512 |
| `batch3_string.onnx` | String | 243 |

**Retraining / converting new models:**

1. Train LightGBM models using `malware_predictor.py` (trained on EMBER2024 PE features)
2. Save as LightGBM text format (`.txt`) in `models_ensemble/`
3. Convert to ONNX:

```powershell
pip install lightgbm onnxmltools skl2onnx onnxruntime numpy
python convert_models_to_onnx.py
```

This produces `.onnx` files in `resources/ai_models/`.

---

## Building & Compiling

### Using Visual Studio

1. Open `AfterlifeWinUI.slnx` (or `AFTERLiFEv2.slnx` for the full solution)
2. Set the platform to **x64**
3. Set the configuration to **Debug** or **Release**
4. Build → Build Solution (Ctrl+Shift+B)

### Using Command Line

```powershell
# Debug build (default)
dotnet build -c Debug -p:Platform=x64

# Release build
dotnet build -c Release -p:Platform=x64

# Publish self-contained
dotnet publish -c Release -r win-x64 --self-contained
```

### Using the Build Script (full solution)

From the solution root (`AFTERLiFEv2/`):

```powershell
# Debug build (UI only, skip C++ core)
.\build.ps1 -SkipCore

# Release build
.\build.ps1 -Release -SkipCore

# Full clean rebuild including C++ core
.\build.ps1 -Release -CleanBuild
```

### Build Output

The build automatically:
1. Restores NuGet packages
2. Compiles the C# WinUI 3 application
3. Copies `libyara64.dll` to the output directory
4. Copies detection resources (LMDB, YARA rules, ONNX models) to `$(OutDir)/resources/`

Output path: `bin/<Config>/net8.0-windows10.0.19041.0/`

### Supported Platforms

| Platform | RID | Status |
|---|---|---|
| x64 | `win-x64` | ✅ Primary |
| x86 | `win-x86` | ✅ Supported |
| ARM64 | `win-arm64` | ✅ Supported |

---

## Running the Application

```powershell
# From the project directory after building
dotnet run -c Debug -p:Platform=x64

# Or directly run the executable
.\bin\Debug\net8.0-windows10.0.19041.0\AfterlifeWinUI.exe
```

### First Launch

On first launch, the application:
1. Creates `%LOCALAPPDATA%\AfterlifeWinUI\` with subdirectories for logs, quarantine, and settings
2. Loads signature database, YARA rules, and AI models from `resources/`
3. Opens the main window with the Dashboard view

If resources are missing, the app will still launch but with reduced detection capability (logged as warnings).

---

## Project Structure

```
AfterlifeWinUI/
├── App.xaml / App.xaml.cs              # App entry point, service initialization
├── MainWindow.xaml / .cs               # Main window with sidebar nav + animated orbs
├── Imports.cs                          # Global using directives
├── Package.appxmanifest                # App identity & capabilities
├── app.manifest                        # Windows app manifest
├── AfterlifeWinUI.csproj               # Project file & build targets
│
├── Views/                              # XAML Pages
│   ├── DashboardPage.xaml / .cs        # Quick/full scan, drag-drop, stats overview
│   ├── ThreatsPage.xaml / .cs          # Threat history list with actions
│   ├── ActivityPage.xaml / .cs         # Timestamped activity log
│   ├── SystemStatusPage.xaml / .cs     # Engine status & system info
│   └── SettingsPage.xaml / .cs         # Theme, engines, whitelist, monitored dirs
│
├── ViewModels/
│   ├── MainViewModel.cs                # Sidebar state, scanner stats binding
│   └── DashboardViewModel.cs           # Scan progress, threat counts, real-time stats
│
├── Services/                           # Core business logic
│   ├── MalwareScanner.cs               # Unified 3-engine scanner (singleton)
│   ├── SignatureEngine.cs              # LMDB hash lookup (MD5/SHA1/SHA256)
│   ├── YaraEngine.cs                   # Native YARA rule compilation & scanning
│   ├── YaraNative.cs                   # P/Invoke declarations for libyara64.dll
│   ├── AIDetectionEngine.cs            # ONNX model loading & ensemble inference
│   ├── PEFeatureExtractor.cs           # PE histogram + string feature extraction
│   ├── ScanService.cs                  # Async scan orchestration with events
│   ├── DirectoryMonitorService.cs      # Real-time FileSystemWatcher monitor
│   ├── FileTypeFilter.cs              # Extension-based scan filtering
│   ├── ConfigurationManager.cs         # AppData path management
│   ├── AppSettingsService.cs           # User settings (JSON persistence)
│   ├── WindowSettingsService.cs        # Window position/size persistence
│   ├── ThreatHistoryService.cs         # Threat history (JSON persistence)
│   ├── ActivityLogService.cs           # Activity log service
│   └── CoreService.cs                  # Core interop service
│
├── Controls/
│   └── FrostedGlassPanel.cs            # Custom frosted glass UI control
│
├── Effects/
│   └── GlassEffect.cs                  # Win2D glass effect implementation
│
├── Shaders/
│   └── FrostedGlassShader.hlsl         # HLSL pixel shader for glass blur
│
├── Animations/
│   └── PageAnimations.cs               # Page transition & orb animations
│
├── Assets/                             # App icons & logos
├── Properties/                         # Launch settings & publish profiles
├── resources/ai_models/                # ONNX model files (6 models)
├── models_ensemble/                    # LightGBM source models (text format)
│
├── malware_predictor.py                # Python: EMBER2024 feature extraction + prediction
├── convert_models_to_onnx.py           # Python: LightGBM → ONNX converter
└── libyara64.dll                       # Native YARA library (x64)
```

---

## Detection Engines

### 1. Signature Engine (`SignatureEngine.cs`)

- **Database**: LMDB (Lightning Memory-Mapped Database)
- **Hash Types**: MD5, SHA-1, SHA-256
- **Lookup**: Binary key lookup → malware name
- **Max DB Size**: 10 GB map size
- **Performance**: Memory-mapped, zero-copy reads

### 2. YARA Engine (`YaraEngine.cs`)

- **Library**: Native `libyara64.dll` via P/Invoke
- **Rules**: Loaded from `yara_rules.yar`, compiled at startup
- **Caching**: Compiled rules saved as `.yarc` for faster reloads
- **False-Positive Filtering**: Rules like `generic_string`, `upx_packed`, `nsis_installer` are auto-filtered
- **Confidence**: Calculated based on rule quality, match count, and file type
- **Timeout**: 60-second scan timeout per file

### 3. AI Detection Engine (`AIDetectionEngine.cs`)

- **Models**: LightGBM ensemble (3 histogram + 3 string models)
- **Runtime**: ONNX Runtime (CPU)
- **PE-Only**: Only scans valid Win32 PE files (EXE, DLL, SYS, SCR, DRV, OCX, CPL)
- **Features**: Byte histogram (256) + byte-entropy histogram (256) = 512 histogram features; string statistics + printable distribution + pattern counts = 243 string features
- **Thresholds**: ≥0.70 = high confidence, 0.55–0.70 = medium, 0.45–0.55 = suspicious zone
- **Trust Heuristics**: Signed executables with low scores bypass detection; large files with high import counts are trusted

### Detection Flow

```
File → FileTypeFilter → Whitelist Check
  ↓
  ├─ Signature Engine (fastest, MD5→SHA256→SHA1)
  ├─ YARA Engine (rule matching with FP filtering)  
  └─ AI Engine (PE-only, ensemble scoring with trust heuristics)
  ↓
Combined Result → Confidence scoring → Threat classification
```

---

## AI Model Pipeline

### Training (Python)

The `malware_predictor.py` script implements the full EMBER2024 feature extraction pipeline:

1. **Raw Feature Extraction**: Byte histogram, byte-entropy, strings (counts + patterns), PE headers (COFF, optional, DOS), sections, imports, exports, data directories
2. **Vectorization**: Converts raw dictionaries into flat feature vectors with FeatureHasher for variable-length features
3. **Prediction**: LightGBM models predict malware probability (0.0–1.0)

### Conversion to ONNX (Python → C#)

```
LightGBM .txt models → convert_models_to_onnx.py → .onnx files → C# OnnxRuntime
```

### Feature Extraction (C#)

`PEFeatureExtractor.cs` reimplements the Python feature extraction in C# for runtime inference:
- **Histogram features** (512): byte frequency distribution + sliding-window entropy
- **String features** (243): string statistics, printable character distribution, vocabulary-based pattern counting

---

## Configuration & Data Paths

### Application Data

All user data is stored in `%LOCALAPPDATA%\AfterlifeWinUI\`:

| Path | Purpose |
|---|---|
| `app-settings.json` | User preferences (theme, engines, whitelist, monitored dirs) |
| `window-settings.json` | Window position and size |
| `threat-history.json` | Detected threat history |
| `activity-log.json` | Activity log entries |
| `logs/` | Serilog rolling log files (`afterlife-YYYY-MM-DD.log`) |
| `quarantine/` | Quarantined files |

### Detection Resources (Build Output)

Copied to `$(OutDir)/resources/` during build:

| Path | Source |
|---|---|
| `resources/malware.lmdb/data.mdb` | From solution root `resources/` |
| `resources/yara_rules.yar` | From solution root `resources/` |
| `resources/ai_models/*.onnx` | From `AfterlifeWinUI/resources/ai_models/` |

---

## Logging

Serilog writes rolling daily logs to `%LOCALAPPDATA%\AfterlifeWinUI\logs\`:

- **Format**: `yyyy-MM-dd HH:mm:ss.fff [LEVEL] Message`
- **Retention**: Last 7 days
- **Flush**: Every 1 second (unbuffered)
- **Levels**: `Debug` minimum (adjustable)

Example log output:
```
2026-04-05 13:00:01.234 [INF] === AFTERLiFE Application Starting ===
2026-04-05 13:00:01.456 [INF] MalwareScanner ready - 50,000,000 signatures, 12,000 YARA rules, 6 AI models
2026-04-05 13:00:05.789 [WRN] [SignatureEngine] MD5 match: C:\test.exe -> Trojan.GenericKD.12345
```

---

## Troubleshooting

| Issue | Solution |
|---|---|
| **"Signature database not found"** | Ensure `resources/malware.lmdb/data.mdb` exists at the solution root. The build copies it to the output. |
| **"YARA rules not found"** | Place `yara_rules.yar` in the solution root `resources/` folder. |
| **"libyara64.dll not found"** | Verify `libyara64.dll` exists in the `AfterlifeWinUI/` project root. |
| **"No ONNX models found"** | Run `python convert_models_to_onnx.py` to generate models in `resources/ai_models/`. |
| **Build fails with WinUI errors** | Install the "Windows application development" workload in Visual Studio Installer. |
| **Platform not supported** | Ensure you're building for `x64`, `x86`, or `ARM64` (not `AnyCPU`). |
| **App launches but no detections** | Check logs at `%LOCALAPPDATA%\AfterlifeWinUI\logs\` for initialization errors. |
| **High memory usage** | The LMDB database is memory-mapped. Actual RAM usage depends on OS page cache. |

---

## License

Private — All rights reserved.
