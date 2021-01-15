public class BuildVersion
{
    public string Version { get; private set; }
    public string SemVersion { get; private set; }
    public string DotNetAsterix { get; private set; }
    public GitVersion GitVersion { get; private set; }
    public string Milestone { get; private set; }
    public string CakeVersion { get; private set; }

    public static BuildVersion Calculate(ICakeContext context, BuildParameters parameters)
    {
        if (context == null)
        {
            throw new ArgumentNullException("context");
        }

        string version = null;
        string semVersion = null;
        string milestone = null;

        context.Information("Calculating Semantic Version");

        GitVersion assertedVersion;

        var versionFile = context.File("./version.json");

        if (context.FileExists("./version.json")) {
            context.Information("Reading version file ({0})", versionFile);
            assertedVersion = context.DeserializeJsonFromFile<GitVersion>(versionFile);
        } else {
            context.GitVersion(new GitVersionSettings{
                OutputType = GitVersionOutput.BuildServer,
                ToolPath = context.Tools.Resolve(context.IsRunningOnWindows() ? "dotnet.exe" : "dotnet"),
                ArgumentCustomization = args => args.Prepend("tool run dotnet-gitversion --")
            });

            assertedVersion = context.GitVersion(new GitVersionSettings{
                OutputType = GitVersionOutput.Json,
                ToolPath = context.Tools.Resolve(context.IsRunningOnWindows() ? "dotnet.exe" : "dotnet"),
                ArgumentCustomization = args => args.Prepend("tool run dotnet-gitversion --")
            });
        }

        version = assertedVersion.MajorMinorPatch;
        semVersion = assertedVersion.FullSemVer;
        milestone = string.Concat("v", version);

        context.Information("Calculated Semantic Version: {0}", semVersion);

        var cakeVersion = typeof(ICakeContext).Assembly.GetName().Version.ToString();

        return new BuildVersion
        {
            Version = version,
            SemVersion = semVersion,
            DotNetAsterix = semVersion.Substring(version.Length).TrimStart('-'),
            Milestone = milestone,
            GitVersion = assertedVersion,
            CakeVersion = cakeVersion
        };
    }
}