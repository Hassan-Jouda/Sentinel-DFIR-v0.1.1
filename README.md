# Sentinel DFIR

**Windows Security Monitoring & Digital Forensics Prototype**  
*Monitor · Detect · Investigate · Respond*

> **Status: v0.1 starter source code (experimental).** This repository is **not** a commercial antivirus, a production-hardened EDR, or a prebuilt EXE/installer. Build and test it on **Windows 10/11 x64** with the **.NET 10 SDK**. A multi-file WPF publishing workaround is documented below; it is not a verified fix for every crash.

## Overview

**Sentinel DFIR** is a local Windows endpoint-monitoring and digital-forensics learning project built in **C# / .NET** with a background agent and a **WPF** desktop dashboard. It observes selected process and network activity, optionally subscribes to Microsoft Sysmon events, assigns illustrative heuristic scores, and records structured evidence events for review.

The long-term vision is to bring together **endpoint monitoring, Mini-SOC workflows, threat hunting, incident response, and digital forensics** in one interface. Those capabilities are **goals**, not claims about this starter version.

**بالعربية:** Sentinel DFIR مشروع تجريبي لمراقبة أمن أجهزة Windows وتحليل الأحداث والأدلة الرقمية. النسخة الحالية محدودة وتحتاج إلى بناء واختبار محلي، ولا تُعد مضاد فيروسات تجاريًا أو نظام EDR جاهزًا للإنتاج.

## Current features — v0.1 starter

| Component | Implemented capability | Important limitation |
| --- | --- | --- |
| Agent | .NET Worker that runs as a console application or Windows Service | Service installation requires administrator privileges |
| Process monitoring | Observes process starts using **5-second polling** | Very short-lived processes can be missed |
| Network monitoring | Polls **established TCP connections** | Polled connections **do not have attributed process PIDs** |
| Optional Sysmon | Live subscription to event IDs **1, 3, 11, 12, 13, 14, 22** | Requires separately installed and configured Sysmon; event coverage depends on configuration |
| Risk scoring | Example rule-based / heuristic severity scores | May produce false positives; **not a malware verdict** |
| Event evidence | Appends JSON Lines records to `%ProgramData%\SentinelDFIR\events.jsonl` | Not a hardened forensic repository |
| Desktop UI | WPF dashboard: recent events, severity counters, event details, and live process inventory | UI publishing/runtime compatibility needs testing |
| Manual response | End a **user-selected, non-protected process** after a warning and confirmation | May cause lost work; not automated remediation |
| Reporting | Export a basic **HTML** event report and accompanying `.sha256` checksum | A checksum alone is not chain-of-custody evidence |

**Note:** Events observed through Sysmon are distinct from what basic polling can capture. Do not interpret the existence of an event type in Sysmon as full coverage by the built-in collectors.

## Architecture

```text
Sentinel DFIR
├── Sentinel.Core       Shared event schema, JSONL storage, sample scoring rules
├── Sentinel.Agent      Windows Worker Service and telemetry collectors
├── Sentinel.Desktop    WPF monitoring dashboard
├── Build.ps1           Local build script
├── Publish.ps1         Publishes agent and desktop outputs (not an installer)
├── Install-Service.ps1
└── Uninstall-Service.ps1
```

**Agent:** collects events in the background.  
**Core:** provides shared data models, storage, and example scoring.  
**Desktop:** displays collected information and offers explicitly confirmed manual actions.

No remote telemetry transmission is implemented in this starter.

## Requirements

- **Windows 10/11 x64** (build and test target).
- Official **.NET 10 SDK**: <https://dotnet.microsoft.com/download>.
- PowerShell and access to download build dependencies as needed. Runtime event collection can work offline.
- **Administrator PowerShell** for installing or managing the Windows Service and for log access requiring elevation.
- **Optional:** Microsoft Sysmon installed, enabled, and configured for the desired events.

Verify .NET installation:

```powershell
dotnet --version
dotnet --list-sdks
dotnet --list-runtimes
```

Use an SDK that supports the project's `net10.0-windows` target. A .NET Runtime alone is **not sufficient to build the source**.

## Build and run locally

Open PowerShell **in the extracted project root**.

**1. Build the solution**

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

**2. Run the agent (in an Administrator PowerShell session)**

```powershell
dotnet run --project .\src\Sentinel.Agent\Sentinel.Agent.csproj
```

**3. Run the desktop app (in a separate PowerShell session)**

```powershell
dotnet run --project .\src\Sentinel.Desktop\Sentinel.Desktop.csproj
```

Stop the console agent with **Ctrl+C**. If a service instance is already running, avoid starting a duplicate agent unintentionally.

## Publish EXE outputs

The project includes a publish script:

```powershell
powershell -ExecutionPolicy Bypass -File .\Publish.ps1
```

Expected output locations *when the script uses the project's documented default paths*:

```text
dist\agent\Sentinel.Agent.exe
dist\desktop\Sentinel.Desktop.exe
```

These are application outputs, **not** an MSI installer. Actual publish layout and dependencies depend on the script settings. An EXE may require accompanying DLL and runtime files; do not assume it is portable on its own.

