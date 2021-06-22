// Contains .NET 6-related Cake targets

var ext = IsRunningOnWindows() ? ".exe" : "";
var dotnetPath = $"./bin/dotnet/dotnet{ext}";

// Tasks for CI

Task("dotnet")
    .Description("Provisions .NET 6 into bin/dotnet based on eng/Versions.props")
    .Does(() =>
    {
        DotNetCoreBuild("./src/DotNet/DotNet.csproj", new DotNetCoreBuildSettings
        {
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .EnableBinaryLogger($"{logDirectory}/dotnet-{configuration}.binlog")
                .SetConfiguration(configuration),
        });
    });

Task("dotnet-local-workloads")
    .Does(() =>
    {
        DotNetCoreBuild("./src/DotNet/DotNet.csproj", new DotNetCoreBuildSettings
        {
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .EnableBinaryLogger($"{logDirectory}/dotnet-{configuration}.binlog")
                .SetConfiguration(configuration)
                .WithProperty("InstallWorkloadPacks", "false"),
        });
        DotNetCoreBuild("./src/DotNet/DotNet.csproj", new DotNetCoreBuildSettings
        {
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .EnableBinaryLogger($"{logDirectory}/dotnet-install-{configuration}.binlog")
                .SetConfiguration(configuration)
                .WithTarget("Install"),
        });
    });

Task("dotnet-buildtasks")
    .IsDependentOn("dotnet")
    .Does(() =>
    {
        RunMSBuildWithLocalDotNet("./Microsoft.Maui.BuildTasks-net6.slnf");
    });

Task("dotnet-build")
    .IsDependentOn("dotnet")
    .Description("Build the solutions")
    .Does(() =>
    {
        if (IsRunningOnWindows())
        {
            RunMSBuildWithLocalDotNet("./Microsoft.Maui.BuildTasks-net6.slnf", settings => ((MSBuildSettings)settings)
                .WithProperty("BuildForWinUI", "true"));
            RunMSBuildWithLocalDotNet("./Microsoft.Maui-winui.sln");
        }
        else
        {
            RunMSBuildWithLocalDotNet("./Microsoft.Maui.BuildTasks-net6.slnf");
            RunMSBuildWithLocalDotNet("./Microsoft.Maui-net6.sln");
        }
    });

Task("dotnet-samples")
    .Does(() =>
    {
        RunMSBuildWithLocalDotNet("./Microsoft.Maui.Samples-net6.slnf", settings => ((MSBuildSettings)settings)
            .WithProperty("UseWorkload", "true"));
    });

Task("dotnet-test")
    .IsDependentOn("dotnet")
    .Description("Build the solutions")
    .Does(() =>
    {
        var tests = new []
        {
            "**/Controls.Core.UnitTests.csproj",
            "**/Controls.Xaml.UnitTests.csproj",
            "**/Core.UnitTests.csproj",
            "**/Essentials.UnitTests.csproj",
            "**/Resizetizer.UnitTests.csproj",
        };

        var success = true;

        foreach (var test in tests)
        {
            foreach (var project in GetFiles(test))
            {
                try
                {
                    RunTestWithLocalDotNet(project.FullPath);
                }
                catch
                {
                    success = false;
                }
            }
        }

        if (!success)
            throw new Exception("Some tests failed. Check the logs or test results.");
    });

Task("dotnet-pack")
    .Description("Build and create .NET 6 NuGet packages")
    .Does(() =>
    {
        DotNetCoreTool("pwsh", new DotNetCoreToolSettings
        {
            DiagnosticOutput = true,
            ArgumentCustomization = args => args.Append($"./eng/package.ps1 -configuration \"{configuration}\"")
        });
    });

Task("dotnet-build-test")
    .IsDependentOn("dotnet")
    .IsDependentOn("dotnet-buildtasks")
    .IsDependentOn("dotnet-build")
    .IsDependentOn("dotnet-test");

// Tasks for Local Development

Task("VS-DOGFOOD")
    .Description("Provisions .NET 6 and launches an instance of Visual Studio using it.")
    .IsDependentOn("dotnet")
    .Does(() =>
    {
        StartVisualStudioForDotNet6(null);
    });

