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
//using Syroot.Windows.IO;


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

            // Create blob operator working on in-blob
            CommonBlob blobOps = new CommonBlob("in");

            // Store binary file from request body as stream
            Stream data = req.Body;

            // Create unique file name as combo of datetime and Guid
            DateTime _date = DateTime.Now;
            var _dateString = _date.ToString("dd-MM-yyyy");
            string fileId = Guid.NewGuid().ToString();
            string fileName = $"{_dateString}-{fileId}.xlsx";

            // Address where blob can be found at
            Uri retUri = await blobOps.uploadFileToBlob(data, fileName);
            Console.WriteLine(retUri.AbsoluteUri);

            // Return id of file in blob 
            var blobname = $"{_dateString}-{fileId}";
            return new OkObjectResult( new {fileId = fileId});
        }
    }

    public static class BlobToFile
    {
        [FunctionName("BlobToFile")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log){
                log.LogInformation("C# Blob to File Function processed a request.");

                // Get id and path from query parameters
                string id = req.Query["id"];
                string path = req.Query["path"];

                // Set default in case no parameters are given (only works for me!)
                if(id==".xlsx"){
                    // No id in parameter should return blank file.
                    id = "download.xlsx";
                }
                if(path==null){
                    // Automatically sets download path to Downloads 
                    //path = new KnownFolder(KnownFolderType.Downloads).Path+"\\";
                }

                // Create blob operator working on 'out' blob
                CommonBlob blobOps = new CommonBlob("out");

                // Bring container instance into this scope
                CloudBlobContainer cloudBlobContainer = blobOps.getLocalCloudBlobContainer();

                // Pull blob with given id from container. If none match, null is returned
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(id);

                // Frodes method of returning a binary stream 
                if(path==null){
                    log.LogInformation("Returning file as binary stream to API client");
                    await blobOps.getListAsStream(id);
                }

                // Download file directly from blob to API client machine
                await cloudBlockBlob.DownloadToFileAsync($"{path}{id}", FileMode.Create);

                // Return destination file was downloaded to
                return new OkObjectResult($"File downloaded. Can be found at path: {path}{id}");
            }
    }

    class CommonBlob
    {
        // string blobConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        string blobConnectionString = "DefaultEndpointsProtocol=https;AccountName=vendorcomparison;AccountKey=jF5PuPFgaNzc+RazIvrecDQdsOaMXoROeou+e7vom23NdOx0HY9l8NnS9ScMVWK76iFZrLN61ARA652RL5++gg==;EndpointSuffix=core.windows.net";
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

            // Get and interpret return value
            var task = getBlobContainer();

            // Only set if created
            var permissinTask = setBlobContainerPermissions();
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
