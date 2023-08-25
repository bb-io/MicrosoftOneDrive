using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.MicrosoftOneDrive.DataSourceHandlers;

public class ConflictBehaviorDataSourceHandler : BaseInvocable, IDataSourceHandler
{
    public ConflictBehaviorDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public Dictionary<string, string> GetData(DataSourceContext context)
    {
        var conflictBehaviors = new Dictionary<string, string>
        {
            { "fail", "Fail uploading" },
            { "replace", "Replace file" },
            { "rename", "Rename file" }
        };
        return conflictBehaviors;
    }
}