Task("VS-NET6")
    .Description("Provisions .NET 6 and launches an instance of Visual Studio using it.")
    .IsDependentOn("Clean")
    .IsDependentOn("dotnet")
    .IsDependentOn("dotnet-buildtasks")
    .Does(() =>
    {
        // VS has trouble building all the references correctly so this makes sure everything is built
        // and we're ready to go right when VS launches
        RunMSBuildWithLocalDotNet("./src/Controls/samples/Controls.Sample/Maui.Controls.Sample-net6.csproj");
        StartVisualStudioForDotNet6();
    });

Task("VS-WINUI")
    .Description("Provisions .NET 6 and launches an instance of Visual Studio with WinUI projects.")
        .IsDependentOn("Clean")
    //  .IsDependentOn("dotnet") WINUI currently can't launch application with local dotnet
    //  .IsDependentOn("dotnet-buildtasks")
    .Does(() =>
    {
        string sln = "./Microsoft.Maui-winui.sln";
        var msbuildSettings = new MSBuildSettings
        {
            Configuration = configuration,
            ToolPath = FindMSBuild(),
            BinaryLogger = new MSBuildBinaryLogSettings
            {
                Enabled  = true,
                FileName = $"{logDirectory}/winui-buildtasks.binlog",
            }
        }.WithRestore().WithProperty("BuildForWinUI", "true");

        MSBuild("./Microsoft.Maui.BuildTasks-net6.sln", msbuildSettings);

        msbuildSettings = new MSBuildSettings
        {
            Configuration = configuration,
            ToolPath = FindMSBuild(),
            BinaryLogger = new MSBuildBinaryLogSettings
            {
                Enabled  = true,
                FileName = $"{logDirectory}/winui.binlog",
            }
        }.WithRestore();

        MSBuild(sln, msbuildSettings);

        var vsLatest = VSWhereLatest(new VSWhereLatestSettings { IncludePrerelease = true, Version = "[\"16.0\",\"17.0\"]"});

        if (vsLatest == null)
            throw new Exception("Unable to find Visual Studio!");

        StartProcess(vsLatest.CombineWithFilePath("./Common7/IDE/devenv.exe"), sln);
    });

Task("VS-ANDROID")
    .Description("Provisions .NET 6 and launches an instance of Visual Studio with Android projects.")
    .IsDependentOn("Clean")
    .IsDependentOn("dotnet")
    .IsDependentOn("dotnet-buildtasks")
    .Does(() =>
    {
        DotNetCoreRestore("./Microsoft.Maui-net6.sln", new DotNetCoreRestoreSettings
        {
            ToolPath = dotnetPath
        });

        // VS has trouble building all the references correctly so this makes sure everything is built
        // and we're ready to go right when VS launches
        RunMSBuildWithLocalDotNet("./src/Controls/samples/Controls.Sample/Maui.Controls.Sample-net6.csproj");
        StartVisualStudioForDotNet6("./Microsoft.Maui.Droid.sln");
    });

Task("SAMPLE-ANDROID")
    .Description("Provisions .NET 6 and launches Android Sample.")
    .IsDependentOn("dotnet")
    .IsDependentOn("dotnet-buildtasks")
    .Does(() =>
    {
        RunMSBuildWithLocalDotNet("./src/Controls/samples/Controls.Sample.Droid/Maui.Controls.Sample.Droid-net6.csproj", deployAndRun:true);
    });

Task("SAMPLE-IOS")
    .Description("Provisions .NET 6 and launches launches iOS Sample.")
    .IsDependentOn("dotnet")
    .IsDependentOn("dotnet-buildtasks")
    .Does(() =>
    {
        RunMSBuildWithLocalDotNet("./src/Controls/samples/Controls.Sample.iOS/Maui.Controls.Sample.iOS-net6.csproj", deployAndRun:true);
    });

Task("SAMPLE-MAC")
    .Description("Provisions .NET 6 and launches Mac Catalyst Sample.")
    .IsDependentOn("dotnet")
    .IsDependentOn("dotnet-buildtasks")
    .Does(() =>
    {
        RunMSBuildWithLocalDotNet("./src/Controls/samples/Controls.Sample.MacCatalyst/Maui.Controls.Sample.MacCatalyst-net6.csproj", deployAndRun:true);
    });


