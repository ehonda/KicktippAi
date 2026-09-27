using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

internal sealed record GitHubArtifactToolInvocation(string ArtifactName, string ContentDirectory, int CompressionLevel, int RetentionDays);
internal interface IGitHubArtifactTool { Task ExecuteUploadAsync(GitHubArtifactToolInvocation invocation, CancellationToken cancellationToken = default); }

// Only admission can construct this value. Native launch never accepts raw ProcessStartInfo.
internal sealed class ValidatedArtifactLaunch
{
    internal string Workspace { get; }
    internal string Executable { get; }
    internal IReadOnlyList<string> Arguments { get; }
    internal IReadOnlyList<string> Environment { get; }
    private readonly GitHubArtifactToolInvocation _invocation;
    private readonly ArtifactBridgeBudget? _budget;
    private ValidatedArtifactLaunch(string workspace, string executable, string[] arguments, string[] environment, GitHubArtifactToolInvocation invocation, ArtifactBridgeBudget? budget)
    {
        Workspace = workspace; Executable = executable;
        Arguments = Array.AsReadOnly((string[])arguments.Clone()); Environment = Array.AsReadOnly((string[])environment.Clone()); _invocation = invocation; _budget = budget;
    }
    internal static ValidatedArtifactLaunch Admit(string workspace, GitHubArtifactToolInvocation invocation, ArtifactBridgeBudget? budget = null)
    {
        workspace = ArtifactLaunchAdmission.Absolute(workspace, "GITHUB_ARTIFACT_WORKSPACE_INVALID");
        invocation = invocation with { ContentDirectory = ArtifactLaunchAdmission.Absolute(invocation.ContentDirectory, "GITHUB_ARTIFACT_SCRATCH_INVALID") };
        ArtifactLaunchAdmission.Validate(workspace, invocation);
        var executable = ArtifactLaunchAdmission.ResolveNode();
        var helper = Path.Combine(workspace, ".github", "scripts", "context-source-artifact", "context-source-artifact.mjs");
        return new(workspace, executable, [executable, helper, "upload", "--name", invocation.ArtifactName, "--content", invocation.ContentDirectory, "--compression-level", "0", "--retention-days", "7"],
            System.Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().Select(x => $"{x.Key}={x.Value}").Order(StringComparer.OrdinalIgnoreCase).ToArray(), invocation, budget);
    }
    internal void Revalidate() => ArtifactLaunchAdmission.Validate(Workspace, _invocation);
    internal CancellationTokenSource CleanupDeadline() => _budget?.CleanupDeadline() ?? new(TimeSpan.FromSeconds(3));
}

internal static class ArtifactLaunchAdmission
{
    internal static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    internal static string Absolute(string path, string error)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path) || (OperatingSystem.IsWindows() && (path.StartsWith("\\\\", StringComparison.Ordinal) || path.Length < 3 || path[1] != ':'))) throw new IOException();
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch { throw new IOException(error); }
    }
    internal static void DirectoryChain(string path, string error)
    {
        try
        {
            var absolute = Absolute(path, error);
            var chain = new Stack<DirectoryInfo>();
            for (var directory = new DirectoryInfo(absolute); directory is not null; directory = directory.Parent) chain.Push(directory);
            while (chain.TryPop(out var directory))
            {
                directory.Refresh();
                if (!directory.Exists || (directory.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory || directory.LinkTarget is not null) throw new IOException();
            }
        }
        catch { throw new IOException(error); }
    }
    internal static void RegularFile(string path, string error)
    {
        try
        {
            var file = new FileInfo(path); file.Refresh();
            if (!file.Exists || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 || file.LinkTarget is not null) throw new IOException();
            if (OperatingSystem.IsLinux())
            {
                if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || FileKindNative.gnu_get_libc_version() == IntPtr.Zero ||
                    FileKindNative.statx(-100 /* AT_FDCWD */, path, 0x100 /* AT_SYMLINK_NOFOLLOW */, 1 /* STATX_TYPE */, out var stat) != 0 ||
                    (stat.Mask & 1) == 0 || (stat.Mode & 0xf000 /* S_IFMT */) != 0x8000 /* S_IFREG */) throw new IOException();
            }
            else if (!OperatingSystem.IsWindows()) throw new IOException();
        }
        catch { throw new IOException(error); }
    }
    internal static void Scratch(string content)
    {
        const string error = "GITHUB_ARTIFACT_SCRATCH_INVALID";
        var absolute = Absolute(content, error);
        var temp = Absolute(Path.GetTempPath(), error);
        var token = Path.GetFileName(Path.GetDirectoryName(absolute));
        if (token is null || token.Length != 32 || token.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            !string.Equals(absolute, Path.Combine(temp, "kicktippai-github-artifact", token, "content"), PathComparison)) throw new IOException(error);
        DirectoryChain(absolute, error);
    }
    internal static void Validate(string workspace, GitHubArtifactToolInvocation invocation)
    {
        if (invocation.CompressionLevel != 0 || invocation.RetentionDays != 7) throw new IOException("GITHUB_ARTIFACT_PATH_INVALID");
        DirectoryChain(workspace, "GITHUB_ARTIFACT_WORKSPACE_INVALID");
        var directory = Path.Combine(workspace, ".github", "scripts", "context-source-artifact");
        DirectoryChain(directory, "GITHUB_ARTIFACT_HELPER_INVALID");
        RegularFile(Path.Combine(directory, "context-source-artifact.mjs"), "GITHUB_ARTIFACT_HELPER_INVALID");
        Scratch(invocation.ContentDirectory);
    }
    internal static string ResolveNode()
    {
        try
        {
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (!Path.IsPathFullyQualified(directory)) continue;
                var candidate = Path.GetFullPath(Path.Combine(directory, OperatingSystem.IsWindows() ? "node.exe" : "node"));
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch { }
        throw new IOException("GITHUB_ARTIFACT_HELPER_START_FAILED");
    }
    private static class FileKindNative
    {
        // Linux UAPI linux/stat.h struct statx: fixed 256-byte ABI, mask@0, mode@28.
        // glibc exposes this fixed-width layout independently of legacy struct stat.
        [StructLayout(LayoutKind.Explicit, Size = 256)] internal struct Statx
        { [FieldOffset(0)] internal uint Mask; [FieldOffset(28)] internal ushort Mode; }
        [DllImport("libc")] internal static extern IntPtr gnu_get_libc_version();
        [DllImport("libc", SetLastError = true)] internal static extern int statx(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, out Statx stat);
    }
}

internal interface IArtifactProcessScope : IDisposable
{
    int ProcessId { get; }
    Task<int> DirectExit { get; }
    Stream StandardOutput { get; }
    Stream StandardError { get; }
    void TerminateScope();
    Task<bool> ConfirmTerminatedAsync(CancellationToken cleanupToken);
    Task<bool> ReleaseAsync(CancellationToken cleanupToken);
    Task<bool> SettleObserverAsync(CancellationToken cleanupToken);
}

internal sealed class ArtifactScopeCleanupException : IOException { }

internal sealed class ArtifactProcessScopeLauncher
{
    // Observation runs while identity is retained, before Windows resume. It never owns reaping.
    internal Action<int>? ObserveIdentity { get; init; }
    internal Action<string>? ObserveTransition { get; init; }
    internal Func<string, CancellationToken, ValueTask>? BeforeLinuxPipeLock { get; init; }
    internal Action<string>? ObserveLinuxPipeClose { get; init; }
    internal Action<int>? ObserveWindowsSuspendedChild { get; init; }
    internal Action<int>? ObserveLinuxSpawnedChild { get; init; }
    internal Func<string, int?>? LinuxWaitErrorForTests { get; init; }
    internal Action<int, SafeFileHandle, SafeFileHandle>? ObserveWindowsAssignment { get; init; }
    internal Func<bool>? WindowsAssignmentGateForTests { get; init; }
    internal IArtifactProcessScope Start(ValidatedArtifactLaunch launch, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); launch.Revalidate(); token.ThrowIfCancellationRequested();
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64) throw new IOException();
        if (OperatingSystem.IsWindows()) return WindowsArtifactProcessScope.Start(launch, token, ObserveIdentity, ObserveTransition,
            ObserveWindowsSuspendedChild, ObserveWindowsAssignment, WindowsAssignmentGateForTests);
        if (OperatingSystem.IsLinux()) return LinuxArtifactProcessScope.Start(launch, token, ObserveIdentity, ObserveTransition, BeforeLinuxPipeLock, ObserveLinuxPipeClose,
            ObserveLinuxSpawnedChild, LinuxWaitErrorForTests);
        throw new IOException();
    }
}

