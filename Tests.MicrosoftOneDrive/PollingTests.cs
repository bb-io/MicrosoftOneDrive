using Apps.MicrosoftOneDrive.Webhooks;
using Apps.MicrosoftOneDrive.Webhooks.Inputs;
using Apps.MicrosoftOneDrive.Webhooks.Memory;
using Blackbird.Applications.Sdk.Common.Polling;
using Tests.MicrosoftOneDrive.Base;

namespace Tests.MicrosoftOneDrive
{
    [TestClass]
    public class PollingTests : TestBase
    {
        [TestMethod]
        public async Task OnFileAddedOrUpdated_IsSuccess()
        {
            var polling = new PollingList(InvocationContext);
            var oldDate = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            var response = await polling.OnFilesCreatedOrUpdated(
                new PollingEventRequest<DeltaTokenMemory> { PollingTime= oldDate, Memory = null } , 
                new FolderInput { },
                new IncludeSubfoldersInput { });

            var files = response.Result.Files;
            foreach (var file in files)
            {
                Console.WriteLine($"Memory: {response.Result.Files}");
            }
           
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task OnFolderAddedOrUpdated_IsSuccess()
        {
            var polling = new PollingList(InvocationContext);
            var oldDate = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            var response = await polling.OnFoldersCreatedOrUpdated(
                new PollingEventRequest<DeltaTokenMemory>
                {
                    PollingTime = oldDate,
                    Memory = new DeltaTokenMemory { DeltaToken = "NDslMjM0OyUyMzE7MztkZDQ2ODc5YS1iOWM0LTQ4YTUtOWJlOS1hZGJlNjVhMDQyZTE7NjM4ODA1MDQxNjEyNzcwMDAwOzY3MDYxNzc5MzslMjM7JTIzOyUyMzA7JTIz" },   
                }, new FolderInput { });

            var files = response.Result.Folders;
            foreach (var file in files)
            {
                Console.WriteLine($"Memory: {response.Result.Folders}");
            }

            Assert.IsNotNull(response);
        }


    }
}
