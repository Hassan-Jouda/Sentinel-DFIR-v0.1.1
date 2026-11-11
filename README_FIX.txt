Sentinel DFIR v0.1.1 - WPF Desktop publication repair
===================================================
Root cause suspected from Windows Event 1026: System.DllNotFoundException in WPF HwndSubclass.
Original v0.1 Publish.ps1 used PublishSingleFile=true for WPF desktop. This patch publishes WPF desktop as a self-contained multi-file directory to avoid the native-DLL bundling issue.

From PowerShell IN THIS FOLDER:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-Desktop.ps1
  .\dist\desktop-fixed\Sentinel.Desktop.exe

IMPORTANT:
- The output folder dist\desktop-fixed is a complete deployable folder; do NOT run/copy the EXE alone without its DLLs.
- Keep SentinelAgent service installed and running; this repair does not stop, uninstall, or modify it.
- Do not delete C:\ProgramData\SentinelDFIR or events.jsonl.
- This is an untested source fix because Windows/.NET SDK execution is not available in this environment.
- It does not implement new features beyond the v0.1 prototype.

Arabic:
هذا تعديل لطريقة نشر الواجهة فقط، ويحافظ على الخدمة وسجل الأحداث. شغّل Fix-Desktop.ps1 ثم ملف EXE من dist\desktop-fixed مع إبقاء ملفات DLL المجاورة له في نفس المجلد.