### WPF crash workaround: publish Desktop as multiple files

A reported Windows **Application Event ID 1026** for `Sentinel.Desktop.exe` included:

```text
System.DllNotFoundException: Dll was not found.
at MS.Internal.WindowsBase.NativeMethodsSetLastError.SetWindowLongPtrWndProc
at MS.Win32.HwndSubclass.HookWindowProc
```

Single-file publishing (`PublishSingleFile=true`) is a **possible** contributor to this failure, not a confirmed root cause. `KERNELBASE.dll` appearing in an error report does **not** by itself mean that Windows DLL is corrupt.

From the project root, try publishing the desktop as a **self-contained, untrimmed, multi-file** application:

```powershell
dotnet publish .\src\Sentinel.Desktop\Sentinel.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:PublishTrimmed=false `
  -o .\dist\desktop-fixed
```

Then launch:

```powershell
.\dist\desktop-fixed\Sentinel.Desktop.exe
```

**Keep the entire `desktop-fixed` folder intact**, including DLLs and other published files. This procedure does not require deleting event logs or reinstalling Windows. Its success **has not been verified** on the affected Windows machine.

If the app still exits, inspect **Event Viewer → Windows Logs → Application**, capture the full .NET exception and related event entries, and verify installed runtime/architecture, file dependencies, and the publish command before changing unrelated Windows components.

Relevant issue report: <https://github.com/dotnet/runtime/issues/61279>.

## Install and manage the Windows Service

In an **Administrator** PowerShell session:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Service.ps1
Get-Service SentinelAgent
```

To stop and unregister the service:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-Service.ps1
```

The provided demo scripts install/uninstall the **background service registration**, not the WPF Desktop application or a full installer. Uninstalling the service does **not** remove stored evidence or binaries according to the starter script's documented behavior.

## Optional Sysmon telemetry

The agent can subscribe to the Windows Event Log channel:

```text
Microsoft-Windows-Sysmon/Operational
```

Target event IDs: **1, 3, 11, 12, 13, 14, 22**. Sysmon must be installed and appropriately configured **separately**. Sentinel DFIR does **not** automatically alter Sysmon configuration or Windows Security Audit Policy.

## Evidence and reports

Default event log location:

```text
%ProgramData%\SentinelDFIR\events.jsonl
```

Records are stored as **JSON Lines** (one JSON object per line). HTML report export can include a matching `.sha256` file to help detect later changes. This is a **basic integrity check**, not cryptographically signed evidence, a provenance guarantee, or a complete chain of custody.

Do **not** upload or share raw event logs without checking for sensitive **process, command-line, DNS, file-path, and user** information.

## Not yet implemented

- Complete native USB, Registry, and file-change coverage without Sysmon.
- YARA-X/YARA and Sigma integration; MITRE ATT&CK technique mapping.
- SQLite evidence database, advanced event correlation, and full threat hunting.
- Signed forensic evidence bundles, defensible chain-of-custody, and memory acquisition.
- AI-assisted analysis, automated containment, or file quarantine.
- A full MSI/EXE setup installer and deployment management.
- IPC command authorization and hardened storage ACLs.
- Robust file rotation, retention controls, and event back-pressure guarantees.
- Automated unit/integration tests and production security hardening.
- Full investigative exports such as PDF/CSV/JSON case bundles.

## Development roadmap

- [ ] Resolve and reproduce WPF startup/publishing compatibility issues across supported Windows versions.
- [ ] Add automated builds, tests, and diagnostic logging.
- [ ] Harden local inter-process commands, user authorization, and filesystem permissions.
- [ ] Add storage rotation, retention, overflow accounting, and evidence-integrity controls.
- [ ] Improve event search, timelines, filters, correlation, and reporting.
- [ ] Add controlled YARA/Sigma rule support and MITRE ATT&CK mapping.
- [ ] Expand process, network, USB, Registry, and file telemetry with explicit coverage guarantees.
- [ ] Add safer incident-response workflows, provenance tracking, and an installer.

## Security, privacy, and safety

- **Only monitor systems you own or are expressly authorized to administer.**
- **Test in a Windows virtual machine first** and take a snapshot before installing the service.
- The starter **does not send telemetry** to remote servers, but its local records can still contain sensitive information.
- **Do not treat heuristic scores as proof of malware.** Validate all suspicious findings independently.
- Process termination is **manual**, asks for confirmation, and can cause data loss. It checks PID, name, and start time; a hardened implementation should also validate executable identity and permissions.
- There is **no automatic quarantine, Defender disabling, or stealth functionality**.
- A bounded in-memory event queue can drop oldest events under heavy load, creating **evidence gaps**.
- Storage permissions, retention, and security boundaries require hardening before any real-world deployment.
- The starter is intended for **education and controlled testing**, not for critical incident-response reliance.

## License and distribution

Educational starter project. **No explicit open-source license has been specified** in this README; do not assume redistribution rights or a standard license. Review dependency/library licenses and add an appropriate `LICENSE` file before public distribution.

---

**Sentinel DFIR** — *Monitor. Detect. Investigate. Respond.*
