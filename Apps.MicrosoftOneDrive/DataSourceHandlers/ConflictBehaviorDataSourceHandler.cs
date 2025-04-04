using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.MicrosoftOneDrive.DataSourceHandlers;

public class ConflictBehaviorDataSourceHandler : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        var conflictBehaviors = new List<DataSourceItem>()
        {
            new DataSourceItem("fail", "Fail uploading"),
            new DataSourceItem("replace", "Replace file"),
            new DataSourceItem("rename", "Rename file"),
        };
        return conflictBehaviors;
    }
}