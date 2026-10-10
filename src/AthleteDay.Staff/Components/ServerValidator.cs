using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AthleteDay.Staff.Components;

/// <summary>
/// Shows validation errors returned by the API next to the matching form fields. Normally the
/// shared DataAnnotations rules catch everything in the browser; this covers server-only checks
/// such as an unknown athlete.
/// </summary>
public sealed class ServerValidator : ComponentBase, IDisposable
{
    private ValidationMessageStore? _store;

    [CascadingParameter] private EditContext? EditContext { get; set; }

    protected override void OnInitialized()
    {
        if (EditContext is null)
            throw new InvalidOperationException($"{nameof(ServerValidator)} must be placed inside an EditForm.");

        _store = new ValidationMessageStore(EditContext);
        EditContext.OnValidationRequested += OnValidationRequested;
        EditContext.OnFieldChanged += OnFieldChanged;
    }

    public void Show(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var (field, messages) in errors)
            _store!.Add(EditContext!.Field(field), messages);
        EditContext!.NotifyValidationStateChanged();
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e) => _store!.Clear();

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e) => _store!.Clear(e.FieldIdentifier);

    public void Dispose()
    {
        if (EditContext is null) return;
        EditContext.OnValidationRequested -= OnValidationRequested;
        EditContext.OnFieldChanged -= OnFieldChanged;
    }
}
