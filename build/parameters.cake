#load "./paths.cake"
#load "./version.cake"
#load "./credentials.cake"

using System.Collections.ObjectModel;

public class BuildParameters
{
    private List<DockerImageConfiguration> dockerImages = new List<DockerImageConfiguration>();
    private List<FilePath> nugetPackageProjects = new List<FilePath>();

    public string Target { get; }
    public string Configuration { get; }
    public bool IsLocalBuild { get; }
    public bool IsRunningOnUnix { get; }
    public bool IsRunningOnWindows { get; }
    public bool IsPullRequest { get; }
    public int BuildId { get; set; }
    public SonarQubeCredentials SonarQube { get; }
    public DockerCredentials Docker { get; }
    public AquaCredentials Aqua { get; }
    public NuGetCredentials NuGet { get; }
    public BuildVersion Version { get; }
    public BuildPaths Paths { get; }
    public bool SonarQubeError { get; set; }
    public string SonarQubeProject { get; private set; }
    public DotNetCoreMSBuildSettings MSBuildSettings { get; }
    public FilePath Solution { get; private set; }
    public ReadOnlyCollection<DockerImageConfiguration> DockerImages => dockerImages.AsReadOnly();
    public ReadOnlyCollection<FilePath> NuGetPackageProjects => nugetPackageProjects.AsReadOnly();
    public List<IIssue> Issues { get; }
    public bool AquaPolicyFailure { get; set; }

    public BuildParameters (ISetupContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException("context");
        }

        var buildSystem = context.BuildSystem();

        Target = context.Argument("target", "Default");
        Configuration = context.Argument("configuration", "Release");
        IsLocalBuild = buildSystem.IsLocalBuild;
        IsRunningOnUnix = context.IsRunningOnUnix();
        IsRunningOnWindows = context.IsRunningOnWindows();
        IsPullRequest = buildSystem.AzurePipelines.Environment.Build.Reason.Equals("PullRequest", StringComparison.OrdinalIgnoreCase);
        SonarQube = SonarQubeCredentials.GetSonarQubeCredentials(context);
        Docker = DockerCredentials.GetDockerCredentials(context);
        Aqua = AquaCredentials.GetAquaCredentials(context);
        NuGet = NuGetCredentials.GetNuGetCredentials(context);
        Version = BuildVersion.Calculate(context, this);
        Paths = BuildPaths.GetPaths(context, Configuration);
        BuildId = buildSystem.AzurePipelines.Environment.Build.Id;
        MSBuildSettings = new DotNetCoreMSBuildSettings()
                            .WithProperty("Version", Version.SemVersion)
                            .WithProperty("AssemblyVersion", Version.Version)
                            .WithProperty("FileVersion", Version.Version);
        
        this.Issues = new List<IIssue>();
    }

    public BuildParameters ForSolution(FilePath solution)
    {
        Solution = solution ?? throw new ArgumentNullException(nameof(solution));
        return this;
    }

    public BuildParameters ForSonarQubeProject(string name)
    {
        SonarQubeProject = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    public BuildParameters WithDockerImage(string name, FilePath dockerfile, FilePath project)
    {
        if (string.IsNullOrEmpty(name)) {
            throw new ArgumentNullException(nameof(name));
        }

        if (dockerfile == null) {
            throw new ArgumentNullException(nameof(dockerfile));
        }

        if (project == null) {
            throw new ArgumentNullException(nameof(project));
        }

        var tags = new List<string>();
        var baseTag = $"{this.Docker.Registry}/{name}";

        if (this.IsLocalBuild) {
            tags.Add($"{baseTag}:latest");
            tags.Add($"{baseTag}:_sha.{this.Version.GitVersion.Sha}");
        } else {
            tags.Add($"{baseTag}:{this.Version.GitVersion.FullSemVer}");
            tags.Add($"{baseTag}:{this.BuildId}");
            tags.Add($"{baseTag}:_sha.{this.Version.GitVersion.Sha}_buildid.{this.BuildId}");
        }

        this.dockerImages.Add(new DockerImageConfiguration(name, dockerfile, project, tags.ToArray()));
        return this;
    }

    public BuildParameters WithNuGetPackageProject(FilePath projectFile)
    {
        if (projectFile == null) {
            throw new ArgumentNullException(nameof(projectFile));
        }

        this.nugetPackageProjects.Add(projectFile);
        return this;
    }
}

public class DockerImageConfiguration
{
    public string Name { get; }
    public FilePath Dockerfile { get; }
    public FilePath Project { get; }
    public string[] Tags { get; }

    public DockerImageConfiguration(string name, FilePath dockerfile, FilePath project, string[] tags)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.Dockerfile = dockerfile ?? throw new ArgumentNullException(nameof(dockerfile));
        this.Project = project ?? throw new ArgumentNullException(nameof(project));
        this.Tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }
}