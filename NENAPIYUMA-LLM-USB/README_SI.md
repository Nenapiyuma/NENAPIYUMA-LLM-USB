# NENAPIYUMA LLM — USB Offline Edition

**මේක Windows සඳහා source project එකක්.** LLM model එක සහ `llama-server.exe` binary එක ZIP එක ඇතුළේ නැහැ. ඒවා විශාල files සහ වෙනම license/versions ඇති නිසා `scripts\Prepare-OfflineBundle.ps1` script එකෙන් internet ඇති Windows PC එකක එක් වරක් ලබාගන්න. ඉන්පසු සකස් කළ USB folder එක internet නැතිව භාවිතා කරන්න පුළුවන්.

## ඉක්මන් ආරම්භය

1. ZIP එක extract කරන්න.
2. Windows 10/11 PC එකක PowerShell විවෘත කර project folder එකට යන්න.
3. එක් වරක් internet තියෙන විට:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\Prepare-OfflineBundle.ps1 -Profile RAM4GB
   ```
   8GB RAM PC එකකට:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\Prepare-OfflineBundle.ps1 -Profile RAM8GB
   ```
4. Script එකෙන් `tools\llama-server.exe` සහ තෝරාගත් GGUF model එක `models\` තුළට ලබාගනී.
5. සම්පූර්ණ `NENAPIYUMA-LLM-USB` folder එක USB drive එකට copy කරන්න.
6. USB එකෙන් `START_NENAPIYUMA.bat` double-click කරන්න.
7. App එක තුළ **Start AI** ඔබා model එක load වනතුරු ඉන්න.

## RAM profile

- **4GB RAM:** Qwen2.5 0.5B Instruct, Q4_K_M quantization. කුඩා හා සීමිත හැකියාවක් ඇති model එකක්.
- **8GB RAM:** Qwen2.5 1.5B Instruct, Q4_K_M quantization. වැඩි memory අවශ්‍යයි; වෙනත් apps වසා තිබීම හොඳයි.
- App එක Windows RAM හඳුනාගෙන profile එක යෝජනා කරයි. මේවා සහතිකයක් නොවේ. Available RAM, Windows, context size සහ CPU අනුව වේගය වෙනස් වේ.
- 4GB PC එකක 1.5B model එක බලෙන් ධාවනය නොකරන්න. Out-of-memory හෝ ඉතා මන්දගාමී විය හැක.

## Offline/privacy

- AI inference එක `127.0.0.1` localhost මත පමණයි.
- App එක cloud API, sign-in, telemetry හෝ remote chatbot API භාවිතා කරන්නේ නැහැ.
- Preparation script එක පමණක් internet අවශ්‍ය කරයි. Bundle සකස් කළ පසු AI භාවිතයට internet අවශ්‍ය නැහැ.
- Chat history `data\chats.json` තුළ USB එකේ save වේ.
- USB එක ඉවත් කිරීමට පෙර app එක close කර AI server එක නවතා, Windows eject භාවිතා කරන්න.

## Model licenses / limits

Qwen model files third-party model files වන අතර ඒවායේ license සහ usage terms model source page එකෙන් කියවන්න. `llama.cpp` වෙනම open-source project එකකි. මෙම project එක ඒවායේ නිල/අනුමත නිෂ්පාදනයක් නොවේ. Model එකේ පිළිතුරු වැරදි විය හැක; වැදගත් තීරණ සඳහා පරීක්ෂා කරන්න.

## Troubleshooting

- `llama-server.exe not found`: Preparation script එක online PC එකක ධාවනය කර `tools` folder එක USB එකට copy කරන්න.
- `No GGUF model found`: `models` folder එක තුළ `.gguf` file එකක් තිබේද බලන්න.
- AI start වෙන්නේ නැත්නම්: RAM අඩු විය හැක; profile එකට ගැළපෙන model එක තෝරන්න.
- Windows SmartScreen warning එකක් ආවොත් file එකේ source/license තහවුරු කර පසුව පමණක් ඉදිරියට යන්න. Unknown binaries ධාවනය නොකරන්න.

## Project layout

- `app\main.py` — desktop UI, local inference requests, chat history
- `scripts\Prepare-OfflineBundle.ps1` — runtime/model setup (internet අවශ්‍ය)
- `START_NENAPIYUMA.bat` — app launch
- `models\` — GGUF files
- `tools\` — llama.cpp runtime files
- `data\` — local chat history
