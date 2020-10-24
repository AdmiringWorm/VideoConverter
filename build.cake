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

Task("Clean")
	.WithCriteria(HasArgument("clean") || string.Compare(target, "clean", StringComparison.OrdinalIgnoreCase) == 0)
	.Does(() =>
{
	var directories = GetDirectories("**/bin") + GetDirectories("**/obj");

	if (DirectoryExists("./.artifacts"))
		directories += "./.artifacts";

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
	.Does(() =>
{
	DotNetCoreBuild(mainProject, new DotNetCoreBuildSettings
	{
		Configuration = configuration,
		Runtime = runtime
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
	});
});

Task("Publish")
	.IsDependentOn("Test")
	.Does(() =>
{
	var outputDirectory = "./.artifacts/output";
	if (DirectoryExists(outputDirectory))
		CleanDirectory(outputDirectory);

	DotNetCorePublish(mainProject, new DotNetCorePublishSettings
	{
		Configuration = configuration,
		Runtime = runtime,
		OutputDirectory = outputDirectory,
		PublishSingleFile = singleFile,
	});
});

Task("Default")
	.IsDependentOn("Publish");

RunTarget(target);
