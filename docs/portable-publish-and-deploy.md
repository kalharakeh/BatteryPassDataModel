# Portable Publish and Deploy (Windows, no .NET install needed)

This app can be published as a self-contained Windows package.

## Publish profile created

- Publish profile: `web/Properties/PublishProfiles/PortableWinX64.pubxml`
- Output folder: `artifacts/publish/portable-win-x64`

## One-command publish script created

- Script: `scripts/publish-portable-win-x64.ps1`

Run from repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-portable-win-x64.ps1
```

Optional (skip zip):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-portable-win-x64.ps1 -SkipZip
```

## What the script produces

In `artifacts/publish/portable-win-x64`:

- `BatteryPassWeb.exe` (self-contained runtime included)
- web/static content and config files
- `.env.local.example`
- `run-batterypass.cmd`
- `README-PORTABLE.txt`

Also in `artifacts/releases`:

- `BatteryPassWeb-portable-win-x64-<timestamp>.zip`

## Deploy to another PC

1. Copy the generated `.zip` to the target PC.
2. Extract it.
3. Copy `.env.local.example` to `.env.local`.
4. Fill required values in `.env.local`:
   - `MONGODB_URI`
   - `MONGODB_DB`
   - `SESSION_SECRET`
   - `EXTERNAL_API_ENCRYPTION_KEY`
5. Run `run-batterypass.cmd`.
6. Open `http://localhost:5186`.

## Notes

- No .NET runtime installation is required on the target PC.
- MongoDB must still be reachable by the app (local or remote URI).
