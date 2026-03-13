# Troubleshooting

This document contains solutions to common issues encountered during development.

## Build Issues

### Build Hangs During CoreCompile

**Symptoms:**
- `dotnet build` appears to hang indefinitely during the `CoreCompile` step
- Build eventually times out or must be manually cancelled
- The issue appears suddenly even though it worked before
- Other projects build successfully, but specific projects (like `KicktippIntegration.Tests`) hang

**Root Cause:**
The VBCSCompiler (Roslyn compiler server) process can occasionally become stuck or unresponsive, causing compilation to hang while waiting for a response from the compiler server.

**Solution:**
Kill the stuck VBCSCompiler process and rebuild:

```powershell
Stop-Process -Name "VBCSCompiler" -Force
dotnet build <project-path>
```

Or in one command:

```powershell
Stop-Process -Name "VBCSCompiler" -Force -ErrorAction SilentlyContinue; dotnet build tests\KicktippIntegration.Tests
```

**Prevention:**
If this issue occurs frequently, you can disable the shared compiler server (though builds will be slower):

1. Add to build command: `-p:UseSharedCompilation=false`
2. Or add to `Directory.Build.props`:
   ```xml
   <PropertyGroup>
     <UseSharedCompilation>false</UseSharedCompilation>
   </PropertyGroup>
   ```

**Diagnosis:**
To check if VBCSCompiler is the issue, list running compiler processes:

```powershell
Get-Process -Name "*dotnet*","*VBCSCompiler*","*csc*" -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, CPU, StartTime
```

If you see a VBCSCompiler process consuming CPU or running for an unusually long time, it's likely stuck.
