# llama.cpp runtime

Place a compatible Windows x64 llama.cpp build here, including llama-server.exe and every DLL required by that exact runtime build.

The apps bind the server to 127.0.0.1 only. Default ports are 8080 for NENAPIYUMA Chat and 8081 for Studio AI. If a port is already in use, change it in config/appsettings.json and restart the app.

Get the runtime from official llama.cpp releases and inspect the release/source and license. The app does not download executables. It calls the local OpenAI-compatible endpoint /v1/chat/completions.
