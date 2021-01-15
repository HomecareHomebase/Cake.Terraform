// Install addins.
#addin nuget:?package=Cake.AquaScanner&version=0.1.0
#addin nuget:?package=Cake.AzureDevOps&version=0.5.0
#addin nuget:?package=Cake.Coverlet&version=2.5.1
#addin nuget:?package=Cake.Docker&version=0.11.1
#addin nuget:?package=Cake.FileHelpers&version=3.3.0
#addin nuget:?package=Cake.Incubator&version=5.1.0
#addin nuget:?package=Cake.Issues&version=0.9.1
#addin nuget:?package=Cake.Issues.AquaScanner&version=0.1.0
#addin nuget:?package=Cake.Issues.PullRequests&version=0.9.0
#addin nuget:?package=Cake.Issues.PullRequests.AzureDevOps&version=0.9.1
#addin nuget:?package=Cake.Json&version=5.2.0
#addin nuget:?package=Cake.Sonar&version=1.1.25
#addin nuget:?package=Newtonsoft.Json&version=12.0.2

// Install tools.
#tool nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.8.0

// Load other scripts.
#load "./build/parameters.cake"

//////////////////////////////////////////////////////////////////////
// Setup/Teardown
//////////////////////////////////////////////////////////////////////

Setup<BuildParameters>(context =>
{
    var parameters = new BuildParameters(context)
                        .ForSolution("./src/Cake.Terraform.sln")
                        .ForSonarQubeProject("Cake.Terraform")
                        .WithNuGetPackageProject("./src/Cake.Terraform/Cake.Terraform.csproj")
                        .WithNuGetPackageProject("./src/Cake.Issues.Terraform/Cake.Issues.Terraform.csproj");

    if(!parameters.IsLocalBuild && !parameters.IsPullRequest && (context.Log.Verbosity != Verbosity.Diagnostic)) {
        Information("Increasing verbosity to diagnostic.");
        context.Log.Verbosity = Verbosity.Diagnostic;
    }

    Information("Building version {0} of Cake.Terraform ({1}, {2}) using version {3} of Cake.",
        parameters.Version.SemVersion,
        parameters.Configuration,
        parameters.Target,
        parameters.Version.CakeVersion);

    Debug("Parameters\n{0}", parameters.Dump());
    Debug("Parameters.Paths.Files\n{0}", parameters.Paths.Files.Dump());
    Debug("Parameters.Paths.Directories\n{0}", parameters.Paths.Directories.Dump());

    return parameters;
});

