using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

public sealed record ContextSourceBundleFiles(BundesligaContextSourceBundle Bundle, IReadOnlyDictionary<string, byte[]> Payloads)
{
    public string Digest => Bundle.BundleSha256(Payloads);
    public void Validate()
    {
        Bundle.Validate();
        var expected = Bundle.Observations.Where(x => x.Payload is not null).Select(x => x.Payload!.Path).ToHashSet(StringComparer.Ordinal);
        if (!Payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        _ = Digest;
    }
}

public sealed record ContextSourceArtifactEntry(string Path, byte[] Bytes, bool IsRegularFile = true, string? LinkTarget = null);
public enum ContextSourceArtifactProbeDisposition { Absent, Present, Indeterminate, Conflict }
public sealed record ContextSourceArtifactProbe(ContextSourceArtifactProbeDisposition Disposition, IReadOnlyList<ContextSourceArtifactEntry> Entries);

public sealed class ContextSourceArtifactRetryException : IOException
{
    public const string ErrorCode = "HANDOFF_ARTIFACT_INDETERMINATE";
    public ContextSourceArtifactRetryException() : base(ErrorCode) { }
}

public interface IContextSourceBundleArtifactStore
{
    Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default);
    Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default);
}

public static class ContextSourceBundleHandoff
{
    public static string CreateDevelopmentDirectory(BundesligaContextSourceCycleIdentity cycle)
    {
        if (cycle.Scope != BundesligaContextSourceScope.Development) throw new InvalidOperationException("Only development cycles use local handoff.");
        return Path.Combine(Path.GetTempPath(), "kicktippai-context-source", cycle.StorageId);
    }

