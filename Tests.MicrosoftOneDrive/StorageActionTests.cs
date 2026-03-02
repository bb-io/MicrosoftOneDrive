using Apps.MicrosoftOneDrive.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.MicrosoftOneDrive.Base;

namespace Tests.MicrosoftOneDrive
{
    [TestClass]
    public class StorageActionTests :TestBase
    {
        [TestMethod]
        public async Task ListFilesInFolderById_ShouldReturnFileMetadata()
        {
            var action = new StorageActions(InvocationContext, FileManager);

            var response = await action.ListFilesInFolderById("016FYB3YN6Y2GOVW7725BZO354PWSELRRZ");

            foreach (var item in response.Files)
            {
                Console.WriteLine($"{item.Name} - {item.FileId}");
            }

            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task DownloadAllFilesInFolder_ShouldReturnFileMetadata()
        {
            var action = new StorageActions(InvocationContext, FileManager);

            var response = await action.DownloadAllFilesInFolder( new Apps.MicrosoftOneDrive.Models.Requests.DownloadAllFilesInFolderRequest { FolderId = "016FYB3YK6XMOTS275MVC3CW6PJNDKGRN2" });

            foreach (var item in response.Files)
            {
                Console.WriteLine($"{item.Name}");
            }

            Assert.IsNotNull(response);
        }
    }
}
