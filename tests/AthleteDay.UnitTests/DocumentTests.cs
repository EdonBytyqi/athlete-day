using AthleteDay.Contracts;
using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public sealed class DocumentTests
{
    [Theory]
    [Trait("AC", "AC-86")]
    [InlineData(Roles.Physio, DocumentType.Medical, true)]
    [InlineData(Roles.Coach, DocumentType.Medical, false)]
    [InlineData(Roles.Admin, DocumentType.Medical, false)]
    [InlineData(Roles.Admin, DocumentType.GovernmentId, true)]
    [InlineData(Roles.Coach, DocumentType.GovernmentId, false)]
    [InlineData(Roles.Physio, DocumentType.GovernmentId, false)]
    [InlineData(Roles.Coach, DocumentType.Certificate, true)]
    [InlineData(Roles.Physio, DocumentType.Certificate, true)]
    [InlineData(Roles.Admin, DocumentType.Certificate, false)]
    [InlineData(Roles.Coach, DocumentType.General, true)]
    [InlineData(Roles.Physio, DocumentType.General, true)]
    [InlineData(Roles.Nutrition, DocumentType.General, false)]
    [InlineData(Roles.Kitchen, DocumentType.Certificate, false)]
    [InlineData(Roles.Athlete, DocumentType.General, false)]
    public void Each_document_type_is_need_to_know(string role, DocumentType type, bool allowed) =>
        Assert.Equal(allowed, DocumentAccess.CanSee(role, type));

    [Fact]
    [Trait("AC", "AC-86")]
    public void A_role_sees_exactly_its_types()
    {
        Assert.Equal([DocumentType.Medical, DocumentType.Certificate, DocumentType.General], DocumentAccess.VisibleTypes(Roles.Physio));
        Assert.Equal([DocumentType.Certificate, DocumentType.General], DocumentAccess.VisibleTypes(Roles.Coach));
        Assert.Equal([DocumentType.GovernmentId], DocumentAccess.VisibleTypes(Roles.Admin));
        Assert.Empty(DocumentAccess.VisibleTypes(Roles.Nutrition));
        Assert.Empty(DocumentAccess.VisibleTypes(Roles.Kitchen));
        Assert.Empty(DocumentAccess.VisibleTypes(Roles.Athlete));
    }

    public static TheoryData<byte[], string?> Signatures => new()
    {
        { "%PDF-1.7\n"u8.ToArray(), "application/pdf" },
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10], "image/jpeg" },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], "image/png" },
        { "%PD"u8.ToArray(), null },
        { "hello, this is text"u8.ToArray(), null },
        { "MZ\x90\0"u8.ToArray(), null },
        { [], null },
    };

    [Theory]
    [Trait("AC", "AC-88")]
    [MemberData(nameof(Signatures))]
    public void A_file_type_is_recognised_by_its_first_bytes(byte[] head, string? expected) =>
        Assert.Equal(expected, DocumentFiles.DetectContentType(head));

    [Theory]
    [Trait("AC", "AC-88")]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10 * 1024 * 1024, true)]
    [InlineData(10 * 1024 * 1024 + 1, false)]
    public void A_file_is_between_1_byte_and_10_MB(long size, bool valid) =>
        Assert.Equal(valid, DocumentFiles.IsValidSize(size));
}
