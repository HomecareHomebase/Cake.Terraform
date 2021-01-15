public class SonarQubeCredentials
{
    public string Url { get; }
    public string Token { get; }
    public bool Enabled { get; }

    public SonarQubeCredentials(string url, string token, bool enabled)
    {
        Url = url;
        Token = token;
        Enabled = enabled;
    }

    public static SonarQubeCredentials GetSonarQubeCredentials(ICakeContext context)
    {
        return new SonarQubeCredentials(
            context.Argument("sonarQubeUrl", context.EnvironmentVariable("SONARQUBE_URL", "https://sonarqube.hchb.com")),
            context.Argument("sonarQubeToken", context.EnvironmentVariable("SONARQUBE_TOKEN")),
            context.Argument("sonarQubeEnabled", context.EnvironmentVariable("SONARQUBE_ENABLED", true)));
    }
}

public class DockerCredentials
{
    public string Registry { get; }
    public string RegistryUrl { get; }
    public string Username { get; }
    public string Password { get; }

    public DockerCredentials(string registry, string registryUrl, string username, string password)
    {
        Registry = registry;
        RegistryUrl = !string.IsNullOrEmpty(registryUrl) ? registryUrl : $"https://{registry}";
        Username = username;
        Password = password;
    }

    public static DockerCredentials GetDockerCredentials(ICakeContext context)
    {
        return new DockerCredentials(
            context.Argument("dockerRegistry", context.EnvironmentVariable("DOCKER_REGISTRY", "hchbprod.azurecr.io")),
            context.Argument("dockerRegistryUrl", context.EnvironmentVariable("DOCKER_REGISTRYURL")),
            context.Argument("dockerUsername", context.EnvironmentVariable("DOCKER_USERNAME")),
            context.Argument("dockerPassword", context.EnvironmentVariable("DOCKER_PASSWORD")));
    }
}

public class AquaCredentials
{
    public string Url { get; }
    public string Username { get; }
    public string Password { get; }
    public string ScannerImage { get; }
    public bool Enabled { get; }
    public bool FailOnPolicyViolation { get; }

    public AquaCredentials(string url, string username, string password, string scannerImage, bool enabled, bool failOnPolicyViolation)
    {
        Url = url;
        Username = username;
        Password = password;
        ScannerImage = scannerImage;
        Enabled = enabled;
        FailOnPolicyViolation = failOnPolicyViolation;
    }

    public static AquaCredentials GetAquaCredentials(ICakeContext context)
    {
        return new AquaCredentials(
            context.Argument("aquaUrl", context.EnvironmentVariable("AQUA_URL", "http://aquasvc.pipeline.hchb.com:8080")),
            context.Argument("aquaUsername", context.EnvironmentVariable("AQUA_USERNAME", "scanner")),
            context.Argument("aquaPassword", context.EnvironmentVariable("AQUA_PASSWORD")),
            context.Argument("aquaScannerImage", context.EnvironmentVariable("AQUA_SCANNERIMAGE", "hchbprod.azurecr.io/aquasec.com/scanner:latest")),
            context.Argument("aquaEnabled", context.EnvironmentVariable("AQUA_ENABLED", true)),
            context.Argument("aquaFailOnPolicyViolation", context.EnvironmentVariable("AQUA_FAILONPOLICYVIOLATION", true))
            );
    }
}

public class NuGetCredentials
{
    public string Source { get; }
    public string ApiKey { get; }
    public bool SkipDuplicates { get; }

    public NuGetCredentials(string source, string apiKey, bool skipDuplicates)
    {
        Source = source;
        ApiKey = apiKey;
        SkipDuplicates = skipDuplicates;
    }

    public static NuGetCredentials GetNuGetCredentials(ICakeContext context)
    {
        return new NuGetCredentials(
            context.Argument("nugetSource", context.EnvironmentVariable("NUGET_SOURCE", "https://pkgs.dev.azure.com/hchb/_packaging/internal/nuget/v3/index.json")),
            context.Argument("nugetApiKey", context.EnvironmentVariable("NUGET_APIKEY")),
            context.Argument("nugetSkipDuplicates", false)
            );
    }
}