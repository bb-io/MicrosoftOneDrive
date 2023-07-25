using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Models.Folder.Requests;

public class CreateFolderInParentFolderWithIdRequest
{
    [Display("Parent folder ID")] 
    public string ParentFolderId { get; set; }
    
    [Display("Folder name")]
    public string FolderName { get; set; }
}