# NENAPIYUMA LLM — USB Offline Edition

**Windows 10/11 x64 සඳහා portable local-AI app එක.** සම්පූර්ණ USB bundle එක සකස් කළ පසු, සහාය දක්වන Windows PC එකක Python install නොකර app එක ධාවනය කළ හැක. Model එක සහ llama.cpp runtime එක විශාල third-party files වන නිසා source repository එකට commit කර නැත.

## ලේසිම ක්‍රමය — Complete USB Bundle download කිරීම

1. මේ repository එකේ **Actions** tab එක විවෘත කරන්න: https://github.com/Nenapiyuma/NENAPIYUMA-LLM-USB/actions
2. **Build Complete NENAPIYUMA USB Bundle** workflow එක තෝරන්න.
3. **Run workflow** ඔබා profile එක තෝරන්න:
   - `RAM4GB` — කුඩා model එක; අඩු RAM PC සඳහා වඩා සුදුසුයි.
   - `RAM8GB` — විශාල model එක; වැඩි memory අවශ්‍යයි.
4. Workflow එක සාර්ථකව අවසන් වූ පසු එම run එකේ **Artifacts** කොටසෙන් `NENAPIYUMA-USB-Bundle-RAM4GB` හෝ `NENAPIYUMA-USB-Bundle-RAM8GB` download කරන්න.
5. Artifact ZIP එක extract කර, ඇතුළත ඇති `NENAPIYUMA-LLM-USB` folder එක USB drive එකට copy කරන්න.
6. USB එකේ `START_NENAPIYUMA.bat` හෝ `NENAPIYUMA.exe` open කරන්න. App එකේ **Start AI** ඔබන්න.

**සටහන:** Workflow එක model/runtime download කර Windows EXE build කරන නිසා විශාල download එකක් සහ මිනිත්තු කිහිපයක් ගත විය හැක. GitHub Actions artifact එක දින 14කට පසු expire වේ; ලබාගත් ZIP එක ඔබේ PC/USB එකේ තබාගන්න. USB එක සකස් වූ පසු සාමාන්‍ය භාවිතයට internet අවශ්‍ය නොවේ.

## විකල්පය — Windows PC එකකින් අතින් සකස් කිරීම

Source project එක extract කර PowerShell තුළ project folder එකට යන්න. Internet ඇති Windows PC එකක:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Prepare-OfflineBundle.ps1 -Profile RAM4GB
powershell -ExecutionPolicy Bypass -File .\scripts\Check-OfflineBundle.ps1
```

8GB RAM profile එක සඳහා `RAM4GB` වෙනුවට `RAM8GB` යොදන්න. මේ manual source method එකේ Python 3.10+ අවශ්‍යයි; standalone EXE අවශ්‍ය නම් Actions workflow එකෙන් ලබාගන්න.

## RAM profiles

- **RAM4GB:** Qwen2.5 0.5B Instruct, Q4_K_M GGUF. කුඩා හා සීමිත හැකියාවක් ඇති model එකක්.
- **RAM8GB:** Qwen2.5 1.5B Instruct, Q4_K_M GGUF. වැඩි memory අවශ්‍යයි.
- Model එකේ ගුණාත්මකභාවය, වේගය සහ run වීම available RAM, CPU, context size සහ Windows background usage මත රඳා පවතී. 4GB PC එකක විශාල model බලෙන් ධාවනය නොකරන්න.

## Offline/privacy

- Inference එක `127.0.0.1` localhost මත පමණයි.
- App එක cloud API, sign-in හෝ telemetry භාවිතා කිරීමට සැලසුම් කර නැත.
- Preparation/build සඳහා internet අවශ්‍යයි; bundle එක සකස් වූ පසු chat කිරීම offline විය යුතුය.
- Chat history `data\chats.json` තුළ USB drive එකේ save වේ.
- USB එක ඉවත් කිරීමට පෙර app එක close කර, Windows eject භාවිතා කරන්න.

## Third-party files / license

Qwen GGUF model files සහ llama.cpp runtime වෙනත් ව්‍යාපෘතිවලින් ලබාගනී. ඒවායේ අදාළ license සහ usage terms ඒවායේ නිල source pages වලින් පරීක්ෂා කරන්න. NENAPIYUMA ඒවායේ නිල හෝ අනුමත නිෂ්පාදනයක් නොවේ. AI පිළිතුරු වැරදි විය හැක; වැදගත් තීරණ සඳහා වෙනම තහවුරු කරන්න.

## Troubleshooting

- **Model නැහැ:** `models` folder එකේ `.gguf` file එකක් තිබේද බලන්න.
- **Runtime නැහැ:** `tools\llama-server.exe` තිබේද බලන්න; complete bundle එක නැවත ලබාගන්න හෝ preparation script එක ධාවනය කරන්න.
- **AI start වෙන්නේ නැහැ:** RAM අඩු විය හැක, හෝ runtime එකේ අවශ්‍ය DLL එකක් අඩු විය හැක. `scripts\Check-OfflineBundle.ps1` ධාවනය කර error එක පරීක්ෂා කරන්න.
- Windows SmartScreen warning එකක් ආවොත් source එක තහවුරු නොකර bypass නොකරන්න.

## Project layout

- `app\main.py` — Tkinter desktop UI සහ local inference
- `NENAPIYUMA.exe` — workflow එකෙන් build කරන standalone app
- `START_NENAPIYUMA.bat` — EXE launcher; source fallback එකද ඇත
- `scripts\Prepare-OfflineBundle.ps1` — runtime/model download
- `scripts\Check-OfflineBundle.ps1` — bundle checks
- `models\` — GGUF model file(s)
- `tools\` — llama.cpp runtime සහ එයට අවශ්‍ය files
- `data\` — local chat history
