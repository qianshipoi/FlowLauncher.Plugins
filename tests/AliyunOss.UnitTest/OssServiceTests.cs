using Flow.Launcher.Plugin; 

using FlowLauncher.Plugin.AliyunOss;
using FlowLauncher.Plugin.AliyunOss.Services;
using FlowLauncher.Plugin.AliyunOss.Utils;

using Microsoft.Extensions.Configuration;

using Moq;

namespace AliyunOss.UnitTest;

public class OssServiceTests
{
    private static OssService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<OssServiceTests>()
            .Build();

        return new OssService(new Mock<IPublicAPI>().Object, new Settings
        {
            Endpoint = configuration.GetSection("Endpoint").Value,
            AccessKeyId = configuration.GetSection("AccessKeyId").Value,
            AccessKeySecret = configuration.GetSection("AccessKeySecret").Value,
        });

    }

    [Fact]
    public async Task GetBuckets()
    {
        var service = CreateService();

        var bucketNames = await service.GetBucketsAsync("" , CancellationToken.None);

        Assert.NotNull(bucketNames);
        Assert.NotEmpty(bucketNames);
    }

    [Fact]
    public async Task PresignDownloadUrlAsync()
    {
        var service = CreateService();
        var url = await service.PresignDownloadUrlAsync("sss", "sss");
    }

    [Fact]
    public async Task GetObjects()
    {
        //var service = new OssService(new Mock<IPublicAPI>().Object);
        //var bucketNames = await service.GetBucketsAsync("", CancellationToken.None);
        //var objectNames = await service.GetObjectsAsync("web-static-p-bucket", "yimo", CancellationToken.None);
        //Assert.NotNull(objectNames);
        //Assert.NotEmpty(objectNames);
    }

    [Fact]
    public async Task PutObjectsAsync()
    {
        var ss = FileHasher.ComputeMD5("D:\\Desktop\\4.jpg");
        var service = CreateService();
        await service.UploadFileAsync("web-temp-bucket", "111.jpg", "D:\\Desktop\\4.jpg", value => { }, CancellationToken.None);
    }
}
