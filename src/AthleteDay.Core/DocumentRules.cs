using AthleteDay.Contracts;

namespace AthleteDay.Core;

/// <summary>Need-to-know per document type (spec section 4, AC-86, ADR-15).</summary>
public static class DocumentAccess
{
    private static readonly IReadOnlyDictionary<DocumentType, string[]> Readers = new Dictionary<DocumentType, string[]>
    {
        [DocumentType.Medical] = [Roles.Physio],
        [DocumentType.GovernmentId] = [Roles.Admin],
        [DocumentType.Certificate] = [Roles.Coach, Roles.Physio],
        [DocumentType.General] = [Roles.Coach, Roles.Physio],
    };

    public static bool CanSee(string role, DocumentType type) => Readers[type].Contains(role);

    /// <summary>The types a role sees, uploads and deletes, in the order of <see cref="DocumentType"/>.</summary>
    public static IReadOnlyList<DocumentType> VisibleTypes(string role) =>
        Enum.GetValues<DocumentType>().Where(t => CanSee(role, t)).ToList();
}

/// <summary>Which files are accepted (spec section 4, AC-88).</summary>
public static class DocumentFiles
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxTitleLength = 100;

    /// <summary>Enough leading bytes to recognise every accepted type.</summary>
    public const int SignatureLength = 8;

    private static readonly (byte[] Signature, string ContentType)[] Known =
    [
        ("%PDF-"u8.ToArray(), "application/pdf"),
        ([0xFF, 0xD8, 0xFF], "image/jpeg"),
        ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png"),
    ];

    public static bool IsValidSize(long bytes) => bytes is > 0 and <= MaxBytes;

    /// <summary>The content type from the file's first bytes; null when it is none of the accepted types.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> head)
    {
        foreach (var (signature, contentType) in Known)
            if (head.StartsWith(signature))
                return contentType;
        return null;
    }
}
