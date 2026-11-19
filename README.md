# CoreDotAI Desktop

A Windows-first AI interview and meeting assistant. This is an independent implementation and is not affiliated with ParakeetAI.

## Current status
- Phase 1: WPF desktop shell and ASP.NET Core health endpoint.
- Phase 2: consent-gated microphone and Windows system-audio capture to local WAV files.
- Phase 3 initial implementation: user-selected WAV transcription through the OpenAI Audio Transcriptions API.
- Phase 4 initial implementation: Gemini-powered interview answer generation from a user-entered question, optional job role, and candidate-provided background.
- Not implemented yet: live streaming transcription, resume ingestion, and mock interview workflows.

## Technology
- .NET 8 / C#
- WPF desktop client
- ASP.NET Core Minimal API
- NAudio for Windows audio capture
- OpenAI Audio Transcriptions API using model gpt-transcribe
- Google Gemini API key entry for the upcoming AI answer-generation feature

## Prerequisites
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 with .NET desktop and ASP.NET workloads
- An OpenAI API key with API billing/credits enabled for transcription

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
1. Connect the microphone and play meeting audio through the Windows playback device.
2. Launch CoreDotAI.
3. Select the desired audio sources.
4. Confirm that you have permission to capture/process the conversation.
5. Select Start capture. The app displays a recording indicator.
6. Select Stop capture to finalize the WAV files.

Files are stored under:
%LOCALAPPDATA%\CoreDotAI\Recordings\<session timestamp>\

Microphone and system audio are stored separately. System loopback captures the default Windows playback device; it does not extract audio directly from a meeting application's private stream.

## API key setup
The desktop UI provides separate masked fields for OpenAI and Google Gemini API keys. Enter each key and click its corresponding button. Keys entered in the UI are kept in memory only for the current app session and cleared when the app closes. Do not paste keys into source code or commit them to Git.

- OpenAI key: used by the current WAV transcription feature. It can also be provided through the current Windows user's `OPENAI_API_KEY` environment variable.
- Gemini key: used by the Interview Coach to generate sample answers. The key is held for the current app session only. Create/manage the key in [Google AI Studio](https://aistudio.google.com/apikey).

## Gemini Interview Coach
1. Enter your Gemini API key in the AI Provider panel and click **Use Gemini API Key**.
2. Scroll down to **Gemini Interview Coach** in the Workspace panel.
3. Enter the job role (optional), the interview question, and any relevant candidate background (optional).
4. Select **Generate Interview Answer**. The question and supplied context are sent to Google's Gemini API.
5. Review the generated answer and use **Copy Answer** if needed.

The coach uses Google's Gemini Interactions API with the `gemini-3.8-flash` model. Requests set `store: false`. Generated answers are suggestions: verify them and personalize any placeholders before using them. The coach is for interview preparation and should be used in line with the interview organizer's rules; it is not designed for covert assistance during a live interview.

In Command Prompt, replace the placeholder with your own API key:
```cmd
setx OPENAI_API_KEY "YOUR_OPENAI_API_KEY"
```
Close and reopen Command Prompt and CoreDotAI after running setx. Never share screenshots or logs containing the key.

To transcribe:
1. Select Select WAV and choose a recorded WAV file.
2. Confirm that the selected recording may be uploaded for processing.
3. Select Transcribe and confirm the upload.
4. The transcript appears in the app and is saved beside the audio as a .transcript.txt file.

The API currently accepts audio files up to 25 MB. This initial implementation rejects larger files; long-recording chunking and live streaming will be added later.

## Privacy
- Capture is off by default and requires explicit user confirmation.
- Use only where all required participants have given permission and AI assistance/recording is allowed.
- Audio remains local until the user selects a file and confirms upload for transcription.
- Interview questions and optional candidate context are sent to Gemini only when the user selects Generate Interview Answer.
- Never commit API keys, recordings, or personal documents.

## CI
A Windows GitHub Actions workflow restores and builds the solution on pushes and pull requests.

## Roadmap
1. Desktop/API foundation
2. Audio capture and speech-to-text
3. Live streaming transcription and AI answer orchestration
4. Resume context and mock interview workflows
5. Persistence, settings, packaging, and tests
