﻿using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Blueprints.Interfaces.FileStorage;

namespace Apps.MicrosoftOneDrive.Models.Requests;

public class UploadFileRequest : IUploadFileInput
{
    public FileReference File { get; set; }
    
    [Display("Conflict behavior")]
    [StaticDataSource(typeof(ConflictBehaviorDataSourceHandler))]
    public string? ConflictBehavior { get; set; }
}