string FindMSBuild()
{
    if (IsRunningOnWindows())
    {
        var vsInstallation = VSWhereLatest(new VSWhereLatestSettings { Requires = "Microsoft.Component.MSBuild", IncludePrerelease = true });
        if (vsInstallation != null)
        {
            var path = vsInstallation.CombineWithFilePath(@"MSBuild\Current\Bin\MSBuild.exe");
            if (FileExists(path))
                return path.FullPath;

            path = vsInstallation.CombineWithFilePath(@"MSBuild\15.0\Bin\MSBuild.exe");
            if (FileExists(path))
                return path.FullPath;
        }
    }
    return "msbuild";
}

void SetDotNetEnvironmentVariables()
{
    var dotnet = MakeAbsolute(Directory("./bin/dotnet/")).ToString();
    var target = EnvironmentVariableTarget.Process;
    Environment.SetEnvironmentVariable("DOTNET_INSTALL_DIR", dotnet, target);
    Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnet, target);
    Environment.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", dotnet, target);
    Environment.SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0", target);
    Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", "true", target);
    Environment.SetEnvironmentVariable("PATH", dotnet + System.IO.Path.PathSeparator + EnvironmentVariable("PATH"), target);
}

void StartVisualStudioForDotNet6(string sln = "./Microsoft.Maui-net6.sln")
{
    if (isCIBuild)
    {
        Information("This target should not run on CI.");
        return;
    }
    if (!IsRunningOnWindows())
    {
        Information("This target is only supported on Windows.");
        return;
    }

    var vsLatest = VSWhereLatest(new VSWhereLatestSettings { IncludePrerelease = true, });
    if (vsLatest == null)
        throw new Exception("Unable to find Visual Studio!");
    SetDotNetEnvironmentVariables();
    Environment.SetEnvironmentVariable("_ExcludeMauiProjectCapability", "true", EnvironmentVariableTarget.Process);
    StartProcess(vsLatest.CombineWithFilePath("./Common7/IDE/devenv.exe"), sln);
}

// NOTE: These methods work as long as the "dotnet" target has already run

void RunMSBuildWithLocalDotNet(string sln, Action<object> settings = null, bool deployAndRun = false)
{
    var name = System.IO.Path.GetFileNameWithoutExtension(sln);
    var binlog = $"{logDirectory}/{name}-{configuration}.binlog";

    SetDotNetEnvironmentVariables();

    // If we're not on Windows, use ./bin/dotnet/dotnet
    if (!IsRunningOnWindows() || deployAndRun)
    {
        var dotnetBuildSettings = new DotNetCoreMSBuildSettings
        {
            BinaryLogger = new MSBuildBinaryLoggerSettings
            {
                Enabled = true,
                FileName = binlog,
            },
        };

        settings?.Invoke(dotnetBuildSettings);

        DotNetCoreBuild(sln,
            new DotNetCoreBuildSettings
            {
                Configuration = configuration,
                ToolPath = dotnetPath,
                MSBuildSettings = dotnetBuildSettings,
                ArgumentCustomization = args=> { if(deployAndRun) { args.Append("-t:Run"); } return args; }
            });
        return;
    }

    // Otherwise we need to run MSBuild for WinUI support
    var msbuildSettings = new MSBuildSettings
        {
            Configuration = configuration,
            BinaryLogger = new MSBuildBinaryLogSettings
            {
                Enabled  = true,
                FileName = binlog,
            },
            ToolPath = FindMSBuild(),
        };

    settings?.Invoke(msbuildSettings);

    MSBuild(sln,
       msbuildSettings
       .WithRestore());
}

void RunTestWithLocalDotNet(string csproj)
{
    var name = System.IO.Path.GetFileNameWithoutExtension(csproj);
    var binlog = $"{logDirectory}/{name}-{configuration}.binlog";
    var results = $"{name}-{configuration}.trx";

    SetDotNetEnvironmentVariables();

    DotNetCoreTest(csproj,
        new DotNetCoreTestSettings
        {
            Configuration = configuration,
            ToolPath = dotnetPath,
            NoBuild = true,
            Logger = $"trx;LogFileName={results}",
            ResultsDirectory = testResultsDirectory,
            ArgumentCustomization = args => args.Append($"-bl:{binlog}")
        });
}
