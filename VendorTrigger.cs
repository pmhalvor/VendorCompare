using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage;
using Syroot.Windows.IO;


namespace vendor
{
    public static class FileToBlob
    {
        [FunctionName("FileToBlob")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Vendor File In triggered. Attemping upload of file from body of API call...");

            CommonBlob blobOps = new CommonBlob("in");
            Stream data = req.Body;

            DateTime _date = DateTime.Now;
            var _dateString = _date.ToString("dd-MM-yyyy");
            string fileId = Guid.NewGuid().ToString();
            string fileName = $"{_dateString}-{fileId}.xlsx";

            Uri retUri = await blobOps.uploadFileToBlob(data, fileName);

            Console.WriteLine(retUri.AbsoluteUri);

            return new OkObjectResult( new {fileId = fileId});
        }
    }

    public static class BlobToFile
    {
        [FunctionName("BlobToFile")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log){
                log.LogInformation("C# HTTP trigger function processed a request.");

                // Get id from query parameters
                string id = req.Query["id"] + ".xlsx";

                //                 string path = req.Query["path"];
                // Set default in case no parameters are given (only works for me!)
                if(id==".xlsx"){
                    id = "download.xlsx";
                }
                // if(path==null){
                //     path = "C:/Users/perha/Downloads/";
                //     path = new KnownFolder(KnownFolderType.Downloads).Path;
                // }

                // Create instance of our blob container
                CommonBlob blobOps = new CommonBlob("out");
                CloudBlobContainer cloudBlobContainer = blobOps.getLocalCloudBlobContainer();
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(id);
            
                // Pull file with id given in query from blob container
                // Stream file = blobOps.getBankruptList(id);

                // This should download the file in the browser calling the API
                // return new FileStreamResult(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"){  
                //    FileDownloadName = id
                // };
                // System.Diagnostics.Process.Start($"{path}{id}");
                return null;
            }
    }

    class CommonBlob
    {
        string blobConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        string blobContainerName;
        CloudStorageAccount blobStorageAccount;
        CloudBlobClient blobClient;
        CloudBlobContainer cloudBlobContainer;


        public CommonBlob(string inOrOut)
        {
            if(inOrOut=="in"){
                blobContainerName  = "vendorcomparison-in";
            }else if(inOrOut=="out"){
                blobContainerName  = "vendorcomparison-out";
            }
            blobClient = getBlobConnection();

            // Get and interpreter return value
            getBlobContainer();
            // Only set if created
            setBlobContainerPermissions();
        }

        public async Stream getBankruptList(string id){
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(id);
            Stream file = new MemoryStream();
            cloudBlockBlob.DownloadToStreamAsync(file);
            //cloudBlockBlob.DownloadToStreamAsync(file);
            //cloudBlockBlob.DownloadToFileAsync($"{path}{id}", FileMode.Create);
            return file;
        }

        public CloudBlobContainer getLocalCloudBlobContainer(){
            return cloudBlobContainer;
        }


        public async Task<Uri> uploadFileToBlob(Stream inFile, string destFileName)
        {
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(destFileName);
            await cloudBlockBlob.UploadFromStreamAsync(inFile);
            return cloudBlockBlob.Uri;
      
        }

 
        private async Task getBlobContainer()
        {

            cloudBlobContainer = blobClient.GetContainerReference(blobContainerName);
            
            await cloudBlobContainer.CreateIfNotExistsAsync();

        }

 
        private CloudBlobClient getBlobConnection()
        {
            if (CloudStorageAccount.TryParse(blobConnectionString, out blobStorageAccount))
            {
                return blobStorageAccount.CreateCloudBlobClient();
            }
            else
            {
                return null;
            }
        }

 
        private async Task setBlobContainerPermissions()
        {
            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Off
            };

            await cloudBlobContainer.SetPermissionsAsync(permissions);        
        } 

    }

}
