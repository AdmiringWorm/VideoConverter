#addin nuget:?package=Newtonsoft.Json&version=12.0.3
#addin nuget:?package=Cake.Json&version=6.0.0

public class BuildData
{
	public BuildVersion Version { get; set; }
}

public class BuildVersion
{
	public string MajorMinorPatch { get; set; }
	public string SemVer { get; set; }
	public string FullSemVer { get; set; }
	public string PreReleaseTag { get; set; }
	public string Metadata { get; set; }
}


public static string GetRuntime(ICakeContext context)
{
	if (context.IsRunningOnWindows())
		return "win-x64";
	else if (context.Environment.Platform.Family == PlatformFamily.OSX)
		return "osx-x64";
	else
		return "linux-x64";
}

var solution = "./VideoConverter.sln";
var mainProject = "./src/VideoConverter/VideoConverter.csproj";
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var runtime = Argument("runtime", GetRuntime(Context));
var singleFile = HasArgument("single-file");
var artifactsDir = Argument<DirectoryPath>("artifacts", "./.artifacts");
var dotnetExec = Context.Tools.Resolve("dotnet") ?? Context.Tools.Resolve("dotnet.exe");
var plainTextReleaseNotes = artifactsDir.CombineWithFilePath("release-notes.txt");


Setup((context) =>
{
	var outputPath = artifactsDir.CombineWithFilePath("data.json");
	EnsureDirectoryExists(artifactsDir);

	var exitCode = StartProcess(dotnetExec, new ProcessSettings
	{
		Arguments = new ProcessArgumentBuilder()
			.Append("ccvarn")
			.Append("parse")
			.AppendQuoted(outputPath.ToString())
			.AppendSwitchQuoted("--output", " ", plainTextReleaseNotes.ToString()),
	});

	var buildData = DeserializeJsonFromFile<BuildData>(outputPath);

	context.Information("Building VideoConverter v{0}", buildData.Version.FullSemVer);

	return buildData.Version;
});


Task("Clean")
	.WithCriteria(HasArgument("clean") || string.Compare(target, "clean", StringComparison.OrdinalIgnoreCase) == 0)
	.Does(() =>
{
	var directories = GetDirectories("**/bin") + GetDirectories("**/obj")
		+ GetDirectories(artifactsDir + "/*");

	CleanDirectories(directories);
});

Task("Build")
	.IsDependentOn("Clean")
	.DoesForEach(GetFiles("tests/**/*.csproj"), (project) =>
{
	DotNetCoreBuild(project.FullPath, new DotNetCoreBuildSettings
	{
		Configuration = configuration,
		Runtime = runtime
	});
})
	.Does<BuildVersion>((version) =>
{
	var plainTextNotes = System.IO.File.ReadAllText(plainTextReleaseNotes.ToString(), System.Text.Encoding.UTF8);
	DotNetCoreBuild(mainProject, new DotNetCoreBuildSettings
	{
		Configuration = configuration,
		Runtime = runtime,
		MSBuildSettings = new DotNetCoreMSBuildSettings()
			.SetVersion(version.FullSemVer)
			.WithProperty("PackageReleaseNotes", plainTextNotes)
	});
});

Task("Test")
	.IsDependentOn("Build")
	.Does(() =>
{
	DotNetCoreTest(solution, new DotNetCoreTestSettings
	{
		Configuration = configuration,
		NoBuild = true,
		Runtime = runtime,
		ArgumentCustomization = (args) =>
			args.AppendSwitchQuoted("--collect", ":", "XPlat Code Coverage")
			.Append("--")
			.AppendSwitchQuoted(
				"DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format",
				"=",
				"opencover,cobertura")
			.AppendSwitchQuoted(
				"DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.SkipAutoProps",
				"=",
				"true")
			.AppendSwitchQuoted(
				"DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.UseSourceLink",
				"=",
				"true")
			.AppendSwitchQuoted(
				"DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude",
				"=",
				"[*]DryIoc.*,[*]FastExpressionCompiler.*,[*]ImTools.*"),
		ResultsDirectory = artifactsDir.Combine("tests")
	});
});

Task("Coverage")
	.IsDependentOn("Test")
	.Does(() =>
{
	var files = GetFiles("./.artifacts/tests/**/*");
	var outDir = artifactsDir.Combine("coverage");
	ReportGenerator(files, outDir, new ReportGeneratorSettings
	{
		ArgumentCustomization = args => args.Prepend("reportgenerator"),
		ToolPath = dotnetExec
	});
});

Task("Publish")
	.IsDependentOn("Test")
	.WithCriteria(IsRunningOnLinux)
	.Does<BuildVersion>((version) =>
{
	var outputDirectory = artifactsDir.Combine("output");
	if (DirectoryExists(outputDirectory))
		CleanDirectory(outputDirectory);

	var plainTextNotes = System.IO.File.ReadAllText(plainTextReleaseNotes.ToString(), System.Text.Encoding.UTF8);

	DotNetCorePublish(mainProject, new DotNetCorePublishSettings
	{
		Configuration = configuration,
		Runtime = runtime,
		OutputDirectory = outputDirectory,
		PublishSingleFile = singleFile,
		MSBuildSettings = new DotNetCoreMSBuildSettings()
			.SetVersion(version.FullSemVer)
			.WithProperty("PackageReleaseNotes", plainTextNotes)
	});
});

Task("Default")
	.IsDependentOn("Coverage")
	.IsDependentOn("Publish");

RunTarget(target);
