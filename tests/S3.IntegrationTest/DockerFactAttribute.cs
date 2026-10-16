using Docker.DotNet;

namespace S3.IntegrationTest;

public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerAvailable = new(ProbeDocker, isThreadSafe: true);

    public DockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("S3_RUN_DOCKER_TESTS") == "1")
            return;

        if (!DockerAvailable.Value)
            Skip = "Docker Desktop is not running. Start it or set S3_RUN_DOCKER_TESTS=1.";
    }

    private static bool ProbeDocker()
    {
        try
        {
            using var client = new DockerClientBuilder().Build();
            client.System
                .PingAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
