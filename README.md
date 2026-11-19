# CoreDotAI Desktop

A Windows-first AI interview and meeting assistant. This is an independent implementation and is not affiliated with ParakeetAI.

## Current status
- **Phase 1 – complete:** WPF desktop shell and ASP.NET Core health endpoint.
- **Phase 2 – in progress:** consent-gated microphone and Windows system-audio capture to local WAV files.
- **Not implemented yet:** speech-to-text, AI answer generation, resume ingestion, and mock interview workflows.

## Technology
- .NET 8 / C#
- WPF desktop client
- ASP.NET Core Minimal API
- NAudio for Windows audio capture

## Prerequisites
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 with .NET desktop and ASP.NET workloads

## Build
```powershell
dotnet restore .\CoreDotAI.sln
dotnet build .\CoreDotAI.sln --configuration Release
```

## Run the API
```powershell
dotnet run --project .\src\CoreDotAI.Api
```
Health check: http://localhost:5080/health

## Run the desktop client
```powershell
dotnet run --project .\src\CoreDotAI.Desktop
```

## Audio capture
1. Connect the microphone and play the meeting audio through the Windows playback device.
2. Launch CoreDotAI.
3. Select the desired audio sources.
4. Confirm that you have permission to capture/process the conversation.
5. Select **Start capture**. The app displays a recording indicator.
6. Select **Stop capture** to close and finalize the WAV files.

Files are stored under:
`%LOCALAPPDATA%\CoreDotAI\Recordings\<session timestamp>\`

Microphone and system audio are stored separately. This phase does not upload audio or perform transcription. System loopback captures the default Windows playback device; it does not extract audio directly from a meeting application's private stream.

## Privacy and responsible use
- Capture is off by default and requires explicit user confirmation.
- Use only where all required participants have given permission and AI assistance/recording is allowed.
- A visible recording indicator and immediate stop control are provided.
- Audio remains local in this phase. Never commit API keys, recordings, or personal documents.

## CI
A Windows GitHub Actions workflow restores and builds the solution on pushes and pull requests.

## Roadmap
1. Desktop/API foundation
2. Audio capture and transcription
3. AI answer orchestration
4. Resume context and mock interviews
5. Persistence, settings, packaging, and tests
