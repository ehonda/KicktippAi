using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>Firestore implementation of the directly addressed ADR-0060 binding store.</summary>
public sealed class FirebaseResolvedTypedContextPublicationBindingRepository : IResolvedTypedContextPublicationBindingRepository
{
    private const string Collection = "resolved-typed-context-publication-bindings";
    private readonly FirestoreDb _db;
    private readonly ILogger<FirebaseResolvedTypedContextPublicationBindingRepository> _logger;
    // Optional diagnostics seam: it observes every transaction callback selection, including
    // SDK retries, without changing the Firestore write path or returned value.
    private readonly Action<TypedContextPublicationBindingUpsertResult>? _transactionSelectionObserver;

    public FirebaseResolvedTypedContextPublicationBindingRepository(
        FirestoreDb db,
        ILogger<FirebaseResolvedTypedContextPublicationBindingRepository> logger,
        Action<TypedContextPublicationBindingUpsertResult>? transactionSelectionObserver = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transactionSelectionObserver = transactionSelectionObserver;
    }

    public async Task<ResolvedTypedContextPublicationBinding?> GetExactAsync(
        ResolvedTypedContextPublicationBindingKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var evaluationInstant = DateTimeOffset.UtcNow;
        var reference = BindingReference(key);
        var snapshot = await reference.GetSnapshotAsync(cancellationToken);
        return snapshot.Exists ? ReadCurrent(snapshot, key, evaluationInstant) : null;
    }

    public async Task<TypedContextPublicationBindingUpsertResult> UpsertExactAsync(
        ResolvedTypedContextPublicationBinding candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var evaluationInstant = DateTimeOffset.UtcNow;
        SchadensfresseTypedContextProfiles.ValidateBindingStructure(candidate);
        SchadensfresseTypedContextProfiles.ValidateBindingFreshness(candidate, evaluationInstant);
        var reference = BindingReference(candidate.Key);

        try
        {
            var selected = await _db.RunTransactionAsync(async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(reference);
                // A stale binding remains structurally valid historical state. It must reach
                // SelectEffective so a fresh identity-equal attestation can refresh it.
                var current = snapshot.Exists ? ReadStructural(snapshot, candidate.Key) : null;
                var result = ResolvedTypedContextPublicationBindingContract.SelectEffective(
                    current, candidate, evaluationInstant);
                _transactionSelectionObserver?.Invoke(result);
                if (result.Disposition is TypedContextPublicationBindingUpsertDisposition.Created)
                {
                    transaction.Create(reference, ToFirestore(reference.Id, result.EffectiveBinding));
                }
                else if (result.Disposition is TypedContextPublicationBindingUpsertDisposition.Updated)
                {
                    transaction.Set(reference, ToFirestore(reference.Id, result.EffectiveBinding));
                }

                return result;
            }, cancellationToken: cancellationToken);

            if (!selected.Succeeded)
            {
                return selected;
            }

            // Do not return a candidate from a losing retry. Re-read the one exact document
            // at one captured instant and accept an identity-equal later winner only.
            var readbackInstant = DateTimeOffset.UtcNow;
            var committed = await reference.GetSnapshotAsync(cancellationToken);
            var returned = ReadCurrent(committed, candidate.Key, readbackInstant);
            return CompleteSelectedTransactionReadback(candidate.Key, selected, returned, readbackInstant);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            _logger.LogError(ex, "Failed to upsert exact resolved typed context publication binding {BindingId}", reference.Id);
            throw;
        }
    }

    private DocumentReference BindingReference(ResolvedTypedContextPublicationBindingKey key)
    {
        var id = TypedContextCanonicalJson.CreatePhysicalBindingId(key);
        var decoded = TypedContextCanonicalJson.DeserializePhysicalBindingId(id);
        if (decoded != key)
        {
            throw new InvalidDataException("Resolved typed context binding physical ID does not round trip its requested key.");
        }

        return _db.Collection(Collection).Document(id);
    }

    private static FirestoreResolvedTypedContextPublicationBinding ToFirestore(
        string id,
        ResolvedTypedContextPublicationBinding binding) => new()
    {
        Id = id,
        CanonicalJsonUtf8 = ByteString.CopyFrom(binding.SerializeCanonical())
    };

    private static ResolvedTypedContextPublicationBinding ReadCurrent(
        DocumentSnapshot snapshot,
        ResolvedTypedContextPublicationBindingKey requestedKey,
        DateTimeOffset evaluationInstant)
    {
        var binding = ReadStructural(snapshot, requestedKey);
        ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(requestedKey, binding, binding, evaluationInstant);
        return binding;
    }

    private static ResolvedTypedContextPublicationBinding ReadStructural(
        DocumentSnapshot snapshot,
        ResolvedTypedContextPublicationBindingKey requestedKey)
    {
        if (!snapshot.Exists || !string.Equals(snapshot.Id, TypedContextCanonicalJson.CreatePhysicalBindingId(requestedKey), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Resolved typed context binding readback is not the exact requested document.");
        }

        var physicalKey = TypedContextCanonicalJson.DeserializePhysicalBindingId(snapshot.Id);
        if (physicalKey != requestedKey)
        {
            throw new InvalidDataException("Resolved typed context binding physical key differs from the requested key.");
        }

        var stored = snapshot.ConvertTo<FirestoreResolvedTypedContextPublicationBinding>();
        if (!string.Equals(stored.Id, snapshot.Id, StringComparison.Ordinal) || stored.CanonicalJsonUtf8 is null || stored.CanonicalJsonUtf8.Length == 0)
        {
            throw new InvalidDataException("Resolved typed context binding carrier is malformed.");
        }

        var binding = ResolvedTypedContextPublicationBinding.DeserializeCanonical(stored.CanonicalJsonUtf8.Span);
        if (binding.Key != physicalKey)
        {
            throw new InvalidDataException("Resolved typed context binding stored key differs from its physical key.");
        }

        return binding;
    }

    // This is the exact post-transaction completion used above. Keeping it internal lets the
    // emulator suite deterministically exercise a competing committed winner, which the
    // Firestore emulator cannot schedule reliably enough to force on every run.
    internal static TypedContextPublicationBindingUpsertResult CompleteSelectedTransactionReadback(
        ResolvedTypedContextPublicationBindingKey requestedKey,
        TypedContextPublicationBindingUpsertResult selected,
        ResolvedTypedContextPublicationBinding returned,
        DateTimeOffset readbackEvaluationInstant)
    {
        ArgumentNullException.ThrowIfNull(requestedKey);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(returned);
        if (!selected.Succeeded)
        {
            return selected;
        }

        ResolvedTypedContextPublicationBindingContract.ValidateExactReadback(
            requestedKey, selected.EffectiveBinding, returned, readbackEvaluationInstant);
        return new TypedContextPublicationBindingUpsertResult(selected.Disposition, returned);
    }
}
