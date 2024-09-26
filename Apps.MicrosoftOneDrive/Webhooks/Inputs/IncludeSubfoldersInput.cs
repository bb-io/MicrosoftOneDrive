using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Webhooks.Inputs;

public class IncludeSubfoldersInput
{
    [Display("Include subfolders", Description = "Include changes in subfolders in the list of changed items. If not specified, changes in subfolders are not included.")]
    public bool? IncludeSubfolders { get; set; }
}