using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Models.Folder.Requests;

public class CreateFolderAtPathRequest
{
    [Display("Path relative to drive's root")] 
    public string? PathRelativeToRoot { get; set; }
    
    [Display("Folder name")]
    public string FolderName { get; set; }
}