#addin nuget:?package=Newtonsoft.Json&version=13.0.2
#addin nuget:?package=Cake.Json&version=7.0.1
#addin nuget:?package=Cake.FileHelpers&version=5.0.0
#tool dotnet:?package=dotnet-t4&version=2.3.1
#tool dotnet:?package=gitreleasemanager.tool&version=0.13.0

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
	public int? Weight { get; set; }
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
var targets = Arguments<string>("target", new[] { "Default" });
var configuration = Argument("configuration", "Release");
var runtime = Argument("runtime", GetRuntime(Context));
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

	if (!buildData.Version.Weight.HasValue)
	{
		buildData.Version.Weight = 0;
	}

	context.Information("Building VideoConverter v{0}", buildData.Version.FullSemVer);

	return buildData.Version;
});


Task("Clean")
	.Does(() =>
{
	var directories = GetDirectories("**/bin") + GetDirectories("**/obj")
		+ GetDirectories(artifactsDir + "/*");

	CleanDirectories(directories);
});

Task("Transform-TextTemplates")
	.Does(() =>
{
	var templates = GetFiles("src/**/*.tt");

	foreach (var template in templates)
	{
		TransformTemplate(template);
	}
});

Task("Build")
	.IsDependentOn("Clean")
	.IsDependentOn("Transform-TextTemplates")
	.DoesForEach(GetFiles("src/**/*Tests.csproj"), (project) =>
{
	DotNetBuild(project.FullPath, new DotNetBuildSettings
	{
		Configuration = configuration,
		Runtime = runtime,
		ArgumentCustomization = (args) => args.Append("--no-self-contained")
	});
})
	.Does<BuildVersion>((version) =>
{
	var plainTextNotes = System.IO.File.ReadAllText(plainTextReleaseNotes.ToString(), System.Text.Encoding.UTF8);
	DotNetBuild(mainProject, new DotNetBuildSettings
	{
		Configuration = configuration,
		Runtime = runtime,
		ArgumentCustomization = (args) => args.Append("--no-self-contained"),
		MSBuildSettings = new DotNetMSBuildSettings()
			.SetVersion(version.FullSemVer)
			.WithProperty("PackageReleaseNotes", plainTextNotes)
	});
});

