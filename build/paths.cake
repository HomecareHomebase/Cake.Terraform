public class BuildPaths
{
    public BuildFiles Files { get; private set; }
    public BuildDirectories Directories { get; private set; }

    public static BuildPaths GetPaths(
        ICakeContext context,
        string configuration
        )
    {
        if (context == null)
        {
            throw new ArgumentNullException("context");
        }
        if (string.IsNullOrEmpty(configuration))
        {
            throw new ArgumentNullException("configuration");
        }

        var artifactsDir = (DirectoryPath)context.Directory(context.Argument("artifactsPath", context.EnvironmentVariable("ARTIFACTS_PATH", "./artifacts")));
        var artifactsBinDir = artifactsDir.Combine("bin");
        var artifactsBinFullFx = artifactsBinDir.Combine("net462");
        var artifactsBinNetCore = artifactsBinDir.Combine("netcoreapp3.1");
        var testResultsDir = artifactsDir.Combine("test-results");
        var nugetRoot = artifactsDir.Combine("nuget");
        var cakeDir = artifactsDir.Combine("cake");
        var zipDir = artifactsDir.Combine("zip");
        var artilleryDir = artifactsDir.Combine("artillery.io");
        var analysisResultsDir = artifactsDir.Combine("analysis-results");

        var testCoverageOutputFilePath = testResultsDir.CombineWithFilePath("UnitTestResults.xml");

        // Directories
        var buildDirectories = new BuildDirectories(
            artifactsDir,
            testResultsDir,
            analysisResultsDir,
            nugetRoot,
            artifactsBinDir,
            cakeDir,
            zipDir,
            artilleryDir);

        // Files
        var buildFiles = new BuildFiles(
            context,
            testCoverageOutputFilePath);

        return new BuildPaths
        {
            Files = buildFiles,
            Directories = buildDirectories
        };
    }
}

public class BuildFiles
{
    public FilePath TestCoverageOutputFilePath { get; private set; }

    public BuildFiles(
        ICakeContext context,
        FilePath testCoverageOutputFilePath
        )
    {
        TestCoverageOutputFilePath = testCoverageOutputFilePath;
    }
}

public class BuildDirectories
{
    public DirectoryPath Artifacts { get; }
    public DirectoryPath TestResults { get; }
    public DirectoryPath AnalysisResults { get; }
    public DirectoryPath NugetRoot { get; }
    public DirectoryPath ArtifactsBin { get; }
    public DirectoryPath CakeScripts { get; }
    public DirectoryPath ZipFiles { get; }
    public DirectoryPath Artillery { get; }
    public ICollection<DirectoryPath> ToClean { get; }

    public BuildDirectories(
        DirectoryPath artifactsDir,
        DirectoryPath testResultsDir,
        DirectoryPath analysisResultsDir,
        DirectoryPath nugetRoot,
        DirectoryPath artifactsBinDir,
        DirectoryPath cakeDir,
        DirectoryPath zipDir,
        DirectoryPath artilleryDir
        )
    {
        Artifacts = artifactsDir;
        TestResults = testResultsDir;
        AnalysisResults = analysisResultsDir;
        NugetRoot = nugetRoot;
        ArtifactsBin = artifactsBinDir;
        CakeScripts = cakeDir;
        ZipFiles = zipDir;
        Artillery = artilleryDir;
        ToClean = new[] {
            Artifacts,
            TestResults,
            AnalysisResults,
            NugetRoot,
            ArtifactsBin,
            CakeScripts,
            ZipFiles,
            Artillery
        };
    }
}