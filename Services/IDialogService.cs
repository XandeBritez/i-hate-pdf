namespace IHatePdf.Services;

/// <summary>Abstrai dialogos do shell para manter os ViewModels testaveis.</summary>
public interface IDialogService
{
    string[]? OpenFiles(string title, string filter, bool multiselect = true);
    string? SaveFile(string title, string filter, string suggestedFileName);
    string? PickFolder(string title);
    void ShowError(string title, string message);
    bool Confirm(string title, string message);
}