Task("Test")
	.IsDependentOn("Build")
	.Does(() =>
{
	DotNetTest(solution, new DotNetTestSettings
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

	DotNetPublish(mainProject, new DotNetPublishSettings
	{
		Configuration = configuration,
		Runtime = runtime,
		OutputDirectory = outputDirectory,
		PublishSingleFile = false,
		SelfContained = true,
		PublishTrimmed = true,
		MSBuildSettings = new DotNetMSBuildSettings()
			.SetVersion(version.FullSemVer)
			.WithProperty("PackageReleaseNotes", plainTextNotes)
	});

	outputDirectory = artifactsDir.Combine("executables");

	DotNetPublish(mainProject, new DotNetPublishSettings
	{
		Configuration = configuration,
		Runtime = runtime,
		OutputDirectory = outputDirectory,
		PublishSingleFile = true,
		SelfContained = true,
		EnableCompressionInSingleFile = true,
		PublishTrimmed = true,
		MSBuildSettings = new DotNetMSBuildSettings()
			.SetVersion(version.FullSemVer)
			.WithProperty("PackageReleaseNotes", plainTextNotes)
	});

	if (FileExists(outputDirectory.CombineWithFilePath("VideoConverter.exe")))
	{
		MoveFile(
			outputDirectory.CombineWithFilePath("VideoConverter.exe"),
			outputDirectory.CombineWithFilePath($"videoconverter-{runtime}.exe")
		);
	}
	else if (FileExists(outputDirectory.CombineWithFilePath("VideoConverter")))
	{
		MoveFile(
			outputDirectory.CombineWithFilePath("VideoConverter"),
			outputDirectory.CombineWithFilePath($"videoconverter-{runtime}")
		);
	}
});

Task("Create-Installer")
	.IsDependentOn("Publish-Binaries")
	.WithCriteria(IsRunningOnWindows)
	.Does<BuildVersion>(version =>
{
	var script = File("./installer/install.iss");
	var outputDirectory = artifactsDir.Combine("executables");;

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

	var installerName = "VideoConverter-" + version.SemVer + "-install.exe";

	ReplaceTextInFiles("./.artifacts/build/packages/choco/**/*.ps1", "{{FILE_NAME}}", installerName);
	var license = MakeAbsolute(File("./LICENSE.txt"));
	var installer = MakeAbsolute(artifactsDir.CombineWithFilePath("executables/" + installerName));
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

Task("Upload-Dev-Packages")
	.IsDependentOn("Pack-Choco")
	.WithCriteria(IsRunningOnWindows())
	.WithCriteria(() => HasEnvironmentVariable("DEVELOPMENT_CHOCO_ARTIFACTS_URL")
		&& HasEnvironmentVariable("DEVELOPMENT_CHOCO_ARTIFACTS_API_KEY"))
	.ContinueOnError()
	.Does(() =>
{
	var packages = GetFiles("./.artifacts/packages/choco/*.nupkg");
	var source = EnvironmentVariable("DEVELOPMENT_CHOCO_ARTIFACTS_URL");

	Information("Pushing " + packages.Count + " package(s) to " + source);

	ChocolateyPush(packages, new ChocolateyPushSettings
	{
		Source = source,
		ApiKey = EnvironmentVariable("DEVELOPMENT_CHOCO_ARTIFACTS_API_KEY")
	});
});

Task("Publish")
	.IsDependentOn("Publish-Binaries")
	.IsDependentOn("Upload-Dev-Packages");

Task("Create-Tag")
	.Does<BuildVersion>((version) =>
{
	var plainTextNotes = System.IO.File.ReadAllLines(plainTextReleaseNotes.ToString(), System.Text.Encoding.UTF8).Skip(2);

	StartProcess("git", new ProcessSettings().WithArguments(args =>
		args.Append("tag")
			.Append(version.MajorMinorPatch)
			.Append("-a")
			.AppendSwitchQuoted("-m", string.Join("\n", plainTextNotes).Replace("\"", string.Empty).Trim())
	));
});

Task("Draft-ReleaseNotes")
	.WithCriteria(() => HasEnvironmentVariable("RELEASE_TOKEN"))
	.WithCriteria(() => !string.IsNullOrEmpty(EnvironmentVariable("RELEASE_TOKEN")))
	.Does<BuildVersion>((version) =>
{
	var token = EnvironmentVariable("RELEASE_TOKEN");

	GitReleaseManagerCreate(token, "AdmiringWorm", "VideoConverter", new GitReleaseManagerCreateSettings
	{
		Name = version.SemVer,
		InputFilePath = markdownReleaseNotes,
		TargetCommitish = "master",
		Prerelease = false
	});
});

Task("Upload-GitHubArtifacts")
	.IsDependentOn("Publish-Binaries")
	.IsDependentOn("Create-Installer")
	.WithCriteria(() => HasEnvironmentVariable("RELEASE_TOKEN"))
	.WithCriteria(() => !string.IsNullOrEmpty(EnvironmentVariable("RELEASE_TOKEN")))
	.Does<BuildVersion>((version) =>
{
	var token = EnvironmentVariable("RELEASE_TOKEN");

	var files = GetFiles(artifactsDir + "/executables/*").Select(f => f.FullPath);
	var joinedFiles = string.Join(',', files);

	GitReleaseManagerAddAssets(token, "AdmiringWorm", "VideoConverter", version.SemVer, joinedFiles);
});

Task("Publish-GitHubRelease")
	.IsDependentOn("Upload-GitHubArtifacts")
	.WithCriteria(() => HasEnvironmentVariable("RELEASE_TOKEN"))
	.WithCriteria(() => !string.IsNullOrEmpty(EnvironmentVariable("RELEASE_TOKEN")))
	.WithCriteria(() => IsRunningOnWindows())
	.Does<BuildVersion>((version) =>
{
	var token = EnvironmentVariable("RELEASE_TOKEN");

	if (version.MajorMinorPatch == version.SemVer)
	{
		GitReleaseManagerClose(token, "AdmiringWorm", "VideoConverter", version.MajorMinorPatch);
	}

	GitReleaseManagerPublish(token, "AdmiringWorm", "VideoConverter", version.SemVer);
});

Task("Default")
	.IsDependentOn("Coverage")
	.IsDependentOn("Publish");

Task("Release")
	.IsDependentOn("Default")
	.IsDependentOn("Publish-GitHubRelease");

RunTargets(targets);
