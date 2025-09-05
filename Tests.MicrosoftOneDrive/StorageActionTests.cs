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

            var response = await action.ListFilesInFolderById("folder-id");

            foreach (var item in response.Files)
            {
                Console.WriteLine($"{item.Name} - {item.FileId}");
            }

            Assert.IsNotNull(response);
        }
    }
}