Teardown<BuildParameters>((context, parameters) =>
{
    Information("Starting Teardown...");

    if (parameters.Issues.Any()) {
        foreach (var issue in parameters.Issues) {
            var messageData = new AzurePipelinesMessageData {
                        LineNumber = issue.Line,
                        ColumnNumber = issue.Column,
                        SourcePath = issue.AffectedFileRelativePath.FullPath
                    };

            if ((IssuePriority)(issue.Priority ?? 0) == IssuePriority.Error) {
                if (parameters.IsLocalBuild) {
                    Error("{0} ({1}: {2} - {3}): {4} {5}",
                        issue.ProviderName, issue.PriorityName,
                        issue.Rule, MakeAbsolute(issue.AffectedFileRelativePath),
                        issue.MessageText,
                        issue.RuleUrl);
                } else {
                    AzurePipelines.Commands.WriteError(issue.MessageText, messageData);
                }
            } else
            {
                if (parameters.IsLocalBuild) {
                    Warning("{0} ({1}: {2} - {3}): {4} {5}",
                        issue.ProviderName, issue.PriorityName,
                        issue.Rule, MakeAbsolute(issue.AffectedFileRelativePath),
                        issue.MessageText,
                        issue.RuleUrl);
                } else {
                    AzurePipelines.Commands.WriteWarning(issue.MessageText, messageData);
                }
            }
        }

        if (BuildSystem.IsPullRequest) {
            ReportIssuesToPullRequest(parameters.Issues, AzureDevOpsPullRequests(), MakeAbsolute(Directory("./")));
        }
    }

    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does<BuildParameters>((context, parameters) =>
{
    CleanDirectories("./src/**/bin/" + parameters.Configuration);
    CleanDirectories("./src/**/obj");
    CleanDirectories("./tests/**/bin/" + parameters.Configuration);
    CleanDirectories("./tests/**/obj");
    CleanDirectories(parameters.Paths.Directories.ToClean);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does<BuildParameters>((context, parameters) =>
{
    DotNetCoreRestore(parameters.Solution.FullPath, new DotNetCoreRestoreSettings {
        MSBuildSettings = parameters.MSBuildSettings
    });
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Sonar-Begin")
    .Does<BuildParameters>((context, parameters) =>
{
    var path = MakeAbsolute(parameters.Solution.GetDirectory());
    DotNetCoreBuild(path.FullPath, new DotNetCoreBuildSettings()
    {
        Configuration = parameters.Configuration,
        MSBuildSettings = parameters.MSBuildSettings,
        NoRestore = true
    });
});

Task("Run-Unit-Tests")
    .Description("Runs unit tests for the builds Commit Phase.")
    .IsDependentOn("Build")
    .Does<BuildParameters>((context, parameters) =>
{
    var projects = GetFiles("./src/**/*.Tests.csproj");

    var unitTestException = false;

    foreach (var project in projects) {
        var testSettings = new DotNetCoreTestSettings {
                Configuration = parameters.Configuration,
                NoBuild = true,
                NoRestore = true,
                ResultsDirectory = parameters.Paths.Directories.TestResults,
                Framework = "netcoreapp3.1",
                Logger = $"trx;LogFileName={project.GetFilenameWithoutExtension()}-UnitTests.trx"
            };

        var coverletSettings = new CoverletSettings {
                CollectCoverage = true,
                CoverletOutputFormat = CoverletOutputFormat.opencover,
                CoverletOutputDirectory = parameters.Paths.Directories.TestResults,
                CoverletOutputName = $"{project.GetFilenameWithoutExtension()}-Coverage.xml"
            };

        try
        {
            DotNetCoreTest(project.FullPath, testSettings, coverletSettings);
        }
        catch (Exception ex)
        {
            Debug("Unit Test Exception: {0}", ex);
            unitTestException = true;
        }
    }

    ReportGenerator(GetFiles(parameters.Paths.Directories.TestResults + "/*-Coverage.xml"),
        parameters.Paths.Directories.TestResults,
        new ReportGeneratorSettings
        {
            ReportTypes = new [] { 
                ReportGeneratorReportType.Cobertura,
            },
            ToolPath = context.Tools.Resolve(context.IsRunningOnWindows() ? "dotnet.exe" : "dotnet"),
            ArgumentCustomization = args => args.Prepend("tool run reportgenerator --")
        });

    var coberturaFile = parameters.Paths.Directories.TestResults.CombineWithFilePath("Cobertura.xml");
    var adoReportDirectory = parameters.Paths.Directories.TestResults.Combine("ado");

    ReportGenerator(coberturaFile,
        adoReportDirectory,
        new ReportGeneratorSettings
        {
            ReportTypes = new [] { 
                ReportGeneratorReportType.HtmlInline_AzurePipelines
            },
            ToolPath = context.Tools.Resolve(context.IsRunningOnWindows() ? "dotnet.exe" : "dotnet"),
            ArgumentCustomization = args => args.Prepend("tool run reportgenerator --")
        });

    if (!parameters.IsLocalBuild) {
        AzurePipelines.Commands.PublishTestResults(new AzurePipelinesPublishTestResultsData {
            Configuration = parameters.Configuration,
            TestRunTitle = "UnitTests",
            TestRunner = AzurePipelinesTestRunnerType.VSTest,
            PublishRunAttachments = true,
            TestResultsFiles = GetFiles(parameters.Paths.Directories.TestResults + "/*-UnitTests.trx").ToList()
        });

        AzurePipelines.Commands.PublishCodeCoverage(coberturaFile, new AzurePipelinesPublishCodeCoverageData {
            CodeCoverageTool = AzurePipelinesCodeCoverageToolType.Cobertura,
            ReportDirectory = adoReportDirectory
        });
    }
});

Task("Sonar-Begin")
    .WithCriteria<BuildParameters>((context, parameters) => parameters.SonarQube.Enabled, "SonarQube isn't enabled")
    .WithCriteria<BuildParameters>((context, parameters) => !String.IsNullOrEmpty(parameters.SonarQube.Url), "SonarQube url isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !string.IsNullOrEmpty(parameters.SonarQube.Token), "SonarQube token isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !string.IsNullOrEmpty(parameters.SonarQubeProject), "SonarQube project isn't set")
    .Does<BuildParameters>((context, parameters) =>
{
    var settings = new SonarBeginSettings {
        Name = parameters.SonarQubeProject,
        Key = parameters.SonarQubeProject,
        Version = parameters.Version.SemVersion,
        Url = parameters.SonarQube.Url,
        Login = parameters.SonarQube.Token,
        OpenCoverReportsPath = MakeAbsolute(parameters.Paths.Directories.TestResults) + "/*-Coverage.xml",
        VsTestReportsPath = MakeAbsolute(parameters.Paths.Directories.TestResults) + "/*-UnitTests.trx",
        DuplicationExclusions = "**/*.Generated.cs/**, **/*.Service/Startup.cs, **/*.Service/Program.cs"
    };

    // if (parameters.IsLocalBuild) {
    //     settings.Branch = parameters.Version.GitVersion.BranchName;
    // } else if (parameters.IsPullRequest) {
    //     settings.PullRequestProvider = "vsts";
    //     settings.PullRequestVstsEndpoint = AzurePipelines.Environment.TeamProject.CollectionUri.ToString();
    //     settings.PullRequestVstsProject = AzurePipelines.Environment.TeamProject.Name;
    //     settings.PullRequestVstsRepository = AzurePipelines.Environment.Repository.RepoName;
    //     settings.PullRequestKey = AzurePipelines.Environment.PullRequest.Id;
    //     settings.PullRequestBranch = AzurePipelines.Environment.PullRequest.SourceBranch.Replace("refs/heads/", string.Empty);
    //     settings.PullRequestBase = AzurePipelines.Environment.PullRequest.TargetBranch.Replace("refs/heads/", string.Empty);
    // } else {
    //     settings.Branch = AzurePipelines.Environment.Repository.SourceBranchName;
    // }

    // if (parameters.IsRunningOnWindows)
    // {
    //     settings.OpenCoverReportsPath = settings.OpenCoverReportsPath.Replace("/", "\\");
    //     settings.VsTestReportsPath = settings.VsTestReportsPath.Replace("/", "\\");
    // }

    SonarBegin(settings);
}).OnError<BuildParameters>((exception, parameters) =>
{
    Warning("Sonar-Begin Task failed, but continuing with next Task...");
    parameters.SonarQubeError = true;
});

Task("Sonar-End")
    .IsDependentOn("Run-Unit-Tests")
    .WithCriteria<BuildParameters>((context, parameters) => parameters.SonarQube.Enabled, "SonarQube isn't enabled")
    .WithCriteria<BuildParameters>((context, parameters) => !String.IsNullOrEmpty(parameters.SonarQube.Url), "SonarQube url isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !string.IsNullOrEmpty(parameters.SonarQube.Token), "SonarQube token isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !string.IsNullOrEmpty(parameters.SonarQubeProject), "SonarQube project isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !parameters.SonarQubeError, "SonarQube-Begin encountered an error")
    .Does<BuildParameters>((context, parameters) =>
{
    SonarEnd(new SonarEndSettings {
        Login = parameters.SonarQube.Token
    });

    AzurePipelines.Commands.AddBuildTag("SonarQube");
}).OnError<BuildParameters>((exception, parameters) =>
{
    Warning("Sonar-End Task failed, but continuing with next Task...");
    parameters.SonarQubeError = true;
});

Task("Copy-Files")
    .IsDependentOn("Sonar-End")
    .Does<BuildParameters>((context, parameters) =>
{
    Information("Creating version.json");
    GitVersion assertedVersions = GitVersion(new GitVersionSettings { 
        OutputType = GitVersionOutput.Json,
        ToolPath = context.Tools.Resolve(context.IsRunningOnWindows() ? "dotnet.exe" : "dotnet"),
        ArgumentCustomization = args => args.Prepend("tool run dotnet-gitversion --")
    });
    var json = SerializeJsonPretty(assertedVersions);
    var versionFile = parameters.Paths.Directories.CakeScripts.CombineWithFilePath("version.json");
    FileWriteText(versionFile, json);

    Information("Copying cake files and configuration");
    var cakeDir = parameters.Paths.Directories.CakeScripts;
    var cakeToolsDir = cakeDir + Directory("/tools");
    EnsureDirectoryExists(cakeToolsDir);
    CopyFileToDirectory("./build.cake", cakeDir);
    CopyDirectory("./build", cakeDir + "/build");
    CopyDirectory("./.config", cakeDir + "/.config");
    CopyFiles("./artillery.io/*", parameters.Paths.Directories.Artillery);
}).DoesForEach<BuildParameters, FilePath>(
        (parameters, context) => parameters.DockerImages.Select(x => x.Project),
        (parameters, project, context) =>
{
    Information("Publishing {0}", project);

    var outputDirectory = parameters.Paths.Directories.ArtifactsBin.Combine(project.GetFilenameWithoutExtension().FullPath);

    DotNetCorePublish(project.FullPath, new DotNetCorePublishSettings
    {
        Framework = "netcoreapp3.1",
        VersionSuffix = parameters.Version.DotNetAsterix,
        Configuration = parameters.Configuration,
        OutputDirectory = outputDirectory,
        MSBuildSettings = parameters.MSBuildSettings,
        NoRestore = true
    });
});

Task("Zip-Files")
    .IsDependentOn("Copy-Files")
    .Does<BuildParameters>((context, parameters) =>
{
    var zipFile = parameters.Paths.Directories.ZipFiles + $"/Menu.Service.zip";
    Information("Creating {0}", zipFile);
    var files = GetFiles(parameters.Paths.Directories.ArtifactsBin.FullPath + "/**/*");
    Zip(parameters.Paths.Directories.ArtifactsBin, zipFile, files);
});

Task("Package-NuGet")
    .IsDependentOn("Copy-Files")
    .DoesForEach<BuildParameters, FilePath>(
        (parameters, context) => parameters.NuGetPackageProjects,
        (parameters, project, context) =>
{
    DotNetCorePack(project.FullPath, new DotNetCorePackSettings {
            Configuration = parameters.Configuration,
            OutputDirectory = parameters.Paths.Directories.NugetRoot,
            NoBuild = true,
            NoRestore = true,
            MSBuildSettings = parameters.MSBuildSettings
        });
});

Task("Publish-NuGet")
    .IsDependentOn("Package-NuGet")
    .WithCriteria<BuildParameters>((context, parameters) => !parameters.IsLocalBuild, "Build running locally")
    .WithCriteria<BuildParameters>((context, parameters) => !parameters.IsPullRequest, "Is a pull request build")
    .DoesForEach<BuildParameters, FilePath>(
        (parameters, context) => GetFiles(parameters.Paths.Directories.NugetRoot + "/*.nupkg"),
        (parameters, package, context) =>
{
    Information("Pushing NuGet package {0}", package.FullPath);

    var settings = new DotNetCoreNuGetPushSettings {
        Source = parameters.NuGet.Source,
        ApiKey = parameters.NuGet.ApiKey,
        SkipDuplicate = parameters.NuGet.SkipDuplicates
    };

    DotNetCoreNuGetPush(package.FullPath, settings);
});

Task("Package-Docker")
    .IsDependentOn("Copy-Files")
    .DoesForEach<BuildParameters, DockerImageConfiguration>(
        (parameters, context) => parameters.DockerImages,
        (parameters, dockerImage, context) =>
{
    Information("Building docker image {0}", dockerImage.Name);

    var settings = new DockerImageBuildSettings {
                    File = MakeAbsolute(dockerImage.Dockerfile).FullPath,
                    Tag = dockerImage.Tags
    };

    DockerBuild(settings, MakeAbsolute(dockerImage.Dockerfile.GetDirectory()).FullPath);
});

Task("Analyze-Docker")
    .IsDependentOn("Package-Docker")
    .WithCriteria<BuildParameters>((context, parameters) => parameters.Aqua.Enabled, "Aqua isn't enabled")
    .WithCriteria<BuildParameters>((context, parameters) => !String.IsNullOrEmpty(parameters.Aqua.Url), "Aqua url isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !String.IsNullOrEmpty(parameters.Aqua.Username), "Aqua username isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !String.IsNullOrEmpty(parameters.Aqua.Password), "Aqua password isn't set")
    .WithCriteria<BuildParameters>((context, parameters) => !String.IsNullOrEmpty(parameters.Aqua.ScannerImage), "Aqua scanner image isn't set")
    .DoesForEach<BuildParameters, DockerImageConfiguration>(
        (parameters, context) => parameters.DockerImages,
        (parameters, dockerImage, context) =>
{
    var imageToAnalyze = dockerImage.Tags[0];
    Information("Analyzing docker image {0}", imageToAnalyze);

    var resultsFileName = $"aqua_{dockerImage.Name.Replace("/", "_")}";
    var htmlFile = parameters.Paths.Directories.AnalysisResults.CombineWithFilePath($"{resultsFileName}.html");
    var jsonFile = parameters.Paths.Directories.AnalysisResults.CombineWithFilePath($"{resultsFileName}.json");

    var settings = new AquaScannerScanSettings {
        Image = imageToAnalyze,
        Local = true,
        Host = parameters.Aqua.Url,
        User = parameters.Aqua.Username,
        Password = parameters.Aqua.Password,
        AquaImage = parameters.Aqua.ScannerImage,
        HtmlFile = htmlFile,
        JsonFile = jsonFile,
    };

    try
    {
        AquaScannerScan(settings);
        AzurePipelines.Commands.AddBuildTag("Aqua Policy Passed");
    }
    catch(Exception ex)
    {
        Debug("Aqua Exception: {0}", ex);
        AzurePipelines.Commands.AddBuildTag("Aqua Policy Failed");
        parameters.AquaPolicyFailure = true;
    }

    if (FileExists(jsonFile)) {
        parameters.Issues.AddRange(ReadIssues(AquaScannerIssuesFromFilePath(jsonFile, dockerImage.Dockerfile), "./"));
    }

    if (parameters.Aqua.FailOnPolicyViolation && parameters.AquaPolicyFailure) {
        throw new Exception("Failed Aqua Policy");
    }
});

Task("Publish-Docker")
    .IsDependentOn("Analyze-Docker")
    .WithCriteria<BuildParameters>((context, parameters) => !parameters.IsLocalBuild, "Build running locally")
    .WithCriteria<BuildParameters>((context, parameters) => !parameters.IsPullRequest, "Is a pull request build")
    .DoesForEach<BuildParameters, DockerImageConfiguration>(
        (parameters, context) => parameters.DockerImages,
        (parameters, dockerImage, context) =>
{
    Information("Pushing tags for {0}", dockerImage.Name);
    foreach (var tag in dockerImage.Tags) {
        DockerPush(tag);
    }
});

Task("Upload-ADO-Artifacts")
    .IsDependentOn("Copy-Files")
    .IsDependentOn("Package")
    .Does<BuildParameters>((context, parameters) =>
{
    AzurePipelines.Commands.UploadArtifactDirectory(parameters.Paths.Directories.CakeScripts, parameters.Paths.Directories.CakeScripts.GetDirectoryName());
    AzurePipelines.Commands.UploadArtifactDirectory(parameters.Paths.Directories.NugetRoot, parameters.Paths.Directories.NugetRoot.GetDirectoryName());
    AzurePipelines.Commands.UploadArtifactDirectory(parameters.Paths.Directories.Artillery, parameters.Paths.Directories.Artillery.GetDirectoryName());
    AzurePipelines.Commands.UploadArtifactDirectory(parameters.Paths.Directories.AnalysisResults, parameters.Paths.Directories.AnalysisResults.GetDirectoryName());
});

Task("Generate-Coverage-Report")
    .IsDependentOn("Run-Unit-Tests")
    .Does<BuildParameters>((context, parameters) =>
{
    ReportGenerator(parameters.Paths.Directories.TestResults + "/*-Coverage.xml",
                    parameters.Paths.Directories.TestResults,
                    new ReportGeneratorSettings
                    {
                        ReportTypes = new [] { ReportGeneratorReportType.HtmlSummary },
                        ToolPath = context.Tools.Resolve(context.IsRunningOnWindows() ? "dotnet.exe" : "dotnet"),
                        ArgumentCustomization = args => args.Prepend("tool run reportgenerator --")
                    });

    MoveFile(parameters.Paths.Directories.TestResults + File("/summary.htm"),
            parameters.Paths.Directories.TestResults + File("/CoverageReport.htm"));

    if(parameters.IsRunningOnWindows)
    {
        StartProcess("cmd.exe", "/c start " + parameters.Paths.Directories.TestResults + "/CoverageReport.htm");
    }
});

Task("Generate-Coverage-Report-Detailed")
    .IsDependentOn("Run-Unit-Tests")
    .Does<BuildParameters>((context, parameters) =>
{
    var tempDirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
    var tempDirectory = Directory(tempDirectoryPath).Path;

    EnsureDirectoryExists(tempDirectory);
    CleanDirectory(tempDirectory);

    ReportGenerator(parameters.Paths.Directories.TestResults + "/*-Coverage.xml",
                    tempDirectory,
                    new ReportGeneratorSettings
                    {
                        ReportTypes = new [] { ReportGeneratorReportType.Html },
                        ToolPath = context.Tools.Resolve(context.IsRunningOnWindows() ? "dotnet.exe" : "dotnet"),
                        ArgumentCustomization = args => args.Prepend("tool run reportgenerator --")
                    });

    if(parameters.IsRunningOnWindows)
    {
        StartProcess("cmd.exe", "/c start " + tempDirectory+ "/index.htm");
    }

    Zip(tempDirectory, parameters.Paths.Directories.TestResults + File("/DetailedCoverageReport.zip"),
        tempDirectory + "/*");
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Package");

Task("Package")
    .IsDependentOn("Zip-Files")
    .IsDependentOn("Package-NuGet")
    .IsDependentOn("Analyze-Docker");

Task("AzureDevOps")
    .IsDependentOn("Default")
    .IsDependentOn("Publish-Docker")
    .IsDependentOn("Publish-NuGet")
    .IsDependentOn("Upload-ADO-Artifacts");

Task("Sonar")
    .IsDependentOn("Sonar-End");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(Argument("target", "Default"));