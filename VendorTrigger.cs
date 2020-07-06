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

                // Get id and path from query parameters
                string id = req.Query["id"] + ".xlsx";
                string path = req.Query["path"];
                // Set default in case no parameters are given (only works for me!)
                if(id==".xlsx"){
                    // No id in parameter should return blank file.
                    id = "download.xlsx";
                }
                if(path==null){
                    // Automatically sets download path to Downloads 
                    path = new KnownFolder(KnownFolderType.Downloads).Path;
                }

                // Create blob operator working on 'out' blob
                CommonBlob blobOps = new CommonBlob("out");

                // Bring container instance into this scope
                CloudBlobContainer cloudBlobContainer = blobOps.getLocalCloudBlobContainer();

                // Pull blob with given id from container. If none match, null is returned
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(id);

                // return new FileContentResult(binaryfile.ToArray(), cloudBlockBlob.Properties.ContentType);

                // Frodes method of returning a binary stream 
                // Task<IActionResult> task = blobOps.getListAsStream(id);
                // return task;

                // One way of downloading directly
                await cloudBlockBlob.DownloadToFileAsync($"{path}{id}", FileMode.Create);
 
                // Downloading first as stream, then converting to file in C# (extra steps for more usability?)
                // return new FileStreamResult(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"){  
                //    FileDownloadName = id
                // };

                // Could return path which file ended up in as plain text
                return new OkObjectResult($"File downloaded at path: {path}{id}");
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

        public async Task<IActionResult> getListAsStream(string id){
            // Pull object with this id from out blob
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(id);

            // New stream instance which file will be downloaded to
            MemoryStream file = new MemoryStream();

            // Download file as stream from blob
            await cloudBlockBlob.DownloadToStreamAsync(file);

            // Return stream as binary data for robot to interpret 
            return new FileContentResult(file.ToArray(), cloudBlockBlob.Properties.ContentType);
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
