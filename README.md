# CoreDotAI Desktop

A Windows-first AI interview and meeting assistant. This repository is the original implementation; it is not affiliated with or a copy of ParakeetAI.

## Current status
**Phase 1 – foundation:** repository scaffold, WPF desktop shell, and ASP.NET Core API health endpoint.

## Planned capabilities
- Consent-based microphone and system-audio capture with visible recording state
- Live speech transcription
- Context-aware AI answer suggestions
- Resume and job-description context
- Mock interview practice and session history
- Configurable AI provider integration

## Technology
- .NET 8 / C#
- WPF desktop client
- ASP.NET Core Minimal API
- MVVM-ready project structure

## Prerequisites
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 with the .NET desktop development workload and ASP.NET workload

## Build
```powershell
dotnet restore .\CoreDotAI.sln
dotnet build .\CoreDotAI.sln
```

## Run the API
```powershell
dotnet run --project .\src\CoreDotAI.Api
```
Open `http://localhost:5080/health`.

## Run the desktop client
```powershell
dotnet run --project .\src\CoreDotAI.Desktop
```

## Privacy and responsible use
Audio capture is intended for calls where the user has permission to record/process audio and where AI assistance is allowed. The app must show a clear recording indicator and provide an immediate stop control. Never hard-code API keys or commit secrets.

## Roadmap
1. Desktop/API foundation
2. Audio capture and transcription
3. AI answer orchestration
4. Resume context and mock interviews
5. Persistence, settings, packaging, and tests
