using Google.Cloud.Firestore;
using Google.Protobuf;

namespace FirebaseAdapter.Models;

/// <summary>Directly addressed canonical ADR-0060 current-publication attestation.</summary>
[FirestoreData]
public sealed class FirestoreResolvedTypedContextPublicationBinding
{
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>Exact canonical UTF-8 binding bytes, stored without an alternate projection.</summary>
    [FirestoreProperty("canonicalJsonUtf8")]
    public ByteString CanonicalJsonUtf8 { get; set; } = ByteString.Empty;
}
