# Contributing

When writing code, please respect the `.editorconfig` file and fix any warnings before committing. C# formatters all respond to it differently, so don't worry about 100% accuracy. I personally use [JetBrains Rider](https://www.jetbrains.com/rider).

## Building & project structure

To build Bullseye, you will need the [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0). Bullseye is a C# library built with [NativeAOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot), so it outputs as a native `.dll`.

The output file gets put into the TGM4 game directory as `d3d9.dll`, which acts as a proxy DLL (also known as DLL hijacking). When TGM4 starts, it will load our `d3d9.dll` instead of the system library. Bullseye then re-exports `Direct3DCreate9`, which the game calls on startup. When called, Bullseye initializes its core logic and hooks, and then the original function is called to let the game continue.

For building, just run `dotnet publish`. Do *not* use `dotnet build` (or any build button in an IDE), as that won't be compiled to native code. I personally use this PowerShell script to compile and test Bullseye:

```pwsh
$ErrorActionPreference = "Stop"

dotnet publish

$tgm = Get-Process tgm4.exe -ErrorAction Ignore
if ($tgm) {
  Stop-Process -Force $tgm
}

$OutDir = "./Bullseye/bin/Release/net9.0-windows/win-x86/publish"
$TGMDir = "G:/games/steam/steamapps/common/TGM4"
Copy-Item "$OutDir/d3d9.dll" "$TGMDir/d3d9.dll"
Copy-Item "$OutDir/d3d9.pdb" "$TGMDir/d3d9.pdb"

explorer steam://run/3328480
```

## Reverse engineering

TGM4 is a 32-bit Windows executable that uses DirectX 9 (yes, you read that right, in 2025). It's packed with SteamStub, so you can use a tool like [Steamless](https://github.com/atom0s/Steamless) to unpack it for reverse engineering (just remember to re-run Steamless every game update).

The game calls `SteamAPI_RestartAppIfNecessary`, so starting the game through a debugger will have it instantly exit. You can prevent this by creating `steam_appid.txt` in the game directory with the contents `3328480`.