internal sealed class LinuxArtifactProcessScope : IArtifactProcessScope
{
    private readonly CancellationTokenSource _observerStop = new();
    private readonly object _waitLock = new();
    private readonly Action<string>? _transition;
    private readonly Func<string, int?>? _waitErrorForTests;
    private enum IdentityState { Retained, Released, Lost }
    private IdentityState _identity;
    public int ProcessId { get; }
    public Task<int> DirectExit { get; }
    public Stream StandardOutput { get; }
    public Stream StandardError { get; }
    private LinuxArtifactProcessScope(int pid, Stream stdout, Stream stderr, Action<string>? transition, Func<string, int?>? waitErrorForTests)
    { ProcessId = pid; StandardOutput = stdout; StandardError = stderr; _transition = transition; _waitErrorForTests = waitErrorForTests; DirectExit = ObserveExit(); }

    internal static IArtifactProcessScope Start(ValidatedArtifactLaunch launch, CancellationToken token, Action<int>? identity, Action<string>? transition,
        Func<string, CancellationToken, ValueTask>? beforePipeLock, Action<string>? pipeClosed, Action<int>? spawnedChild,
        Func<string, int?>? waitErrorForTests)
    {
        // Linux glibc x86-64 ABI: spawn.h attributes=336, actions=80; siginfo_t=128.
        // Opaque storage is typed and sized for this ABI; native init/destroy owns its contents.
        if (Native.gnu_get_libc_version() == IntPtr.Zero) throw new IOException();
        var attributes = new Native.SpawnAttributes(); var actions = new Native.SpawnActions();
        var attributesReady = false; var actionsReady = false;
        var descriptors = new HashSet<int>(); LinuxArtifactProcessScope? scope = null; var pid = 0;
        try
        {
            Native.Check(Native.posix_spawnattr_init(ref attributes)); attributesReady = true;
            Native.Check(Native.posix_spawn_file_actions_init(ref actions)); actionsReady = true;
            Native.Check(Native.posix_spawnattr_setpgroup(ref attributes, 0));
            var mask = new Native.SignalSet(); Native.Check(Native.sigemptyset(ref mask));
            Native.Check(Native.posix_spawnattr_setsigmask(ref attributes, ref mask));
            var defaults = new Native.SignalSet(); Native.Check(Native.sigfillset(ref defaults));
            Native.Check(Native.posix_spawnattr_setsigdefault(ref attributes, ref defaults));
            Native.Check(Native.posix_spawnattr_setflags(ref attributes, 0x02 | 0x04 | 0x08));
            int Relocate(int fd)
            {
                if (fd < 0) throw new IOException(); descriptors.Add(fd);
                if (fd > 2) return fd;
                var moved = Native.fcntl(fd, 1030 /* F_DUPFD_CLOEXEC */, 3);
                if (moved < 0) throw new IOException(); descriptors.Add(moved);
                Close(fd); return moved;
            }
            void Close(int fd) { if (descriptors.Remove(fd) && Native.close(fd) != 0) throw new IOException(); }
            int[] Pipe()
            {
                var ends = new int[2]; Native.Check(Native.pipe2(ends, 0x80000));
                // Register both before any fallible relocation.
                descriptors.Add(ends[0]); descriptors.Add(ends[1]);
                return [Relocate(ends[0]), Relocate(ends[1])];
            }
            var stdout = Pipe(); var stderr = Pipe(); var stdin = Relocate(Native.open("/dev/null", 0x80000));
            Native.Check(Native.posix_spawn_file_actions_addchdir_np(ref actions, launch.Workspace));
            Native.Check(Native.posix_spawn_file_actions_adddup2(ref actions, stdin, 0));
            Native.Check(Native.posix_spawn_file_actions_adddup2(ref actions, stdout[1], 1));
            Native.Check(Native.posix_spawn_file_actions_adddup2(ref actions, stderr[1], 2));
            foreach (var fd in descriptors) Native.Check(Native.posix_spawn_file_actions_addclose(ref actions, fd));
            using var argv = new NativeStrings(launch.Arguments); using var environment = new NativeStrings(launch.Environment);
            launch.Revalidate(); token.ThrowIfCancellationRequested();
            Native.Check(Native.posix_spawn(out pid, launch.Executable, ref actions, ref attributes, argv.Pointer, environment.Pointer));
            spawnedChild?.Invoke(pid);
            // Parent writer copies MUST be closed before scope publication or any drain starts.
            Close(stdout[1]); Close(stderr[1]); Close(stdin);
            transition?.Invoke("parent-child-endpoints-closed");
            var output = new LinuxPipeStream(stdout[0], "stdout", beforePipeLock, pipeClosed); descriptors.Remove(stdout[0]);
            Stream? error = null;
            try
            {
                transition?.Invoke("stdout-transferred");
                error = new LinuxPipeStream(stderr[0], "stderr", beforePipeLock, pipeClosed); descriptors.Remove(stderr[0]);
                transition?.Invoke("stderr-transferred");
                scope = new(pid, output, error, transition, waitErrorForTests);
            }
            catch { output.Dispose(); error?.Dispose(); throw; }
            identity?.Invoke(pid); transition?.Invoke("scope-contained"); token.ThrowIfCancellationRequested(); transition?.Invoke("before-publication");
            return scope;
        }
        catch
        {
            if (pid != 0)
            {
                scope ??= new(pid, Stream.Null, Stream.Null, transition, waitErrorForTests);
                using var cleanup = launch.CleanupDeadline();
                var confirmed = false; var released = false; var observed = false;
                try { scope.TerminateScope(); } catch { }
                try { confirmed = scope.ConfirmTerminatedAsync(cleanup.Token).GetAwaiter().GetResult(); } catch { }
                try { if (confirmed) released = scope.ReleaseAsync(cleanup.Token).GetAwaiter().GetResult(); } catch { }
                try { observed = scope.SettleObserverAsync(cleanup.Token).GetAwaiter().GetResult(); } catch { }
                finally { scope.Dispose(); }
                if (!confirmed || !released || !observed) throw new ArtifactScopeCleanupException();
            }
            throw;
        }
        finally
        {
            foreach (var fd in descriptors) Native.close(fd);
            if (actionsReady) Native.posix_spawn_file_actions_destroy(ref actions);
            if (attributesReady) Native.posix_spawnattr_destroy(ref attributes);
        }
    }
    private async Task<int> ObserveExit()
    {
        while (true)
        {
            _observerStop.Token.ThrowIfCancellationRequested();
            lock (_waitLock)
            {
                if (_identity != IdentityState.Retained) throw new IOException();
                if (TryObserveExit(out var code, "waitid-observer")) return code;
            }
            await Task.Delay(10, _observerStop.Token);
        }
    }
    // All calls hold _waitLock. EINTR leaves retained ownership intact and retries
    // at the caller's cancellable poll. ECHILD irrevocably forbids further PGID use.
    private bool TryObserveExit(out int code, string phase)
    {
        code = 0; var info = new Native.SignalInfo();
        var injectedError = _waitErrorForTests?.Invoke(phase);
        if ((injectedError.HasValue ? -1 : Native.waitid(1, (uint)ProcessId, ref info, 4 | 1 | 0x01000000)) != 0)
        {
            var error = injectedError ?? Marshal.GetLastPInvokeError();
            if (error == 4) return false;
            if (error == 10) { _identity = IdentityState.Lost; _transition?.Invoke("waitid-echild-lost"); }
            throw new IOException();
        }
        if (info.Pid == 0) return false;
        if (info.Pid != ProcessId) throw new IOException();
        _transition?.Invoke("waitid-wnowait-retained");
        code = info.Code == 1 ? info.Status : 128 + info.Status; return true;
    }
    public void TerminateScope()
    {
        lock (_waitLock)
        {
            if (_identity == IdentityState.Released) return;
            if (_identity == IdentityState.Lost) throw new IOException();
            // Check retained child ownership even if the background observer faulted.
            _ = TryObserveExit(out _, "waitid-terminate");
            if (Native.kill(-ProcessId, 9) != 0 && Marshal.GetLastPInvokeError() != 3) throw new IOException();
        }
    }
    public async Task<bool> ConfirmTerminatedAsync(CancellationToken cleanupToken)
    {
        try
        {
            var emptyScans = 0;
            while (emptyScans < 2)
            {
                cleanupToken.ThrowIfCancellationRequested(); var live = false;
                foreach (var directory in Directory.EnumerateDirectories("/proc"))
                {
                    cleanupToken.ThrowIfCancellationRequested();
                    if (!int.TryParse(Path.GetFileName(directory), out _)) continue;
                    string stat;
                    try { stat = File.ReadAllText(Path.Combine(directory, "stat")); }
                    catch (FileNotFoundException) { continue; }
                    catch (DirectoryNotFoundException) { continue; }
                    var close = stat.LastIndexOf(')'); if (close < 0 || close + 2 >= stat.Length) return false;
                    var fields = stat[(close + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length < 3 || !int.TryParse(fields[2], out var group)) return false;
                    if (group == ProcessId && fields[0] is not ("Z" or "X")) live = true;
                }
                if (live) { emptyScans = 0; TerminateScope(); } else emptyScans++;
                if (emptyScans < 2) await Task.Delay(10, cleanupToken);
            }
            while (true)
            {
                cleanupToken.ThrowIfCancellationRequested();
                lock (_waitLock)
                {
                    if (_identity != IdentityState.Retained) return false;
                    if (TryObserveExit(out _, "waitid-confirm")) return true;
                }
                await Task.Delay(10, cleanupToken);
            }
        }
        catch { return false; }
    }
    public async Task<bool> ReleaseAsync(CancellationToken cleanupToken)
    {
        try
        {
            // Settle the WNOWAIT observer before the sole consuming wait. A faulted
            // observer does not preclude reaping a still-retained, killed child.
            if (!await SettleObserverAsync(cleanupToken)) return false;
            while (true)
            {
                cleanupToken.ThrowIfCancellationRequested();
                lock (_waitLock)
                {
                    if (_identity == IdentityState.Released) return true;
                    if (_identity == IdentityState.Lost) return false;
                    _transition?.Invoke("before-final-reap");
                    var injectedError = _waitErrorForTests?.Invoke("waitpid-release");
                    var result = injectedError.HasValue ? -1 : Native.waitpid(ProcessId, out _, 1);
                    if (result == ProcessId) { _identity = IdentityState.Released; _transition?.Invoke("waitpid-reaped"); return true; }
                    if (result < 0)
                    {
                        var error = injectedError ?? Marshal.GetLastPInvokeError();
                        if (error == 10) _identity = IdentityState.Lost;
                        if (error != 4) return false;
                    }
                }
                await Task.Delay(10, cleanupToken);
            }
        }
        catch { return false; }
    }
    public async Task<bool> SettleObserverAsync(CancellationToken cleanupToken)
    {
        _observerStop.Cancel();
        try { await DirectExit.WaitAsync(cleanupToken); } catch { }
        return DirectExit.IsCompleted;
    }
    public void Dispose()
    {
        try { TerminateScope(); } catch { }
        _observerStop.Cancel(); StandardOutput.Dispose(); StandardError.Dispose();
        // Normal paths explicitly settle first. Fallback disposal retains the CTS
        // until the actual observer completes rather than disposing beneath it.
        _ = DirectExit.ContinueWith(t => { _ = t.Exception; _observerStop.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed class LinuxPipeStream : Stream
    {
        private readonly object _gate = new(); private int _fd;
        private readonly string _name;
        private readonly Func<string, CancellationToken, ValueTask>? _beforeLock;
        private readonly Action<string>? _closed;
        internal LinuxPipeStream(int fd, string name, Func<string, CancellationToken, ValueTask>? beforeLock, Action<string>? closed)
        {
            var flags = Native.fcntl(fd, 3, 0);
            if (flags < 0 || Native.fcntl(fd, 4, flags | 0x800) < 0) throw new IOException();
            _fd = fd; _name = name; _beforeLock = beforeLock; _closed = closed;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return 0;
            // The production drain supplies its sole array. Pin only during read,
            // including the supplied segment offset; no second pipe buffer exists.
            if (!MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment) || segment.Array is null) throw new IOException();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested(); long count; int error;
                if (_beforeLock is not null) await _beforeLock(_name, cancellationToken);
                lock (_gate)
                {
                    // Cleanup cancels before taking this lock to close the fd.
                    cancellationToken.ThrowIfCancellationRequested();
                    ObjectDisposedException.ThrowIf(_fd < 0, this);
                    var pin = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
                    try { count = Native.read(_fd, IntPtr.Add(pin.AddrOfPinnedObject(), segment.Offset), (nuint)Math.Min(segment.Count, 4096)); error = Marshal.GetLastPInvokeError(); }
                    finally { pin.Free(); }
                }
                if (count >= 0) return checked((int)count);
                if (error == 4) continue;
                if (error != 11) throw new IOException();
                await Task.Delay(10, cancellationToken);
            }
        }
        protected override void Dispose(bool disposing)
        {
            var closed = false;
            lock (_gate) { if (_fd >= 0) { Native.close(_fd); _fd = -1; closed = true; } }
            if (closed) _closed?.Invoke(_name);
            base.Dispose(disposing);
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class NativeStrings : IDisposable
    {
        private readonly List<IntPtr> _strings = []; internal IntPtr Pointer { get; private set; }
        internal NativeStrings(IEnumerable<string> strings)
        {
            try
            {
                foreach (var value in strings) { if (value.Contains('\0')) throw new IOException(); _strings.Add(Marshal.StringToCoTaskMemUTF8(value)); }
                Pointer = Marshal.AllocHGlobal((_strings.Count + 1) * IntPtr.Size);
                for (var i = 0; i < _strings.Count; i++) Marshal.WriteIntPtr(Pointer, i * IntPtr.Size, _strings[i]);
                Marshal.WriteIntPtr(Pointer, _strings.Count * IntPtr.Size, IntPtr.Zero);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var value in _strings) Marshal.FreeCoTaskMem(value); _strings.Clear(); if (Pointer != IntPtr.Zero) Marshal.FreeHGlobal(Pointer); Pointer = IntPtr.Zero; }
    }
    private static class Native
    {
        [StructLayout(LayoutKind.Sequential, Size = 336)] internal struct SpawnAttributes { private long _alignment; }
        [StructLayout(LayoutKind.Sequential, Size = 80)] internal struct SpawnActions { private long _alignment; }
        [StructLayout(LayoutKind.Sequential, Size = 128)] internal struct SignalSet { private long _alignment; }
        [StructLayout(LayoutKind.Explicit, Size = 128)] internal struct SignalInfo { [FieldOffset(8)] internal int Code; [FieldOffset(16)] internal int Pid; [FieldOffset(24)] internal int Status; }
        internal static void Check(int result) { if (result != 0) throw new IOException(); }
        [DllImport("libc")] internal static extern IntPtr gnu_get_libc_version();
        [DllImport("libc")] internal static extern int posix_spawnattr_init(ref SpawnAttributes value);
        [DllImport("libc")] internal static extern int posix_spawnattr_destroy(ref SpawnAttributes value);
        [DllImport("libc")] internal static extern int posix_spawnattr_setpgroup(ref SpawnAttributes value, int group);
        [DllImport("libc")] internal static extern int posix_spawnattr_setflags(ref SpawnAttributes value, short flags);
        [DllImport("libc")] internal static extern int posix_spawnattr_setsigmask(ref SpawnAttributes value, ref SignalSet signals);
        [DllImport("libc")] internal static extern int posix_spawnattr_setsigdefault(ref SpawnAttributes value, ref SignalSet signals);
        [DllImport("libc")] internal static extern int sigemptyset(ref SignalSet signals);
        [DllImport("libc")] internal static extern int sigfillset(ref SignalSet signals);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_init(ref SpawnActions value);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_destroy(ref SpawnActions value);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_addchdir_np(ref SpawnActions value, [MarshalAs(UnmanagedType.LPUTF8Str)] string directory);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_adddup2(ref SpawnActions value, int from, int to);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_addclose(ref SpawnActions value, int fd);
        [DllImport("libc")] internal static extern int posix_spawn(out int pid, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, ref SpawnActions actions, ref SpawnAttributes attributes, IntPtr argv, IntPtr environment);
        [DllImport("libc", SetLastError = true)] internal static extern int pipe2([Out] int[] pipes, int flags);
        [DllImport("libc", SetLastError = true)] internal static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
        [DllImport("libc", SetLastError = true)] internal static extern int fcntl(int fd, int command, int value);
        [DllImport("libc", SetLastError = true)] internal static extern int close(int fd);
        [DllImport("libc", SetLastError = true)] internal static extern long read(int fd, IntPtr buffer, nuint count);
        [DllImport("libc", SetLastError = true)] internal static extern int kill(int pid, int signal);
        [DllImport("libc", SetLastError = true)] internal static extern int waitid(int kind, uint pid, ref SignalInfo info, int options);
        [DllImport("libc", SetLastError = true)] internal static extern int waitpid(int pid, out int status, int options);
    }
}

internal sealed class WindowsArtifactProcessScope : IArtifactProcessScope
{
    private readonly SafeFileHandle _process; private readonly SafeFileHandle _job;
    private readonly CancellationTokenSource _observerStop = new(); private bool _released;
    public int ProcessId { get; }
    public Task<int> DirectExit { get; }
    public Stream StandardOutput { get; }
    public Stream StandardError { get; }
    private WindowsArtifactProcessScope(int pid, SafeFileHandle process, SafeFileHandle job, Stream stdout, Stream stderr)
    { ProcessId = pid; _process = process; _job = job; StandardOutput = stdout; StandardError = stderr; DirectExit = ObserveExit(); }
    internal static IArtifactProcessScope Start(ValidatedArtifactLaunch launch, CancellationToken token, Action<int>? identity, Action<string>? transition,
        Action<int>? suspendedChild, Action<int, SafeFileHandle, SafeFileHandle>? assignedJob, Func<bool>? assignmentGateForTests)
    {
        SafeFileHandle? job = null, process = null, thread = null, outputWriter = null, errorWriter = null, input = null;
        NamedPipeServerStream? output = null, error = null;
        IntPtr attributes = IntPtr.Zero, handles = IntPtr.Zero, environment = IntPtr.Zero; var attributesReady = false;
        WindowsArtifactProcessScope? scope = null; var assigned = false; var rollingBack = false;
        CancellationTokenSource? rollbackDeadline = null;
        void BeginRollback() { rollbackDeadline ??= launch.CleanupDeadline(); }
        try
        {
            job = Native.CreateJobObjectW(IntPtr.Zero, null); Require(!job.IsInvalid);
            var limits = new Native.JobLimits { Flags = 0x2000 };
            Require(Native.SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<Native.JobLimits>()));
            var security = new Native.SecurityAttributes { Length = Marshal.SizeOf<Native.SecurityAttributes>(), Inherit = 1 };
            (NamedPipeServerStream Reader, SafeFileHandle Writer) Pipe()
            {
                var name = "kicktippai-artifact-" + Guid.NewGuid().ToString("N");
                var reader = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
                SafeFileHandle? writer = null; Task? connection = null;
                var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                try
                {
                    writer = Native.CreateFileW("\\\\.\\pipe\\" + name, 0x40000000, 0, ref security, 3, 0, IntPtr.Zero); Require(!writer.IsInvalid);
                    connection = reader.WaitForConnectionAsync(connectCancellation.Token);
                    connection.WaitAsync(TimeSpan.FromSeconds(3), token).GetAwaiter().GetResult();
                    return (reader, writer);
                }
                catch
                {
                    BeginRollback(); connectCancellation.Cancel(); reader.Dispose(); writer?.Dispose();
                    if (connection is not null)
                    {
                        try { connection.WaitAsync(rollbackDeadline!.Token).GetAwaiter().GetResult(); } catch { }
                        if (!connection.IsCompleted) throw new ArtifactScopeCleanupException();
                    }
                    throw;
                }
                finally
                {
                    if (connection is null || connection.IsCompleted) connectCancellation.Dispose();
                    else _ = connection.ContinueWith(t => { _ = t.Exception; connectCancellation.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
            (output, outputWriter) = Pipe(); (error, errorWriter) = Pipe();
            input = Native.CreateFileW("NUL", 0x80000000, 3, ref security, 3, 0, IntPtr.Zero); Require(!input.IsInvalid);
            nuint size = 0; Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            Require(size > 0); attributes = Marshal.AllocHGlobal(checked((int)size));
            Require(Native.InitializeProcThreadAttributeList(attributes, 1, 0, ref size)); attributesReady = true;
            handles = Marshal.AllocHGlobal(3 * IntPtr.Size);
            Marshal.WriteIntPtr(handles, 0, input.DangerousGetHandle()); Marshal.WriteIntPtr(handles, IntPtr.Size, outputWriter.DangerousGetHandle()); Marshal.WriteIntPtr(handles, 2 * IntPtr.Size, errorWriter.DangerousGetHandle());
            Require(Native.UpdateProcThreadAttribute(attributes, 0, (IntPtr)0x20002, handles, (nuint)(3 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero));
            var startup = new Native.StartupInfoEx { Startup = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfoEx>(), Flags = 0x100, Input = input.DangerousGetHandle(), Output = outputWriter.DangerousGetHandle(), Error = errorWriter.DangerousGetHandle() }, Attributes = attributes };
            environment = Marshal.StringToHGlobalUni(string.Join('\0', launch.Environment) + "\0\0");
            launch.Revalidate(); transition?.Invoke("before-create-process"); token.ThrowIfCancellationRequested();
            Require(Native.CreateProcessW(launch.Executable, new StringBuilder(string.Join(' ', launch.Arguments.Select(Quote))), IntPtr.Zero, IntPtr.Zero, true,
                0x00000004 | 0x00000400 | 0x00080000 | 0x08000000, environment, launch.Workspace, ref startup, out var information));
            process = new(information.Process, true); thread = new(information.Thread, true);
            // Close parent child-only handles while the child is still suspended.
            outputWriter.Dispose(); errorWriter.Dispose(); input.Dispose();
            transition?.Invoke("parent-child-endpoints-closed");
            // The retained process is still suspended when a test observes the
            // unassigned rollback branch; production has no extra callback.
            suspendedChild?.Invoke(checked((int)information.ProcessId));
            transition?.Invoke("before-assignment");
            // A test may force the native-failure branch, but cannot make an
            // unassigned process look assigned and reach ResumeThread.
            Require((assignmentGateForTests?.Invoke() ?? true) && Native.AssignProcessToJobObject(job, process));
            assigned = true; transition?.Invoke("scope-contained");
            assignedJob?.Invoke(checked((int)information.ProcessId), process, job);
            identity?.Invoke(checked((int)information.ProcessId));
            launch.Revalidate(); transition?.Invoke("before-resume"); token.ThrowIfCancellationRequested();
            // Prepare the managed owner while suspended, before arbitrary code can
            // create descendants. It owns every subsequently started observer task.
            scope = new(checked((int)information.ProcessId), process, job, output, error);
            process = null; job = null; output = null; error = null;
            Require(Native.ResumeThread(thread) != uint.MaxValue); thread.Dispose(); thread = null;
            transition?.Invoke("after-resume");
            transition?.Invoke("before-publication");
            return scope;
        }
        catch
        {
            rollingBack = true;
            BeginRollback();
            if (scope is not null)
            {
                var confirmed = false; var released = false; var observed = false;
                try { scope.TerminateScope(); } catch { }
                try { confirmed = scope.ConfirmTerminatedAsync(rollbackDeadline!.Token).GetAwaiter().GetResult(); } catch { }
                try { if (confirmed) released = scope.ReleaseAsync(rollbackDeadline!.Token).GetAwaiter().GetResult(); } catch { }
                try { observed = scope.SettleObserverAsync(rollbackDeadline!.Token).GetAwaiter().GetResult(); } catch { }
                finally { scope.Dispose(); }
                if (!confirmed || !released || !observed) throw new ArtifactScopeCleanupException();
            }
            else if (process is not null && !process.IsInvalid)
            {
                // Assignment failure still requires direct suspended-child cleanup.
                _ = Native.TerminateProcess(process, 1);
                if (assigned && job is not null) _ = Native.TerminateJobObject(job, 1);
                var confirmed = false;
                while (!rollbackDeadline!.IsCancellationRequested)
                {
                    var direct = Native.WaitForSingleObject(process, 0);
                    var jobEmpty = !assigned;
                    if (assigned && job is not null)
                    {
                        if (!Native.QueryInformationJobObject(job, 1, out var accounting, (uint)Marshal.SizeOf<Native.JobAccounting>(), IntPtr.Zero)) break;
                        jobEmpty = accounting.ActiveProcesses == 0;
                    }
                    if (direct == 0 && jobEmpty) { confirmed = true; break; }
                    if (direct != 0 && direct != 258) break;
                    Thread.Sleep(10);
                }
                if (!confirmed) throw new ArtifactScopeCleanupException();
            }
            throw;
        }
        finally
        {
            thread?.Dispose(); process?.Dispose(); job?.Dispose(); outputWriter?.Dispose(); errorWriter?.Dispose(); input?.Dispose(); output?.Dispose(); error?.Dispose();
            if (rollingBack) transition?.Invoke("rollback-endpoints-closed");
            if (attributesReady) Native.DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            rollbackDeadline?.Dispose();
        }
    }
    private static string Quote(string value)
    {
        if (value.Contains('\0')) throw new IOException();
        var result = new StringBuilder("\""); var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes); result.Append(character); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    private async Task<int> ObserveExit()
    {
        while (true)
        {
            var wait = Native.WaitForSingleObject(_process, 0);
            if (wait == 0) { Require(Native.GetExitCodeProcess(_process, out var code)); return unchecked((int)code); }
            Require(wait == 258); await Task.Delay(10, _observerStop.Token);
        }
    }
    public void TerminateScope() { if (!_released) Require(Native.TerminateJobObject(_job, 1)); }
    public async Task<bool> ConfirmTerminatedAsync(CancellationToken cleanupToken)
    {
        try
        {
            while (true)
            {
                cleanupToken.ThrowIfCancellationRequested();
                Require(Native.QueryInformationJobObject(_job, 1, out var accounting, (uint)Marshal.SizeOf<Native.JobAccounting>(), IntPtr.Zero));
                if (accounting.ActiveProcesses == 0) { await DirectExit.WaitAsync(cleanupToken); return true; }
                await Task.Delay(10, cleanupToken);
            }
        }
        catch { return false; }
    }
    public async Task<bool> ReleaseAsync(CancellationToken cleanupToken)
    {
        try { await DirectExit.WaitAsync(cleanupToken); _released = true; _process.Dispose(); _job.Dispose(); return true; }
        catch { return false; }
    }
    public async Task<bool> SettleObserverAsync(CancellationToken cleanupToken)
    {
        _observerStop.Cancel();
        try { await DirectExit.WaitAsync(cleanupToken); } catch { }
        return DirectExit.IsCompleted;
    }
    public void Dispose()
    {
        if (!_released) { try { TerminateScope(); } catch { } }
        _observerStop.Cancel(); StandardOutput.Dispose(); StandardError.Dispose(); _process.Dispose(); _job.Dispose();
        _ = DirectExit.ContinueWith(t => { _ = t.Exception; _observerStop.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    private static void Require(bool value) { if (!value) throw new IOException(); }
    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct SecurityAttributes { internal int Length; internal IntPtr Descriptor; internal int Inherit; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct StartupInfo
        {
            internal int Size; internal IntPtr Reserved, Desktop, Title; internal uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
            internal ushort Show, ReservedSize; internal IntPtr ReservedBytes, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct StartupInfoEx { internal StartupInfo Startup; internal IntPtr Attributes; }
        [StructLayout(LayoutKind.Sequential)] internal struct ProcessInformation { internal IntPtr Process, Thread; internal uint ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Explicit, Size = 144)] internal struct JobLimits { [FieldOffset(16)] internal uint Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct JobAccounting { internal long User, Kernel, PeriodUser, PeriodKernel; internal uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref JobLimits limits, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool QueryInformationJobObject(SafeFileHandle job, int kind, out JobAccounting accounting, uint size, IntPtr returned);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern SafeFileHandle CreateFileW(string name, uint access, uint share, ref SecurityAttributes attributes, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] internal static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation process);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeFileHandle process);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint ResumeThread(SafeFileHandle thread);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool TerminateProcess(SafeFileHandle process, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool TerminateJobObject(SafeFileHandle job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetExitCodeProcess(SafeFileHandle process, out uint code);
    }
}

internal sealed class ArtifactBridgeBudget : IDisposable
{
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly CancellationTokenSource _work;
    private TimeSpan? _cleanupCutoff;
    internal ArtifactBridgeBudget(TimeProvider? clock = null)
    { _clock = clock ?? TimeProvider.System; _started = _clock.GetTimestamp(); _work = new(TimeSpan.FromSeconds(117), _clock); }
    internal CancellationToken WorkToken => _work.Token;
    internal bool WorkExpired => _work.IsCancellationRequested || _clock.GetElapsedTime(_started) >= TimeSpan.FromSeconds(117);
    internal CancellationTokenSource CleanupDeadline()
    {
        var now = _clock.GetElapsedTime(_started);
        _cleanupCutoff ??= TimeSpan.FromSeconds(Math.Min(120, now.TotalSeconds + 3));
        var remaining = _cleanupCutoff.Value - now;
        var result = new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : Timeout.InfiniteTimeSpan, _clock);
        if (remaining <= TimeSpan.Zero) result.Cancel();
        return result;
    }
    public void Dispose() => _work.Dispose();
}

[Flags]
internal enum ArtifactBridgeFailure { None = 0, Helper = 1, Start = 2, Io = 4, OutputLimit = 8 }
internal enum ArtifactDrainResult { Eof, Canceled, IoFailure, OutputLimit }

internal sealed class ArtifactPipeDrain
{
    internal const int ByteCap = 1_048_576;
    internal Task<ArtifactDrainResult> Completion { get; }
    internal bool DisposeFailed { get; private set; }
    internal ArtifactPipeDrain(string name, Stream input, CancellationToken token, Func<string, Stream, Stream>? observer,
        Func<StreamReader, CancellationToken, Task<string>>? testReader)
    { Completion = DrainAsync(name, input, token, observer, testReader); }
    private async Task<ArtifactDrainResult> DrainAsync(string name, Stream input, CancellationToken token, Func<string, Stream, Stream>? observer,
        Func<StreamReader, CancellationToken, Task<string>>? testReader)
    {
        Stream? observed = null; var result = ArtifactDrainResult.IoFailure;
        try
        {
            observed = observer?.Invoke(name, input) ?? input;
            if (testReader is not null)
            {
                // Retain the established reader-fault seam, but constrain every real
                // underlying read. Production never constructs or retains text.
                using var bounded = new TestReaderStream(observed, token);
                using var reader = new StreamReader(bounded, Encoding.UTF8, false, 4096, true);
                _ = await testReader(reader, token);
                result = bounded.Overflowed ? ArtifactDrainResult.OutputLimit : ArtifactDrainResult.Eof;
            }
            else
            {
                var buffer = new byte[4096]; var consumed = 0;
                while (true)
                {
                    var count = await observed.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, ByteCap - consumed + 1)), token);
                    if (count == 0) { result = ArtifactDrainResult.Eof; break; }
                    consumed += count;
                    if (consumed > ByteCap) { result = ArtifactDrainResult.OutputLimit; break; }
                }
            }
        }
        catch (OutputLimitException) { result = ArtifactDrainResult.OutputLimit; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { result = ArtifactDrainResult.Canceled; }
        catch { result = ArtifactDrainResult.IoFailure; }
        finally
        {
            try { observed?.Dispose(); }
            catch { DisposeFailed = true; result = ArtifactDrainResult.IoFailure; }
        }
        return result;
    }
    private sealed class OutputLimitException : IOException { }
    private sealed class TestReaderStream(Stream inner, CancellationToken token) : Stream
    {
        private int _consumed;
        internal bool Overflowed => _consumed > ByteCap;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            token.ThrowIfCancellationRequested();
            if (Overflowed) throw new OutputLimitException();
            var count = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, Math.Min(4096, ByteCap - _consumed + 1))], token);
            _consumed += count; if (Overflowed) throw new OutputLimitException(); return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask().GetAwaiter().GetResult();
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

internal sealed class NodeGitHubArtifactTool : IGitHubArtifactTool
{
    private readonly string _workspace;
    private readonly Func<ValidatedArtifactLaunch, CancellationToken, IArtifactProcessScope?> _start = new ArtifactProcessScopeLauncher().Start;
    private readonly CancellationToken _testDeadline;
    private readonly Func<string, Stream, Stream>? _observeOutputStream;
    private readonly Action<string, Task>? _observeOutputTask;
    private readonly Func<StreamReader, CancellationToken, Task<string>>? _readOutput;
    internal NodeGitHubArtifactTool(string workspace, Func<ValidatedArtifactLaunch, CancellationToken, IArtifactProcessScope?>? start = null,
        CancellationToken testDeadline = default, Func<StreamReader, CancellationToken, Task<string>>? readOutput = null,
        Func<string, Stream, Stream>? observeOutputStream = null, Action<string, Task>? observeOutputTask = null)
    {
        _workspace = ArtifactLaunchAdmission.Absolute(workspace, "GITHUB_ARTIFACT_WORKSPACE_INVALID");
        if (start is not null) _start = start;
        _testDeadline = testDeadline;
        _observeOutputStream = observeOutputStream; _observeOutputTask = observeOutputTask;
        if (readOutput is not null) _readOutput = readOutput;
    }
    public NodeGitHubArtifactTool()
    {
        var value = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value)) throw new IOException("GITHUB_ARTIFACT_WORKSPACE_INVALID");
        _workspace = Path.GetFullPath(value);
    }
    public async Task ExecuteUploadAsync(GitHubArtifactToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var budget = new ArtifactBridgeBudget();
        if (_testDeadline.IsCancellationRequested) throw new IOException("GITHUB_ARTIFACT_HELPER_TIMEOUT");
        var launch = ValidatedArtifactLaunch.Admit(_workspace, invocation, budget);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.WorkToken, _testDeadline);
        using var drainCancellation = new CancellationTokenSource();
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationRegistration = linked.Token.Register(() => canceled.TrySetResult());
        IArtifactProcessScope? scope = null; ArtifactPipeDrain? stdout = null, stderr = null;
        var failure = ArtifactBridgeFailure.None; var cleanupSucceeded = true;
        void Collect(ArtifactPipeDrain? drain)
        {
            if (drain?.Completion.IsCompletedSuccessfully != true) return;
            failure |= drain.Completion.Result switch { ArtifactDrainResult.OutputLimit => ArtifactBridgeFailure.OutputLimit, ArtifactDrainResult.IoFailure => ArtifactBridgeFailure.Io, _ => ArtifactBridgeFailure.None };
        }
        try
        {
            try { linked.Token.ThrowIfCancellationRequested(); scope = _start(launch, linked.Token) ?? throw new IOException(); }
            catch (ArtifactScopeCleanupException) { cleanupSucceeded = false; }
            catch { failure |= ArtifactBridgeFailure.Start; }
            if (scope is not null)
            {
                stdout = new("stdout", scope.StandardOutput, drainCancellation.Token, _observeOutputStream, _readOutput);
                stderr = new("stderr", scope.StandardError, drainCancellation.Token, _observeOutputStream, _readOutput);
                foreach (var item in new[] { ("stdout", stdout), ("stderr", stderr) })
                    try { _observeOutputTask?.Invoke(item.Item1, item.Item2.Completion); } catch { failure |= ArtifactBridgeFailure.Io; }
                while (true)
                {
                    Collect(stdout); Collect(stderr);
                    if (scope.DirectExit.IsFaulted || scope.DirectExit.IsCanceled) { _ = scope.DirectExit.Exception; failure |= ArtifactBridgeFailure.Io; }
                    else if (scope.DirectExit.IsCompletedSuccessfully && scope.DirectExit.Result != 0) failure |= ArtifactBridgeFailure.Helper;
                    if (linked.IsCancellationRequested || failure != ArtifactBridgeFailure.None) break;
                    if (scope.DirectExit.IsCompletedSuccessfully && stdout.Completion.IsCompletedSuccessfully && stderr.Completion.IsCompletedSuccessfully)
                    {
                        if (stdout.Completion.Result != ArtifactDrainResult.Eof || stderr.Completion.Result != ArtifactDrainResult.Eof) failure |= ArtifactBridgeFailure.Io;
                        break;
                    }
                    var pending = new List<Task> { canceled.Task };
                    if (!scope.DirectExit.IsCompleted) pending.Add(scope.DirectExit);
                    if (!stdout.Completion.IsCompleted) pending.Add(stdout.Completion);
                    if (!stderr.Completion.IsCompleted) pending.Add(stderr.Completion);
                    await Task.WhenAny(pending);
                }
            }
        }
        catch { failure |= ArtifactBridgeFailure.Io; }
        finally
        {
            if (scope is not null)
            {
                using var cleanup = budget.CleanupDeadline();
                try { scope.TerminateScope(); } catch { cleanupSucceeded = false; }
                drainCancellation.Cancel();
                try { scope.StandardOutput.Dispose(); } catch { cleanupSucceeded = false; }
                try { scope.StandardError.Dispose(); } catch { cleanupSucceeded = false; }
                // Termination confirmation proceeds concurrently with real reader settlement.
                Task<bool>? confirmation = null;
                try { confirmation = scope.ConfirmTerminatedAsync(cleanup.Token); } catch { cleanupSucceeded = false; }
                foreach (var drain in new[] { stdout, stderr })
                {
                    if (drain is null) continue;
                    try { await drain.Completion.WaitAsync(cleanup.Token); } catch { }
                    cleanupSucceeded &= drain.Completion.IsCompleted && !drain.DisposeFailed;
                    Collect(drain);
                    if (!drain.Completion.IsCompleted) _ = drain.Completion.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
                }
                var confirmed = false;
                try { if (confirmation is not null) confirmed = await confirmation.WaitAsync(cleanup.Token); } catch { }
                cleanupSucceeded &= confirmed;
                if (confirmation is not null && !confirmation.IsCompleted) { cleanupSucceeded = false; _ = confirmation.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default); }
                if (confirmed && stdout?.Completion.IsCompleted != false && stderr?.Completion.IsCompleted != false)
                    try { cleanupSucceeded &= await scope.ReleaseAsync(cleanup.Token); } catch { cleanupSucceeded = false; }
                try { cleanupSucceeded &= await scope.SettleObserverAsync(cleanup.Token); } catch { cleanupSucceeded = false; }
                try { scope.Dispose(); } catch { cleanupSucceeded = false; }
            }
        }
        if (!cleanupSucceeded) throw new IOException("GITHUB_ARTIFACT_HELPER_CLEANUP_FAILED");
        if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
        if (budget.WorkExpired || _testDeadline.IsCancellationRequested) throw new IOException("GITHUB_ARTIFACT_HELPER_TIMEOUT");
        var code = failure.HasFlag(ArtifactBridgeFailure.OutputLimit) ? "GITHUB_ARTIFACT_HELPER_OUTPUT_LIMIT" :
            failure.HasFlag(ArtifactBridgeFailure.Io) ? "GITHUB_ARTIFACT_HELPER_IO_FAILED" :
            failure.HasFlag(ArtifactBridgeFailure.Start) ? "GITHUB_ARTIFACT_HELPER_START_FAILED" :
            failure.HasFlag(ArtifactBridgeFailure.Helper) ? "GITHUB_ARTIFACT_HELPER_FAILED" : null;
        if (code is not null) throw new IOException(code);
    }
}

internal sealed record GitHubArtifactRuntime(string Repository, long RepositoryId, long RunId)
{
    public static GitHubArtifactRuntime FromEnvironment()
    {
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY"); var repositoryId = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_ID"); var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
        if (repository != "ehonda/KicktippAi" || !TryCanonicalPositiveId(repositoryId, out var parsedRepositoryId) || !TryCanonicalPositiveId(runId, out var parsedRunId)) throw new InvalidOperationException("GITHUB_ARTIFACT_RUNTIME_IDENTITY_INVALID");
        return new(repository, parsedRepositoryId, parsedRunId);
    }
    private static bool TryCanonicalPositiveId(string? value, out long result) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0 && value == result.ToString(CultureInfo.InvariantCulture);
    public string ExpectedArtifactName => $"bundesliga-context-source-bundle-{BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, RepositoryId, RunId).StorageId}";
}

/// <summary>Immutable same-run adapter: bearer authentication is confined to GitHub's fixed API origin.</summary>
public sealed class GitHubContextSourceArtifactStore : IContextSourceBundleArtifactStore, IDisposable
{
    private const int MaxCompressedBytes = 4 * 1024 * 1024, MaxExpandedBytes = 3 * 1024 * 1024, MaxManifestBytes = 512 * 1024, MaxHashBytes = 65, MaxHtmlBytes = 2 * 1024 * 1024, MaxListingJsonBytes = 1024 * 1024;
    private static readonly TimeSpan OperationDeadline = TimeSpan.FromSeconds(30);
    private readonly CancellationToken _testOperationDeadline;
    private readonly Action<string, string>? _stagingTransitionForTests;
    private readonly TimeProvider _operationTimeProvider = TimeProvider.System;
    private static readonly Uri ApiOrigin = new("https://api.github.com/");
    private readonly IGitHubArtifactTool _tool; private readonly HttpClient _api; private readonly HttpClient _archive; private readonly GitHubArtifactRuntime _runtime; private readonly bool _ownsClients;
    public GitHubContextSourceArtifactStore()
    {
        _tool = new NodeGitHubArtifactTool(); _runtime = GitHubArtifactRuntime.FromEnvironment(); _ownsClients = true;
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN"); if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("GITHUB_ARTIFACT_TOKEN_MISSING");
        _api = CreateApiClient(token); _archive = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
    }
    internal GitHubContextSourceArtifactStore(IGitHubArtifactTool tool, HttpClient api, HttpClient archive, GitHubArtifactRuntime runtime, bool testSeam = false, CancellationToken testOperationDeadline = default, TimeProvider? operationTimeProvider = null,
        Action<string, string>? stagingTransitionForTests = null)
    {
        _testOperationDeadline = testOperationDeadline;
        _stagingTransitionForTests = stagingTransitionForTests;
        _operationTimeProvider = operationTimeProvider ?? TimeProvider.System;
        _tool = tool ?? throw new ArgumentNullException(nameof(tool)); _api = api ?? throw new ArgumentNullException(nameof(api)); _archive = archive ?? throw new ArgumentNullException(nameof(archive)); _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); _ownsClients = false;
        if (_runtime.Repository != "ehonda/KicktippAi" || _runtime.RepositoryId <= 0 || _runtime.RunId <= 0 || (!testSeam && _api.BaseAddress != ApiOrigin)) throw new InvalidOperationException("GITHUB_ARTIFACT_RUNTIME_IDENTITY_INVALID");
    }
    public async Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        ValidateAuthority(artifactName);
        using var deadline = new CancellationTokenSource(OperationDeadline, _operationTimeProvider); using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token, _testOperationDeadline);
        try { var matches = await ListMatchesAsync(artifactName, linked.Token); if (matches.Count == 0) return new(ContextSourceArtifactProbeDisposition.Absent, []); if (matches.Count != 1 || matches[0].Expired || matches[0].RunId != _runtime.RunId) return new(ContextSourceArtifactProbeDisposition.Conflict, []); return new(ContextSourceArtifactProbeDisposition.Present, ParseArchive(await DownloadArchiveAsync(matches[0].Id, linked.Token))); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ArtifactConflictException) { return new(ContextSourceArtifactProbeDisposition.Conflict, []); }
        catch (ArtifactListingException) { return new(ContextSourceArtifactProbeDisposition.Indeterminate, []); }
        catch (OperationCanceledException) { return new(ContextSourceArtifactProbeDisposition.Indeterminate, []); }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidDataException) { return new(ContextSourceArtifactProbeDisposition.Indeterminate, []); }
    }
    public async Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAuthority(artifactName); if (overwrite || compressionLevel != 0 || retentionDays != 7) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT"); ArgumentNullException.ThrowIfNull(entries); ValidateUploadEntries(entries);
        var root = CreateScratchDirectory();
        try
        {
            var content = Path.Combine(root, "content");
            try
            {
                Directory.CreateDirectory(content);
                var staged = false;
                foreach (var entry in entries)
                {
                    var target = ResolveContained(content, entry.Path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await File.WriteAllBytesAsync(target, entry.Bytes, cancellationToken);
                    if (!staged) { staged = true; _stagingTransitionForTests?.Invoke("first-file-staged", root); }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
            catch { throw new IOException("GITHUB_ARTIFACT_SCRATCH_INVALID"); }
            await _tool.ExecuteUploadAsync(new(artifactName, content, 0, 7), cancellationToken);
        }
        finally { try { _stagingTransitionForTests?.Invoke("before-delete", root); } finally { DeleteScratchDirectory(root); } }
    }
    private async Task<List<Artifact>> ListMatchesAsync(string artifactName, CancellationToken cancellationToken)
    {
        var result = new List<Artifact>(); int? total = null; var listed = 0;
        for (var page = 1; page <= 100; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var listingUri = new Uri(ApiOrigin, $"repos/{_runtime.Repository}/actions/runs/{_runtime.RunId}/artifacts?per_page=100&page={page}");
            using var response = await _api.GetAsync(listingUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaxListingJsonBytes ||
                (response.RequestMessage?.RequestUri is { } requested && requested != listingUri)) throw new ArtifactListingException();
            byte[] listing; try { await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken); listing = await ReadLimitedAsync(stream, MaxListingJsonBytes, cancellationToken); } catch (ArtifactConflictException) { throw new ArtifactListingException(); } using var document = JsonDocument.Parse(listing);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("total_count", out var countValue) || !countValue.TryGetInt32(out var count) || count < 0 || !document.RootElement.TryGetProperty("artifacts", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 100) throw new ArtifactListingException();
            total ??= count; listed += items.GetArrayLength(); if (total != count || listed > total) throw new ArtifactListingException();
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String) throw new ArtifactListingException();
                if (string.Equals(name.GetString(), artifactName, StringComparison.Ordinal)) result.Add(ParseArtifact(item));
            }
            var hasNext = HasNextPage(response, page);
            if (listed == total)
            {
                if (hasNext) throw new ArtifactListingException();
                return result;
            }
            if (items.GetArrayLength() != 100 || !hasNext) throw new ArtifactListingException();
        }
        throw new ArtifactListingException();
    }    private async Task<byte[]> DownloadArchiveAsync(long artifactId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archiveUri = new Uri(ApiOrigin, $"repos/{_runtime.Repository}/actions/artifacts/{artifactId}/zip");
        using var response = await _api.GetAsync(archiveUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken); if (response.StatusCode is not HttpStatusCode.Found and not HttpStatusCode.Redirect ||
            (response.RequestMessage?.RequestUri is { } apiRequested && apiRequested != archiveUri)) throw new IOException("GITHUB_ARTIFACT_ARCHIVE_FAILED");
        var location = response.Headers.Location; if (location is null || !location.IsAbsoluteUri || location.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(location.UserInfo)) throw new ArtifactConflictException();
        cancellationToken.ThrowIfCancellationRequested();
        using var signed = await _archive.GetAsync(location, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if ((signed.RequestMessage?.RequestUri is { } requested && requested != location) || (int)signed.StatusCode is >= 300 and < 400) throw new ArtifactConflictException();
        // Failed authorization, expired signed URLs, timeouts and service errors do not
        // establish immutable content conflict or authoritative absence.
        if (!signed.IsSuccessStatusCode) throw new IOException("GITHUB_ARTIFACT_ARCHIVE_INDETERMINATE");
        if (signed.Content.Headers.ContentLength is > MaxCompressedBytes) throw new ArtifactConflictException();
        await using var stream = await signed.Content.ReadAsStreamAsync(cancellationToken); return await ReadLimitedAsync(stream, MaxCompressedBytes, cancellationToken);
    }
    internal static IReadOnlyList<ContextSourceArtifactEntry> InspectArchiveForTests(byte[] archive) => ParseArchive(archive);
    private static IReadOnlyList<ContextSourceArtifactEntry> ParseArchive(byte[] archive)
    {
        try
        {
            var metadata = ValidateCentralDirectory(archive); using var memory = new MemoryStream(archive, false); using var zip = new ZipArchive(memory, ZipArchiveMode.Read); if (zip.Entries.Count != metadata.Count) throw new ArtifactConflictException(); var total = 0; var result = new List<ContextSourceArtifactEntry>();
            foreach (var entry in zip.Entries)
            {
                if (!metadata.TryGetValue(entry.FullName, out var bound) || entry.Length != bound.Expanded || entry.CompressedLength != bound.Compressed) throw new ArtifactConflictException(); var maximum = MaximumFor(entry.FullName); if (entry.Length > maximum || total > MaxExpandedBytes - entry.Length) throw new ArtifactConflictException(); using var input = entry.Open(); var bytes = ReadLimited(input, checked((int)entry.Length), maximum); if (Crc32(bytes) != bound.Crc) throw new ArtifactConflictException(); total += bytes.Length; result.Add(new(entry.FullName, bytes));
            }
            if (!result.Select(x => x.Path).ToHashSet(StringComparer.Ordinal).SetEquals(metadata.Keys) || !result.Any(x => x.Path == "manifest.json") || !result.Any(x => x.Path == "bundle.sha256")) throw new ArtifactConflictException(); return result.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
        }
        catch (ArtifactConflictException) { throw; } catch (Exception exception) when (exception is InvalidDataException or ArgumentException or OverflowException or IOException or NotSupportedException) { throw new ArtifactConflictException(); }
    }    private static Dictionary<string, ZipBound> ValidateCentralDirectory(byte[] data)
    {
        if (data.Length < 22) throw new ArtifactConflictException(); var eocd = data.Length - 22;
        if (U32(data, eocd) != 0x06054b50 || U16(data, eocd + 4) != 0 || U16(data, eocd + 6) != 0 || U16(data, eocd + 8) != U16(data, eocd + 10) || U16(data, eocd + 8) == ushort.MaxValue || U32(data, eocd + 12) == uint.MaxValue || U32(data, eocd + 16) == uint.MaxValue || U16(data, eocd + 20) != 0) throw new ArtifactConflictException();
        var entries = U16(data, eocd + 10); var centralSize = U32(data, eocd + 12); var centralOffset = U32(data, eocd + 16);
        if (entries is < 2 or > 3 || centralOffset > data.Length || centralSize > data.Length - centralOffset || centralOffset + centralSize != eocd) throw new ArtifactConflictException();
        var result = new Dictionary<string, ZipBound>(StringComparer.Ordinal); var folded = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var ranges = new List<(long Start, long End)>(); var cursor = checked((int)centralOffset); ulong declared = 0;
        for (var index = 0; index < entries; index++)
        {
            if (cursor > data.Length - 46 || U32(data, cursor) != 0x02014b50) throw new ArtifactConflictException();
            var madeBy = U16(data, cursor + 4); var flags = U16(data, cursor + 8); var method = U16(data, cursor + 10); var crc = U32(data, cursor + 16); var compressed = U32(data, cursor + 20); var expanded = U32(data, cursor + 24); var nameLength = U16(data, cursor + 28); var extraLength = U16(data, cursor + 30); var commentLength = U16(data, cursor + 32); var disk = U16(data, cursor + 34); var attrs = U32(data, cursor + 38); var localOffset = U32(data, cursor + 42); var record = checked(46 + nameLength + extraLength + commentLength);
            if (cursor > data.Length - record || disk != 0 || extraLength != 0 || commentLength != 0 || !SafeFlags(flags) || method is not (0 or 8) || compressed == uint.MaxValue || expanded == uint.MaxValue || localOffset > centralOffset || localOffset > data.Length - 30 || U32(data, checked((int)localOffset)) != 0x04034b50 || !RegularFile(madeBy, attrs)) throw new ArtifactConflictException();
            var name = ReadUtf8(data, cursor + 46, nameLength); if (!IsAllowedName(name) || !folded.Add(name)) throw new ArtifactConflictException();
            var local = checked((int)localOffset); var localFlags = U16(data, local + 6); var localMethod = U16(data, local + 8); var localCrc = U32(data, local + 14); var localCompressed = U32(data, local + 18); var localExpanded = U32(data, local + 22); var localNameLength = U16(data, local + 26); var localExtraLength = U16(data, local + 28); var dataOffsetLong = checked((long)local + 30L + localNameLength + localExtraLength);
            if (localExtraLength != 0 || !SafeFlags(localFlags) || localFlags != flags || localMethod != method || localCrc != crc || localCompressed != compressed || localExpanded != expanded || dataOffsetLong > centralOffset || compressed > centralOffset - dataOffsetLong || ReadUtf8(data, local + 30, localNameLength) != name) throw new ArtifactConflictException();
            declared += expanded; if (declared > MaxExpandedBytes || !result.TryAdd(name, new(compressed, expanded, crc))) throw new ArtifactConflictException(); ranges.Add((local, dataOffsetLong + compressed)); cursor += record;
        }
        if (cursor != centralOffset + centralSize) throw new ArtifactConflictException(); long expectedStart = 0; foreach (var range in ranges.OrderBy(range => range.Start)) { if (range.Start != expectedStart || range.End < range.Start || range.End > centralOffset) throw new ArtifactConflictException(); expectedStart = range.End; } if (expectedStart != centralOffset) throw new ArtifactConflictException(); return result;
    }
    private static bool SafeFlags(ushort flags) => (flags & ~0x0800) == 0;
    private static bool RegularFile(ushort madeBy, uint attrs)
    {
        var os = madeBy >> 8; var dos = (ushort)(attrs & 0xffff);
        if (os == 3) return ((attrs >> 16) & 0xF000) == 0x8000 && (dos & 0x0458) == 0;
        if (os == 0) return (dos & 0x0458) == 0;
        return false;
    }    private static int MaximumFor(string name) => name switch { "manifest.json" => MaxManifestBytes, "bundle.sha256" => MaxHashBytes, "club-elo/source.html" => MaxHtmlBytes, _ => throw new ArtifactConflictException() };
    private static Artifact ParseArtifact(JsonElement value) { if (!value.TryGetProperty("id", out var id) || !id.TryGetInt64(out var artifactId) || artifactId <= 0 || !value.TryGetProperty("expired", out var expired) || expired.ValueKind is not JsonValueKind.True and not JsonValueKind.False || !value.TryGetProperty("workflow_run", out var run) || run.ValueKind != JsonValueKind.Object || !run.TryGetProperty("id", out var runId) || !runId.TryGetInt64(out var parsedRunId) || parsedRunId <= 0) throw new ArtifactConflictException(); return new(artifactId, expired.GetBoolean(), parsedRunId); }
    private bool HasNextPage(HttpResponseMessage response, int page)
    {
        if (!response.Headers.TryGetValues("Link", out var values)) return false;
        var expected = new Uri(ApiOrigin, $"repos/{_runtime.Repository}/actions/runs/{_runtime.RunId}/artifacts?per_page=100&page={page + 1}").AbsoluteUri;
        var next = 0;
        foreach (var value in values)
        {
            foreach (var part in value.Split(','))
            {
                var trimmed = part.Trim(); if (!trimmed.Contains("rel=\"next\"", StringComparison.Ordinal)) continue;
                var close = trimmed.IndexOf('>'); if (!trimmed.StartsWith('<') || close <= 1 || !Uri.TryCreate(trimmed[1..close], UriKind.Absolute, out var uri) || uri.AbsoluteUri != expected) throw new ArtifactListingException(); next++;
            }
        }
        if (next > 1) throw new ArtifactListingException(); return next == 1;
    }    private void ValidateAuthority(string artifactName) { if (artifactName != _runtime.ExpectedArtifactName) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT"); }
    private static void ValidateUploadEntries(IReadOnlyList<ContextSourceArtifactEntry> entries) { if (entries.Count is < 2 or > 3 || entries.Any(x => !x.IsRegularFile || x.LinkTarget is not null) || entries.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count() != entries.Count || !entries.Any(x => x.Path == "manifest.json") || !entries.Any(x => x.Path == "bundle.sha256")) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT"); foreach (var entry in entries) { _ = ResolveContained("C:\\scratch", entry.Path); if (entry.Bytes.Length > MaximumFor(entry.Path)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT"); } }
    private static bool IsAllowedName(string name) => name is "manifest.json" or "bundle.sha256" or "club-elo/source.html";
    private static byte[] ReadLimited(Stream stream, int expected, int maximum) { if (expected > maximum) throw new ArtifactConflictException(); var bytes = new byte[expected]; var offset = 0; while (offset < expected) { var read = stream.Read(bytes, offset, expected - offset); if (read == 0) throw new ArtifactConflictException(); offset += read; } if (stream.ReadByte() != -1) throw new ArtifactConflictException(); return bytes; }
    private static async Task<byte[]> ReadLimitedAsync(Stream stream, int maximum, CancellationToken cancellationToken)
    {
        await using var target = new MemoryStream(); var buffer = new byte[81920];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allowance = checked(maximum - (int)target.Length);
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, allowance + 1)), cancellationToken);
            if (read == 0) return target.ToArray();
            if (read > allowance) throw new ArtifactConflictException();
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
    private static string ReadUtf8(byte[] bytes, int offset, int count) { if (offset < 0 || count < 0 || offset > bytes.Length - count) throw new ArtifactConflictException(); var text = new UTF8Encoding(false, true).GetString(bytes, offset, count); if (Encoding.UTF8.GetByteCount(text) != count) throw new ArtifactConflictException(); return text; }
    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff; foreach (var value in bytes) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u); } return ~crc;
    }    private static ushort U16(byte[] bytes, int offset) { if (offset < 0 || offset > bytes.Length - 2) throw new ArtifactConflictException(); return BitConverter.ToUInt16(bytes, offset); }
    private static uint U32(byte[] bytes, int offset) { if (offset < 0 || offset > bytes.Length - 4) throw new ArtifactConflictException(); return BitConverter.ToUInt32(bytes, offset); }
    internal static string ResolveContained(string root, string relative) { if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.StartsWith('/') || relative.Contains(':') || relative.Split('/').Any(value => value is "" or "." or "..")) throw new IOException("GITHUB_ARTIFACT_PATH_INVALID"); var basePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar; var resolved = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))); if (!resolved.StartsWith(basePath, ArtifactLaunchAdmission.PathComparison)) throw new IOException("GITHUB_ARTIFACT_PATH_INVALID"); return resolved; }
    private static HttpClient CreateApiClient(string token) { var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = ApiOrigin, Timeout = Timeout.InfiniteTimeSpan }; client.DefaultRequestHeaders.UserAgent.ParseAdd("KicktippAi-context-source/1.0"); client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28"); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client; }
    private static string CreateScratchDirectory()
    {
        string? root = null;
        try
        {
            var temp = ArtifactLaunchAdmission.Absolute(Path.GetTempPath(), "GITHUB_ARTIFACT_SCRATCH_INVALID");
            ArtifactLaunchAdmission.DirectoryChain(temp, "GITHUB_ARTIFACT_SCRATCH_INVALID");
            var fixedRoot = Path.Combine(temp, "kicktippai-github-artifact"); Directory.CreateDirectory(fixedRoot);
            ArtifactLaunchAdmission.DirectoryChain(fixedRoot, "GITHUB_ARTIFACT_SCRATCH_INVALID");
            root = Path.Combine(fixedRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); ArtifactLaunchAdmission.DirectoryChain(root, "GITHUB_ARTIFACT_SCRATCH_INVALID"); return root;
        }
        catch { if (root is not null) DeleteScratchDirectory(root); throw new IOException("GITHUB_ARTIFACT_SCRATCH_INVALID"); }
    }
    private static void DeleteScratchDirectory(string root)
    {
        const string error = "GITHUB_ARTIFACT_SCRATCH_INVALID";
        try
        {
            var absolute = ArtifactLaunchAdmission.Absolute(root, error);
            var fixedRoot = Path.Combine(ArtifactLaunchAdmission.Absolute(Path.GetTempPath(), error), "kicktippai-github-artifact");
            var token = Path.GetFileName(absolute);
            if (token.Length != 32 || token.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
                !string.Equals(absolute, Path.Combine(fixedRoot, token), ArtifactLaunchAdmission.PathComparison)) throw new IOException();
            ArtifactLaunchAdmission.DirectoryChain(fixedRoot, error);
            if (!Directory.Exists(absolute)) { if (File.Exists(absolute)) throw new IOException(); return; }
            void RemoveDirectory(string directory, string[] files, string? child = null)
            {
                ArtifactLaunchAdmission.DirectoryChain(directory, error);
                // Finite known staging shape; never recursively follow a substituted tree.
                var entries = Directory.EnumerateFileSystemEntries(directory).Take(files.Length + (child is null ? 0 : 1) + 1).ToArray();
                foreach (var entry in entries)
                {
                    var name = Path.GetFileName(entry);
                    if (name == child)
                    {
                        if (child == "content") RemoveDirectory(entry, ["manifest.json", "bundle.sha256"], "club-elo");
                        else RemoveDirectory(entry, ["source.html"]);
                    }
                    else if (files.Contains(name, StringComparer.Ordinal)) { ArtifactLaunchAdmission.RegularFile(entry, error); File.Delete(entry); }
                    else throw new IOException();
                }
                Directory.Delete(directory, false);
            }
            RemoveDirectory(absolute, [], "content");
        }
        catch { throw new IOException(error); }
    }
    public void Dispose() { if (_ownsClients) { _api.Dispose(); _archive.Dispose(); } }
    private sealed record Artifact(long Id, bool Expired, long RunId); private sealed record ZipBound(uint Compressed, uint Expanded, uint Crc); private sealed class ArtifactConflictException : IOException { } private sealed class ArtifactListingException : IOException { }
}
