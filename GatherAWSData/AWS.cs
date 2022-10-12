using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Model.Internal.MarshallTransformations;
using System.Threading.Channels;

namespace GatherAWSData;

static internal class AWS
{
    public static async Task<string?[]> DownloadData(AWSConfig config, string destinationDirectory, string prefix, string areaName, int year, int month, int date)
    {
        if(!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }
        string?[] fileNames = Array.Empty<string>();
        using (var client = new AmazonS3Client(config.AwsKey, config.AwsSecret, Amazon.RegionEndpoint.USWest2))
        {
            var awsNames = await CreateAWSDataSourceNames(config, client, prefix, areaName, year, month, date);
            fileNames = CreateFileNames(destinationDirectory, awsNames);
            var cancel = new CancellationToken();
            for (int i = 0; i < awsNames.Length; i++)
            {
                try
                {
                    var response = await client.GetObjectAsync(config.BucketName, awsNames[i]);
                    await response.WriteResponseStreamToFileAsync(fileNames[i], false, cancel);
                }
                catch
                {
                    fileNames[i] = null;
                }
            }
        }
        return fileNames;
    }

    private static async Task<string[]> CreateAWSDataSourceNames(AWSConfig config, AmazonS3Client client, string prefix, 
        string areaName, int year, int month, int date)
    {
        var p = $"{prefix}/{areaName}/{year}/{month:00}/{date:00}";
        var request = new ListObjectsRequest()
        {
            BucketName = config.BucketName,
            Prefix = p
        };
        var namesRequest = await client.ListObjectsAsync(request);
        return namesRequest.S3Objects.Where(x => !x.Key.Equals(prefix)).Select(x => x.Key).ToArray();
    }

    private static string[] CreateFileNames(string directory, string[] awsNames)
    {
        return awsNames.Select(x => Path.Combine(directory, x.Split('/').Last())).ToArray();
    }
}
