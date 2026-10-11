using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AthleteDay.Api.Documents;
using AthleteDay.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AthleteDay.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DocumentTests(ApiFactory api)
{
    private static readonly byte[] Pdf = "%PDF-1.4\n% Synthetic test document, not a real person\n%%EOF\n"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];

    private static async Task<HttpResponseMessage> Upload(HttpClient client, int athleteId, string type, string title, byte[] bytes,
        string fileName = "document.pdf", string contentType = "application/pdf")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(type), "type");
        form.Add(new StringContent(title), "title");
        return await client.PostAsync($"/api/athletes/{athleteId}/documents", form);
    }

    private static string List(int athleteId) => $"/api/athletes/{athleteId}/documents";
    private static string One(int athleteId, int documentId) => $"/api/athletes/{athleteId}/documents/{documentId}";

    private async Task<int> CountAsync(int athleteId)
    {
        var count = 0;
        await api.WithDbAsync(async db => count = await db.AthleteDocuments.CountAsync(d => d.AthleteId == athleteId));
        return count;
    }

    [Fact]
    [Trait("AC", "AC-85")]
    public async Task Staff_upload_a_document_with_its_metadata()
    {
        var athlete = await api.CreateAthleteAsync();

        var response = await Upload(api.ClientAs(Roles.Coach), athlete.Id, "Certificate", "  Anti-doping course 2026 ", Pdf,
            fileName: "anti-doping.pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.ReadAsync<DocumentDto>();
        Assert.Equal(athlete.Id, document.AthleteId);
        Assert.Equal(DocumentType.Certificate, document.Type);
        Assert.Equal("Anti-doping course 2026", document.Title);
        Assert.Equal("anti-doping.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(Pdf.Length, document.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Pdf)), document.Sha256);
        Assert.Equal("coach", document.UploadedBy);

        await api.WithDbAsync(async db =>
        {
            var stored = await db.AthleteDocuments.SingleAsync(d => d.Id == document.Id);
            Assert.True(Guid.TryParse(stored.BlobName, out _), "the blob name is random and carries no personal data");
            await using var scope = api.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            await using var content = await store.OpenReadAsync(stored.BlobName, CancellationToken.None);
            Assert.NotNull(content);
        });
    }

    [Fact]
    [Trait("AC", "AC-86")]
    public async Task A_role_cannot_upload_a_type_it_may_not_see()
    {
        var athlete = await api.CreateAthleteAsync();

        var coachMedical = await Upload(api.ClientAs(Roles.Coach), athlete.Id, "Medical", "Knee scan", Pdf);
        var physioId = await Upload(api.ClientAs(Roles.Physio), athlete.Id, "GovernmentId", "Passport", Pdf);
        var adminCertificate = await Upload(api.ClientAs(Roles.Admin), athlete.Id, "Certificate", "Licence", Pdf);

        Assert.Equal(HttpStatusCode.Forbidden, coachMedical.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, physioId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminCertificate.StatusCode);
        Assert.Equal(0, await CountAsync(athlete.Id));
    }

    [Theory]
    [Trait("AC", "AC-86")]
    [InlineData(Roles.Nutrition)]
    [InlineData(Roles.Kitchen)]
    [InlineData(Roles.Athlete)]
    public async Task Other_roles_get_403_on_every_document_endpoint(string role)
    {
        var athlete = await api.CreateAthleteAsync();
        var created = await (await Upload(api.ClientAs(Roles.Coach), athlete.Id, "General", "Team rules", Pdf)).ReadAsync<DocumentDto>();
        var client = api.ClientAs(role, role == Roles.Athlete ? athlete.Code : null);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(List(athlete.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(client, athlete.Id, "General", "Mine", Pdf)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(One(athlete.Id, created.Id) + "/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(One(athlete.Id, created.Id))).StatusCode);
        Assert.Equal(1, await CountAsync(athlete.Id));
    }

    [Fact]
    [Trait("AC", "AC-87")]
    [Trait("AC", "AC-92")]
    public async Task Each_role_lists_only_its_types_and_other_documents_do_not_exist_for_it()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);
        var physio = api.ClientAs(Roles.Physio);
        var admin = api.ClientAs(Roles.Admin);
        var medical = await (await Upload(physio, athlete.Id, "Medical", "MRI report", Pdf)).ReadAsync<DocumentDto>();
        var certificate = await (await Upload(coach, athlete.Id, "Certificate", "Coaching licence", Pdf)).ReadAsync<DocumentDto>();
        var passport = await (await Upload(admin, athlete.Id, "GovernmentId", "Passport", Png, "passport.png", "image/png")).ReadAsync<DocumentDto>();

        var coachList = await coach.GetFromJsonAsync<List<DocumentDto>>(List(athlete.Id));
        var physioList = await physio.GetFromJsonAsync<List<DocumentDto>>(List(athlete.Id));
        var adminList = await admin.GetFromJsonAsync<List<DocumentDto>>(List(athlete.Id));

        Assert.Equal([certificate.Id], coachList!.Select(d => d.Id));
        Assert.Equal([certificate.Id, medical.Id], physioList!.Select(d => d.Id)); // newest first
        Assert.Equal([passport.Id], adminList!.Select(d => d.Id));

        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync(One(athlete.Id, medical.Id) + "/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.DeleteAsync(One(athlete.Id, medical.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await physio.GetAsync(One(athlete.Id, passport.Id) + "/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync(One(athlete.Id, certificate.Id))).StatusCode);
        // A document is only found under its own athlete.
        var other = await api.CreateAthleteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync(One(other.Id, certificate.Id) + "/content")).StatusCode);
        Assert.Equal(3, await CountAsync(athlete.Id));

        // The admin needs the athlete list to pick an athlete (names and codes only).
        var athletes = await admin.GetFromJsonAsync<List<AthleteDto>>("/api/athletes");
        Assert.Contains(athletes!, a => a.Id == athlete.Id && a.Code == athlete.Code);
    }

    public static TheoryData<string, string, int, string, string, string> InvalidUploads => new()
    {
        { "empty file", "General", 0, "%PDF-", "a.pdf", "application/pdf" },
        { "over 10 MB", "General", 10 * 1024 * 1024 + 1, "%PDF-", "a.pdf", "application/pdf" },
        { "text named .pdf", "General", 200, "hello", "a.pdf", "application/pdf" },
        { "executable", "General", 200, "MZ", "a.png", "image/png" },
        { "unknown type", "Passport", 200, "%PDF-", "a.pdf", "application/pdf" },
    };

    [Theory]
    [Trait("AC", "AC-88")]
    [MemberData(nameof(InvalidUploads))]
    public async Task Invalid_files_are_rejected_and_nothing_is_stored(string _, string type, int size, string head, string fileName, string contentType)
    {
        var athlete = await api.CreateAthleteAsync();
        var bytes = new byte[size];
        System.Text.Encoding.ASCII.GetBytes(head).AsSpan(0, Math.Min(head.Length, size)).CopyTo(bytes);

        var response = await Upload(api.ClientAs(Roles.Coach), athlete.Id, type, "Title", bytes, fileName, contentType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(athlete.Id));
    }

    [Theory]
    [Trait("AC", "AC-88")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A title that is far too long, A title that is far too long, A title that is far too long, A title that is far")]
    public async Task A_title_is_required_and_short(string title)
    {
        var athlete = await api.CreateAthleteAsync();

        var response = await Upload(api.ClientAs(Roles.Coach), athlete.Id, "General", title, Pdf);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(athlete.Id));
    }

    [Fact]
    [Trait("AC", "AC-89")]
    public async Task A_download_returns_the_stored_bytes_as_an_attachment()
    {
        var athlete = await api.CreateAthleteAsync();
        var physio = api.ClientAs(Roles.Physio);
        var created = await (await Upload(physio, athlete.Id, "Medical", "Scan", Png, "knee scan.png", "image/png")).ReadAsync<DocumentDto>();

        var response = await physio.GetAsync(One(athlete.Id, created.Id) + "/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("knee scan.png", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    [Trait("AC", "AC-90")]
    public async Task Deleting_removes_metadata_and_file_and_every_access_is_audited()
    {
        var athlete = await api.CreateAthleteAsync();
        var coach = api.ClientAs(Roles.Coach);
        var created = await (await Upload(coach, athlete.Id, "General", "Travel plan", Pdf)).ReadAsync<DocumentDto>();
        string blobName = "";
        await api.WithDbAsync(async db => blobName = (await db.AthleteDocuments.SingleAsync(d => d.Id == created.Id)).BlobName);

        (await coach.GetAsync(List(athlete.Id))).EnsureSuccessStatusCode();
        (await coach.GetAsync(One(athlete.Id, created.Id) + "/content")).EnsureSuccessStatusCode();
        var deleted = await coach.DeleteAsync(One(athlete.Id, created.Id));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync(One(athlete.Id, created.Id) + "/content")).StatusCode);
        Assert.Equal(0, await CountAsync(athlete.Id));
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            Assert.Null(await store.OpenReadAsync(blobName, CancellationToken.None));
        }

        await api.WithDbAsync(async db =>
        {
            var actions = await db.AuditEntries
                .Where(a => a.Resource == "Document" && a.AthleteId == athlete.Id)
                .OrderBy(a => a.Id)
                .Select(a => a.Action)
                .ToListAsync();
            Assert.Equal(["Changed", "Viewed", "Viewed", "Changed"], actions);
        });
    }
}