    public static async Task WriteDevelopmentAsync(ContextSourceBundleFiles files, CancellationToken cancellationToken = default)
    {
        files.Validate();
        var root = CreateDevelopmentDirectory(files.Bundle.Cycle);
        if (Directory.Exists(root)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "manifest.json"), files.Bundle.CreateManifestUtf8(), cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(root, "bundle.sha256"), Encoding.ASCII.GetBytes(files.Digest + "\n"), cancellationToken);
            foreach (var (relative, bytes) in files.Payloads)
            {
                var path = ResolveContained(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            }
            ValidateDevelopmentDirectory(files);
        }
        catch { CleanupDevelopment(files.Bundle.Cycle); throw; }
    }

    public static void ValidateDevelopmentDirectory(ContextSourceBundleFiles files)
    {
        files.Validate(); var root = CreateDevelopmentDirectory(files.Bundle.Cycle);
        if (!Directory.Exists(root)) throw new InvalidDataException("LOCAL_HANDOFF_MISSING");
        var expected = ExpectedEntries(files).ToDictionary(x => x.Path, StringComparer.Ordinal);
        var actual = ReadRegularTree(root);
        if (!actual.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        foreach (var (path, entry) in expected) if (!actual[path].AsSpan().SequenceEqual(entry.Bytes)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        VerifyEntries(files.Bundle, actual.Select(x => new ContextSourceArtifactEntry(x.Key, x.Value)).ToArray(), files.Digest);
    }

    public static async Task<BundesligaContextSourceOuterCycle> ReserveUploadAndMakeReadyAsync(
        IBundesligaContextSourceCycleRepository repository, BundesligaContextSourceOuterCycle requestedCycle, ContextSourceBundleFiles files,
        IContextSourceBundleArtifactStore artifactStore, CancellationToken cancellationToken = default)
    {
        files.Validate(); requestedCycle.Validate(); ValidateBundleCycle(requestedCycle, files.Bundle); var cycle = requestedCycle.Identity;
        if (cycle.Scope != BundesligaContextSourceScope.ProductionLive) throw new InvalidOperationException("Production artifact handoff requires production scope.");
        var name = $"bundesliga-context-source-bundle-{cycle.StorageId}";
        var reserved = await repository.TransitionCycleAsync(cycle, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, files.Digest, name, cancellationToken);
        ValidateBundleCycle(reserved, files.Bundle);
        var probe = await artifactStore.ProbeAsync(name, cancellationToken);
        if (probe.Disposition == ContextSourceArtifactProbeDisposition.Present)
        {
            VerifyEntries(files.Bundle, probe.Entries, files.Digest);
        }
        else if (probe.Disposition == ContextSourceArtifactProbeDisposition.Absent)
        {
            await artifactStore.UploadAsync(name, ExpectedEntries(files), overwrite: false, compressionLevel: 0, retentionDays: 7, cancellationToken);
            var verification = await artifactStore.ProbeAsync(name, cancellationToken);
            if (verification.Disposition == ContextSourceArtifactProbeDisposition.Indeterminate) throw new ContextSourceArtifactRetryException();
            if (verification.Disposition != ContextSourceArtifactProbeDisposition.Present) throw new InvalidDataException(verification.Disposition == ContextSourceArtifactProbeDisposition.Absent ? "HANDOFF_UPLOAD_FAILED" : "HANDOFF_ARTIFACT_CONFLICT");
            VerifyEntries(files.Bundle, verification.Entries, files.Digest);
        }
        else if (probe.Disposition == ContextSourceArtifactProbeDisposition.Indeterminate)
        {
            throw new ContextSourceArtifactRetryException();
        }
        else
        {
            throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        }
        var ready = await repository.TransitionCycleAsync(cycle, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, files.Digest, name, cancellationToken);
        ValidateBundleCycle(ready, files.Bundle);
        return ready;
    }

    public static async Task<ContextSourceBundleFiles> LoadProductionAsync(BundesligaContextSourceOuterCycle cycle, IContextSourceBundleArtifactStore artifactStore, CancellationToken cancellationToken = default)
    {
        if (cycle.Identity.Scope != BundesligaContextSourceScope.ProductionLive || cycle.Status is not (BundesligaContextSourceCycleStatus.UploadReserved or BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete) || cycle.ArtifactName is null || cycle.BundleSha256 is null) throw new InvalidDataException("Production handoff reservation is incomplete.");
        cycle.Validate();
        var probe = await artifactStore.ProbeAsync(cycle.ArtifactName, cancellationToken);
        if (probe.Disposition == ContextSourceArtifactProbeDisposition.Indeterminate) throw new ContextSourceArtifactRetryException();
        if (probe.Disposition != ContextSourceArtifactProbeDisposition.Present)
            throw new InvalidDataException(probe.Disposition == ContextSourceArtifactProbeDisposition.Absent
                ? cycle.Status == BundesligaContextSourceCycleStatus.UploadReserved ? "HANDOFF_UPLOAD_FAILED" : "HANDOFF_ARTIFACT_MISSING"
                : "HANDOFF_ARTIFACT_CONFLICT");
        return ParseAndVerifyAsConflict(probe.Entries, cycle.BundleSha256, cycle);
    }

    public static ContextSourceBundleFiles LoadDevelopment(BundesligaContextSourceOuterCycle cycle, string reservedDigest)
    {
        var root = CreateDevelopmentDirectory(cycle.Identity); if (!Directory.Exists(root)) throw new InvalidDataException("LOCAL_HANDOFF_MISSING");
        cycle.Validate();
        var entries = ReadRegularTree(root).Select(x => new ContextSourceArtifactEntry(x.Key, x.Value)).ToArray();
        return ParseAndVerifyAsConflict(entries, reservedDigest, cycle);
    }

    public static void VerifyEntries(BundesligaContextSourceBundle bundle, IReadOnlyList<ContextSourceArtifactEntry> entries, string reservedDigest)
    {
        BundesligaContextSourceHashing.ValidateSha(reservedDigest);
        var clubElo = bundle.Observations.SingleOrDefault(value => value.Source == BundesligaContextSource.ClubElo);
        if (clubElo?.Payload is { Path: var clubEloPath } && clubEloPath is not ("club-elo/source.csv" or "club-elo/source.html"))
            throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        if (entries.Any(x => !x.IsRegularFile || x.LinkTarget is not null || x.Path.Contains('\\') || x.Path.StartsWith('/') || x.Path.Split('/').Any(segment => segment is "" or "." or ".."))) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        var unique = entries.GroupBy(x => x.Path, StringComparer.Ordinal).ToArray(); if (unique.Any(x => x.Count() != 1)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        var map = unique.ToDictionary(x => x.Key, x => x.Single().Bytes, StringComparer.Ordinal);
        var expectedNames = new[] { "manifest.json", "bundle.sha256" }.Concat(bundle.Observations.Where(x => x.Payload is not null).Select(x => x.Payload!.Path)).ToHashSet(StringComparer.Ordinal);
        if (!map.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedNames)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        if (!map["manifest.json"].AsSpan().SequenceEqual(bundle.CreateManifestUtf8())) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        if (!map["bundle.sha256"].AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(reservedDigest + "\n"))) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        var payloads = bundle.Observations.Where(x => x.Payload is not null).ToDictionary(x => x.Payload!.Path, x => map[x.Payload!.Path], StringComparer.Ordinal);
        if (bundle.BundleSha256(payloads) != reservedDigest) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
    }

    private static ContextSourceBundleFiles ParseAndVerify(IReadOnlyList<ContextSourceArtifactEntry> entries, string reservedDigest, BundesligaContextSourceOuterCycle expectedCycle)
    {
        var manifests = entries.Where(x => x.Path == "manifest.json").ToArray(); if (manifests.Length != 1) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        var bundle = BundesligaContextSourceBundle.ParseManifest(manifests[0].Bytes);
        ValidateBundleCycle(expectedCycle, bundle);
        VerifyEntries(bundle, entries, reservedDigest);
        var payloads = entries.Where(x => x.Path is not ("manifest.json" or "bundle.sha256")).ToDictionary(x => x.Path, x => x.Bytes, StringComparer.Ordinal);
        return new ContextSourceBundleFiles(bundle, payloads);
    }

    private static ContextSourceBundleFiles ParseAndVerifyAsConflict(IReadOnlyList<ContextSourceArtifactEntry> entries, string reservedDigest, BundesligaContextSourceOuterCycle expectedCycle)
    {
        try { return ParseAndVerify(entries, reservedDigest, expectedCycle); }
        catch (JsonException exception) { throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT", exception); }
        catch (InvalidDataException exception) { throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT", exception); }
    }

    public static void ValidateBundleCycle(BundesligaContextSourceOuterCycle expectedCycle, BundesligaContextSourceBundle bundle)
    {
        expectedCycle.Validate(); bundle.Validate();
        if (bundle.Cycle != expectedCycle.Identity
            || bundle.StartedAtUtc != expectedCycle.StartedAtUtc
            || bundle.StalenessReferenceAtUtc != expectedCycle.StalenessReferenceAtUtc
            || bundle.ProducerLaneId != expectedCycle.ProducerLaneId
            || !bundle.ExpectedConsumers.SequenceEqual(expectedCycle.ExpectedConsumers, StringComparer.Ordinal)
            || !bundle.Observations.Select(value => value.Source).SequenceEqual(expectedCycle.EnabledSources))
            throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
    }

    public static void ValidateBundleCycle(BundesligaContextSourceOuterCycle requestedCycle, BundesligaContextSourceOuterCycle persistedCycle)
    {
        requestedCycle.Validate(); persistedCycle.Validate();
        if (persistedCycle.Identity != requestedCycle.Identity
            || persistedCycle.ProducerLaneId != requestedCycle.ProducerLaneId
            || !persistedCycle.ExpectedConsumers.SequenceEqual(requestedCycle.ExpectedConsumers, StringComparer.Ordinal)
            || !persistedCycle.EnabledSources.SequenceEqual(requestedCycle.EnabledSources))
            throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
    }

    public static void CleanupDevelopment(BundesligaContextSourceCycleIdentity cycle)
    {
        var root = Path.GetFullPath(CreateDevelopmentDirectory(cycle));
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kicktippai-context-source")) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(root) != cycle.StorageId) throw new InvalidOperationException("Refusing unsafe handoff cleanup.");
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static IReadOnlyList<ContextSourceArtifactEntry> ExpectedEntries(ContextSourceBundleFiles files)
    {
        var entries = new List<ContextSourceArtifactEntry> { new("manifest.json", files.Bundle.CreateManifestUtf8()), new("bundle.sha256", Encoding.ASCII.GetBytes(files.Digest + "\n")) };
        entries.AddRange(files.Bundle.Observations.Where(x => x.Payload is not null).Select(x => new ContextSourceArtifactEntry(x.Payload!.Path, files.Payloads[x.Payload.Path])));
        return entries;
    }

    private static Dictionary<string, byte[]> ReadRegularTree(string root)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal); var pending = new Stack<DirectoryInfo>(); pending.Push(new DirectoryInfo(root));
        while (pending.Count > 0)
        {
            foreach (var entry in pending.Pop().EnumerateFileSystemInfos())
            {
                if (entry.LinkTarget is not null || entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
                if (entry is DirectoryInfo directory) { pending.Push(directory); continue; }
                if (entry is not FileInfo file) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
                var relative = Path.GetRelativePath(root, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
                if (!result.TryAdd(relative, File.ReadAllBytes(file.FullName))) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
            }
        }
        return result;
    }

    private static string ResolveContained(string root, string relative)
    {
        if (relative.Contains('\\') || relative.StartsWith('/') || relative.Split('/').Any(x => x is "" or "." or "..")) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT");
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar; var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("HANDOFF_ARTIFACT_CONFLICT"); return path;
    }
}
