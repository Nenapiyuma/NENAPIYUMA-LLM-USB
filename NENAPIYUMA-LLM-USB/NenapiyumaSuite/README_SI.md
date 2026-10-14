# NENAPIYUMA AI Suite — Windows x64

මෙම folder එකේ Windows 10/11 x64 සඳහා native desktop app දෙකක් ඇත:

- **NENAPIYUMA Chat** — portable local chat, Sinhala/English, coding, chat search, JSON backup import/export, local history.
- **NENAPIYUMA Studio AI** — එකම ප්‍රධාන input එකෙන් chat/coding task routing, Thinking/Spike modes, local history, සීමිත approval-gated Windows app launching.

මෙය existing NENAPIYUMA-LLM-USB Python/Tkinter app එකට අමතර solution එකකි. පැරණි app එකේ files මකා හෝ වෙනස් කර නැත.

## EXE ලබාගැනීම

1. GitHub repository එකේ **Actions** වෙත යන්න: https://github.com/Nenapiyuma/NENAPIYUMA-LLM-USB/actions
2. **Build NENAPIYUMA Windows AI Suite** workflow එක තෝරන්න.
3. Push build එකේදී source projects සහ tests build වෙයි. Artifact එකේ NENAPIYUMA-Chat/NENAPIYUMA.Chat.exe සහ NENAPIYUMA-Studio/NENAPIYUMA.Studio.exe තිබේ.
4. Model/runtime ඇතුළත් offline bundle එකක් හදන්න නම් **Run workflow** තෝරා “Include offline GGUF model and llama.cpp runtime” එක සක්‍රිය කරන්න. Model download එක විශාල බැවින් එය default ලෙස සක්‍රිය නැත.
5. සාර්ථක run එකේ **Artifacts** කොටසෙන් NENAPIYUMA-Windows-x64-Suite ZIP එක බාගෙන extract කරන්න. Artifact එක දින 14කින් expire වේ.

**වැදගත්:** Build සාර්ථක වීමෙන් පමණක් model/runtime එක artifact එකට ඇතුළත් වූ බව අදහස් නොවේ. Optional offline model input එක enable කළේද, artifact එකේ NENAPIYUMA-Chat/models/*.gguf සහ NENAPIYUMA-Chat/tools/llama-server.exe තිබේද කියා පරීක්ෂා කරන්න.

## Windows PC එකේ source වලින් build කිරීම

- .NET 10 SDK සහ Windows 10/11 x64 අවශ්‍යයි.
- PowerShell project folder එකට යන්න:

    Set-ExecutionPolicy -Scope Process Bypass
    .\scripts\build-windows.ps1

Script එක dependencies restore කර, core self-tests run කර, apps දෙකම Release self-contained win-x64 output ලෙස publish කරයි. Expected folders: release/NENAPIYUMA-Chat/ සහ release/NENAPIYUMA-Studio/. Build failure එකක් සිදුවුවහොත් script එක exit failure ලබාදෙයි.

## Local model setup සහ auto-detect

- GGUF model එක NENAPIYUMA-Chat/models/ තුළ තබන්න.
- Windows x64 llama.cpp runtime එක (llama-server.exe සහ එයට අවශ්‍ය DLLs ඇතුළුව) NENAPIYUMA-Chat/tools/ තුළ තබන්න.
- Chat සහ Studio model picker එකක් නොපෙන්වයි. helalm-4b.Q8_0.gguf තිබේ නම් එයට මුල් ප්‍රමුඛතාවය දෙයි; නැතිනම් දන්නා Qwen2.5 3B/1.5B file names හෝ වෙනත් complete GGUF එකක් සොයයි.
- Studio එක NENAPIYUMA-Chat sibling folder එකේ model/runtime සොයා බලන නිසා folders දෙකම එකම extracted release directory එක යට තබාගන්න.
- Model එක EXE එක තුළ embed කර නැත. File name සහ size check එකෙන් format compatibility, license හෝ model quality තහවුරු නොවේ.
- RAM අවශ්‍යතාව model size/context එක මත රඳා පවතී. 3B Q4 model එකක් වුවත් Windows, context memory සහ වෙනත් apps නිසා 8 GB RAM එකක් තිබුණත් ගැටලු විය හැක. 6 GB USB එකක fit වන බව assume නොකර extracted folder එකේ actual size පරීක්ෂා කරන්න.

## Offline / data storage

- Normal chat/code inference එක local 127.0.0.1 endpoint එක භාවිතා කරයි; conversations cloud service එකකට යවන්නේ නැත.
- **NENAPIYUMA Chat** history එක එහි data/chat-history.json තුළ තබයි. USB එකේ app folder එක writable විය යුතුය.
- **NENAPIYUMA Studio AI** history එක %LOCALAPPDATA%/NENAPIYUMA/Studio/data/chat-history.json තුළ තබයි.
- config/appsettings.json මඟින් ModelDirectory, RuntimeDirectory, ports (8080, 8081) සහ context size වෙනස් කළ හැක.
- Current news lookup එක නව native apps වලට සම්බන්ධ කර නැත. Offline model එකෙන් real-time news දන්නා බවට app එක පොරොන්දු නොවේ.

## Studio AI හි ක්‍රියාත්මක තත්ත්වය

- Chat/coding: local GGUF model සහ llama-server.exe හඳුනාගෙන API-compatible runtime එක start වූ විට ක්‍රියා කරයි.
- Thinking mode: වැඩි response budget එකක් සහ deliberate answer prompt එකක් යොදයි; model එකේ නොමැති hidden reasoning feature එකක් සක්‍රිය කරන බව නොකියයි.
- Spike mode: කෙටි response budget/වේගවත් උත්තර සඳහා සකසා ඇත.
- Windows control: default ලෙස OFF. සක්‍රිය කළ පසුවත් allow-list එකේ ඇති Notepad/Calculator launch කිරීම පමණක් confirmation එකෙන් කළ හැක. Arbitrary shell commands, installs, deletion, UAC bypass, private credential access මෙහි implemented නොවේ.
- Image generation සහ audio generation: මේ build එකේ local generation modules සම්බන්ධ කර නැත. UI එක missing capability කියා දන්වයි; fake image/audio file එකක් සාදන බව නොකියයි. Text-to-speech සහ music generation එකම capability නොවේ.

## Troubleshooting

- **Model missing** — models folder එකේ .gguf file එකක් තිබේද සහ size එක 10,000,000 bytes ට වැඩිද බලන්න.
- **Runtime missing** — tools/llama-server.exe සහ එහි required DLLs තිබේද බලන්න.
- **Port busy** — config/appsettings.json තුළ Chat/Studio ports වෙනස් කරන්න.
- **AI fails while loading** — llama-server.exe build එක model format එකට ගැළපේද, model download සම්පූර්ණද, RAM ප්‍රමාණවත්ද, DLLs තිබේද කියා බලන්න.
- **SmartScreen warning** — publisher verification නොකර Windows security controls bypass නොකරන්න.
- USB එක ඉවත් කිරීමට පෙර app එක close කර Windows Eject භාවිතා කරන්න.

## Third-party license notes

The app source is maintained separately from third-party model/runtime files. Check the exact model-card/license and exact llama.cpp release/license for files you add or download. Do not redistribute third-party weights unless their license permits it. Generated AI results can be inaccurate; verify important claims independently.
