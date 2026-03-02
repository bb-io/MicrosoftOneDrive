using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Tests.MicrosoftOneDrive.Base;

namespace Tests.MicrosoftOneDrive;

[TestClass]
public class HandlerTests : TestBase
{
    [TestMethod]
    public async Task FolderDataHandler_Issuccess()
    {
        var handlet = new FolderDataSourceHandler(InvocationContext);

        var result = await handlet.GetFolderContentAsync(new Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems.FolderContentDataSourceContext {  }, CancellationToken.None);

        foreach (var folder in result)
        {
            Console.WriteLine($"{folder.Id}-{folder.DisplayName}");
        }
        Assert.IsNotNull(result);
    }


    [TestMethod]
    public async Task FileDataHandler_Issuccess()
    {
        var handlet = new FileDataSourceHandler(InvocationContext);

        var result = await handlet.GetFolderContentAsync(new Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems.FolderContentDataSourceContext { }, CancellationToken.None);

        foreach (var file in result)
        {
            Console.WriteLine($"{file.Id}-{file.DisplayName}");
        }
        Assert.IsNotNull(result);
    }
}
