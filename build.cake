#addin nuget:?package=Newtonsoft.Json&version=13.0.1
#addin nuget:?package=Cake.Json&version=7.0.1
#addin nuget:?package=Cake.FileHelpers&version=5.0.0

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
	public string PreReleaseLabel { get; set; }
	public int Commits { get; set; }
	public int Weight { get; set; }
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
var markdownReleaseNotes = artifactsDir.CombineWithFilePath("release-notes.md");

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
			.AppendSwitchQuoted("--output", " ", plainTextReleaseNotes.ToString())
			.AppendSwitchQuoted("--output", " ", markdownReleaseNotes.ToString()),
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
	.DoesForEach(GetFiles("src/**/*Tests.csproj"), (project) =>
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

Task("Publish-Binaries")
	.IsDependentOn("Test")
	//.WithCriteria(IsRunningOnLinux)
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

Task("Create-Installer")
	.IsDependentOn("Publish-Binaries")
	.WithCriteria(IsRunningOnWindows)
	.Does<BuildVersion>(version =>
{
	var script = File("./installer/install.iss");
	var outputDirectory = artifactsDir.Combine("installers");;
	if (DirectoryExists(outputDirectory))
		CleanDirectory(outputDirectory);

	var versionString = version.MajorMinorPatch;
	if (!string.IsNullOrEmpty(version.PreReleaseTag)) {
		versionString += "-" + version.PreReleaseTag;
	}

	InnoSetup(script, new InnoSetupSettings {
		OutputDirectory = outputDirectory,
		Defines = new Dictionary<string,string> {
			{ "MyAppVersion", versionString }
		}
	});
});

Task("Pack-Choco")
	.IsDependentOn("Create-Installer")
	.WithCriteria(IsRunningOnWindows)
	.Does<BuildVersion>(version =>
{
	var outputDirectory = artifactsDir.Combine("packages/choco");;
	var buildDirectory = artifactsDir.Combine("build/packages/choco");
	var nuspec = buildDirectory.CombineWithFilePath("video-converter.nuspec");

	CleanDirectory(outputDirectory);
	if (DirectoryExists(buildDirectory)) {
		CleanDirectory(buildDirectory);
	}

	CopyDirectory("./packages/choco/", buildDirectory);

	var versionString = version.MajorMinorPatch;
	if (!string.IsNullOrEmpty(version.PreReleaseLabel)) {
		versionString += string.Format(
			"-{0}{1:00}{2:0000}",
			version.PreReleaseLabel,
			version.Weight,
			version.Commits
		);
	}

	var installerName = "VideoConverter-" + version.SemVer + ".exe";

	ReplaceTextInFiles("./.artifacts/build/packages/choco/**/*.ps1", "{{FILE_NAME}}", installerName);
	var license = MakeAbsolute(File("./LICENSE.txt"));
	var installer = MakeAbsolute(artifactsDir.CombineWithFilePath("installers/" + installerName));
	var markdownNotes = System.IO.File.ReadAllText(markdownReleaseNotes.ToString(), System.Text.Encoding.UTF8)
		.Split('\n');

	ChocolateyPack(nuspec, new ChocolateyPackSettings {
		Version = versionString,
		OutputDirectory = outputDirectory,
		ReleaseNotes = markdownNotes,
		Files = new[]{
			new ChocolateyNuSpecContent { Source = "tools/**", Target = "tools" },
			new ChocolateyNuSpecContent { Source = "legal/**", Target = "legal" },
			new ChocolateyNuSpecContent { Source = license.ToString(), Target = "legal" },
			new ChocolateyNuSpecContent { Source = installer.ToString(), Target = "tools" }
		}
	});
});

Task("Publish")
	.IsDependentOn("Publish-Binaries")
	.IsDependentOn("Create-Installer")
	.IsDependentOn("Pack-Choco");

Task("Create-Tag")
	.Does<BuildVersion>((version) =>
{
	var plainTextNotes = System.IO.File.ReadAllLines(plainTextReleaseNotes.ToString(), System.Text.Encoding.UTF8).Skip(2);

	StartProcess("git", new ProcessSettings().WithArguments(args =>
		args.Append("tag")
			.Append(version.MajorMinorPatch)
			.Append("-a")
			.AppendSwitchQuoted("-m", string.Join("\n", plainTextNotes).Trim())
	));
});

Task("Default")
	.IsDependentOn("Coverage")
	.IsDependentOn("Publish");

RunTarget(target);
