# Core self-tests

Run these shared-service tests with:

    dotnet run --project tests/NENAPIYUMA.SelfTests/NENAPIYUMA.SelfTests.csproj -c Release

The test harness checks routing, model/runtime discovery, incomplete model rejection, chat storage/search/backup, settings persistence, and the Windows command allow-list. A passing self-test suite does not itself prove a real GGUF inference run; that must be checked on a Windows PC with a compatible model and runtime.
