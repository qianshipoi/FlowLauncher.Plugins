using System.IO.Pipes;

namespace S3.IntegrationTest;

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("S3_RUN_DOCKER_TESTS") == "1")
            return;

        if (!IsDockerAvailable())
            Skip = "Docker Desktop is not running. Start it or set S3_RUN_DOCKER_TESTS=1.";
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                "docker_engine",
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            pipe.Connect(500);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
