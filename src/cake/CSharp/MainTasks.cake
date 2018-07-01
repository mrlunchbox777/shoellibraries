#addin nuget:?package=Polly // For timeout / retry
#addin "nuget:?package=NuGet.Core"
//#addin "Cake.Powershell"
#addin "Cake.IIS"
#addin "nuget:?package=System.ServiceProcess.ServiceController"

//#tool "nuget:?package=Microsoft.TypeScript.Compiler&version=2.7.2"

//////////////////////////////////////////////////////////////
// MSBuild Tasks
//////////////////////////////////////////////////////////////

Task("Restore-CSharp-Nuget-Packages")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Restore Packages.");
    }
    var maxRetryCount = 5;
    var toolTimeout = 1d;
    Policy
        .Handle<Exception>()
        .Retry(maxRetryCount, (exception, retryCount, context) => {
            if (retryCount == maxRetryCount)
            {
                throw exception;
            }
            else
            {
                Verbose("{0}", exception);
                toolTimeout+=0.5;
            }})
        .Execute(()=> {
            NuGetRestore(cakeConfig.ProjectInfo.ProjectSolution, new NuGetRestoreSettings {
                // we don't want to define a source atm
                // Source = new List<string> {
                //     cakeConfig.Nuget.nugetServerURL
                // },
                ToolTimeout = TimeSpan.FromMinutes(toolTimeout),
                PackagesDirectory = cakeConfig.Nuget.packagesDirectory
            });
        });
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Check that packages are available",
            "Check that the nuget server is available",
            "Try local compilation after deleting the packages directory",
            "Ensure the .NET version and packages can be compiled with cake"
        },
        true
        );
});

Task("Build-Project")
    .IsDependentOn("Restore-CSharp-NuGet-Packages")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Build Project.");
    }
    if (cakeConfig.MSBuildInfo.shouldFlatten(false))
    {
        MSBuild(cakeConfig.ProjectInfo.ProjectFile, new MSBuildSettings()
            //.WithTarget(cakeConfig.ProjectInfo.ProjectName) //.Replace('.','_')
            .SetConfiguration("Release")
            .WithProperty("Platform", cakeConfig.MSBuildInfo.platform)        
            .WithProperty("VisualStudioVersion", cakeConfig.MSBuildInfo.MSBuildVersion)
            .WithProperty("PipelineDependsOnBuild", "false")
            .WithProperty("OutputPath", cakeConfig.ProjectInfo.FlattenOutputDirectory)
            .WithProperty("ExcludeFilesFromDeployment", "\"**\\*.svn\\**\\*.*;Web.*.config;*.cs;*\\*.cs;*\\*\\*.cs;*\\*\\*\\*.cs;*.csproj\"")
            .UseToolVersion(MSBuildToolVersion.Default)
            .SetVerbosity(Verbosity.Minimal)
            .SetMaxCpuCount(1));
    }
    else
    {
        MSBuild(cakeConfig.ProjectInfo.ProjectFile, new MSBuildSettings()
            //.WithTarget(cakeConfig.ProjectInfo.ProjectName) //.Replace('.','_')
            .SetConfiguration(cakeConfig.MSBuildInfo.msbuildConfig(false))
            .WithProperty("Platform", cakeConfig.MSBuildInfo.platform)        
            .WithProperty("VisualStudioVersion", cakeConfig.MSBuildInfo.MSBuildVersion)
            .UseToolVersion(MSBuildToolVersion.Default)
            .SetVerbosity(Verbosity.Minimal)
            .SetMaxCpuCount(1));
    }
    
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Check for c# syntax/runtime errors",
            "Try local compilation",
            "Ensure the .NET version and packages can be compiled with cake"
        },
        true
        );
});

Task("CopyWebConfig")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Copy Web Config.");
    }
    // get the web.config
    string origWebConfigLocation = cakeConfig.ProjectInfo.ProjectDirectory + "\\Web.config.example";
    string newWebConfigLocation = cakeConfig.ProjectInfo.ProjectDirectory + "\\Web.config";
    Information("--------------------------------------------------------------------------------");
    Information("Copying - " + origWebConfigLocation + " -> " + newWebConfigLocation);
    Information("--------------------------------------------------------------------------------");
    CopyFile(origWebConfigLocation, newWebConfigLocation);
})

    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure that there is a good web.config"
        },
        true
        );
});

Task("CopyWebConfigToOutput")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Copy Web Config To Output.");
    }
    // get the web.config
    string origWebConfigLocation = cakeConfig.ProjectInfo.ProjectDirectory + "\\Web.config";
    string newWebConfigLocation = cakeConfig.ProjectInfo.FlattenOutputDirectory + "\\" + cakeConfig.ConfigurableSettings.specificWebsiteOutputDir + "\\Web.config";
    Information("--------------------------------------------------------------------------------");
    Information("Copying - " + origWebConfigLocation + " -> " + newWebConfigLocation);
    Information("--------------------------------------------------------------------------------");
    CopyFile(origWebConfigLocation, newWebConfigLocation);
})

    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure that there is a good web.config"
        },
        true
        );
});

Task("RemoveWebConfig")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Remove Web Config.");
    }
    // remove the web.config
    string webConfigLocation = cakeConfig.ProjectInfo.FlattenOutputDirectory + "\\" + cakeConfig.ConfigurableSettings.specificWebsiteOutputDir + "\\Web.config";
    Information("--------------------------------------------------------------------------------");
    Information("Deleting - " + webConfigLocation);
    Information("--------------------------------------------------------------------------------");
    DeleteFile(webConfigLocation);
})

    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure that there is a good web.config"
        },
        true
        );
});

Task("SassCompile")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Sass Compile.");
    }
    Information("--------------------------------------------------------------------------------");
    Information("Compiling Sass - ");
    Information("--------------------------------------------------------------------------------");
    try {
        StartProcess("..\\scss_compiler.bat");
    } catch (Exception ex) {
        Information("Got and ignoring error - " + ex.Message + ".\r\nStack trace -\r\n" + ex.StackTrace);
    }
})

    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure that there is a good web.config"
        },
        true
        );
});

Task("TypeScriptCompile")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Type Script Compile.");
    }
    Information("--------------------------------------------------------------------------------");
    Information("Compiling TSC - ");
    Information("--------------------------------------------------------------------------------");
    try {
        //StartPowershellScript("C:\\Windows\\System32\\config\\systemprofile\\AppData\\Roaming\\npm\\tsc.cmd");
        StartProcess("C:\\Windows\\System32\\config\\systemprofile\\AppData\\Roaming\\npm\\tsc.cmd");
    } catch (Exception ex) {
        Information("Got and ignoring error - " + ex.Message + ".\r\nStack trace -\r\n" + ex.StackTrace);
    }
})

    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure that there is a good web.config"
        },
        true
        );
});


//////////////////////////////////////////////////////////////
// Unit Test Tasks (we aren't doing any of these right now)
//////////////////////////////////////////////////////////////

Task("Build-Unit-Tests")
    .WithCriteria(() => DirectoryExists(cakeConfig.UnitTests.UnitTestDirectoryPath))
    .IsDependentOn("Restore-CSharp-NuGet-Packages")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Build Unit Tests.");
    }
    MSBuild(cakeConfig.ProjectInfo.ProjectSolution, new MSBuildSettings()
        .WithTarget(cakeConfig.UnitTests.UnitTestProjectName.Replace('.','_'))
        .SetConfiguration(cakeConfig.MSBuildInfo.msbuildConfig(true))
        .WithProperty("Platform", cakeConfig.MSBuildInfo.platform)        
        .WithProperty("Configuration", cakeConfig.MSBuildInfo.msbuildConfig(true))
        .WithProperty("VisualStudioVersion", cakeConfig.MSBuildInfo.MSBuildVersion)
        .UseToolVersion(MSBuildToolVersion.Default)
        .SetVerbosity(Verbosity.Minimal)
        .SetMaxCpuCount(1));
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Check for xunit syntax/runtime errors",
            "ENSURE THE UNIT TESTS HAVE AT LEAST 1 XUNIT TEST",
            "Check for file locks"
        },
        true
        );
});

Task("Run-Unit-Tests")
    .WithCriteria(() => DirectoryExists(cakeConfig.UnitTests.UnitTestDirectoryPath))
    .IsDependentOn("Build-Unit-Tests")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Run Unit Tests.");
    }
    string targetDir = cakeConfig.UnitTests.UnitTestDirectoryPath.FullPath + "/bin/" + cakeConfig.MSBuildInfo.msbuildConfig(true);
    IEnumerable<FilePath> targetDLLs = new List<FilePath>(){File(targetDir + "/" + cakeConfig.UnitTests.UnitTestProjectName + ".dll")};
    OpenCoverSettings settings = new OpenCoverSettings();
    settings.ArgumentCustomization = args => args.Append(string.Concat("-targetdir:\"" + targetDir + "\""));
    settings.ArgumentCustomization = args => args.Append(string.Concat("-register")); // Magic!
    OpenCover(tool => {
        tool.XUnit2(
            targetDLLs,
            new XUnit2Settings {
                OutputDirectory = cakeConfig.ProjectInfo.ProjectDirectory,
                XmlReport = true,
                Parallelism = ParallelismOption.All, // Like Sanic
                ShadowCopy = false
            });
        },
        cakeConfig.UnitTests.CoverageReportFilePath,
        settings.WithFilter("+[" + cakeConfig.ProjectInfo.ProjectName + "*]*")
    );
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Check for xunit syntax/runtime errors",
            "ENSURE THE UNIT TESTS HAVE AT LEAST 1 XUNIT TEST",
            "Check for file locks"
        },
        true
        );
});

Task("StopAnApplicationPool")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Stopping IIS.");
    }
    if (cakeConfig.ConfigurableSettings.restartIIS) {
        if (cakeConfig.ConfigurableSettings.useRemoteServer)
        {
            StopPool(cakeConfig.ConfigurableSettings.remoteIISServerName, cakeConfig.ConfigurableSettings.applicationPoolName);
            StopSite(cakeConfig.ConfigurableSettings.remoteIISServerName, cakeConfig.ConfigurableSettings.applicationSiteName);
        } else
        {
            StopPool(cakeConfig.ConfigurableSettings.applicationPoolName);
            StopSite(cakeConfig.ConfigurableSettings.applicationSiteName);
        }
    }
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Did all the settings load?",
            "Did you set the pool and site name?",
            "Is IIS running?"
        },
        true
        );
});

Task("StartAnApplicationPool")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting IIS.");
    }
    if (cakeConfig.ConfigurableSettings.restartIIS) {
        if (cakeConfig.ConfigurableSettings.useRemoteServer)
        {
            StartPool(cakeConfig.ConfigurableSettings.remoteIISServerName, cakeConfig.ConfigurableSettings.applicationPoolName);
            StartSite(cakeConfig.ConfigurableSettings.remoteIISServerName, cakeConfig.ConfigurableSettings.applicationSiteName);
        } else
        {
            StartPool(cakeConfig.ConfigurableSettings.applicationPoolName);
            StartSite(cakeConfig.ConfigurableSettings.applicationSiteName);
        }
    }
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Did all the settings load?",
            "Did you set the pool and site name?",
            "Is IIS running?"
        },
        true
        );
});

//////////////////////////////////////////////////////////////
// SonarQube Tasks (We aren't going to use these right now)
//////////////////////////////////////////////////////////////

Task("Start-SonarQube")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting SonarQube.");
    }
    // using (var process = StartAndReturnProcess(
    //     "./tools/SonarQube.MSBuild.Runner/tools/MSBuild.SonarQube.Runner.exe", 
    //     new ProcessSettings()
    //         .WithArguments(
    //             arguments => {
    //                 arguments
    //                     .Append("begin")
    //                     .AppendSwitchQuoted(@"/k", ":", cakeConfig.ProjectInfo.ProjectName)
    //                     .AppendSwitchQuoted(@"/n", ":", cakeConfig.ProjectInfo.ProjectName)
    //                     .AppendSwitchQuoted(@"/v", ":", cakeConfig.Nuget.Version);
    //                 if (!string.IsNullOrEmpty(EnvironmentVariable("SONARQUBE_KEY")))
    //                 {
    //                     arguments
    //                         .AppendSwitchQuoted(@"/d", ":", "sonar.login=" + EnvironmentVariable("SONARQUBE_KEY"));
    //                 }
    //                 if (DirectoryExists(cakeConfig.UnitTests.UnitTestDirectoryPath))
    //                 {
    //                     arguments
    //                         .AppendSwitchQuoted(@"/d", ":", "sonar.cs.opencover.reportsPaths=" + cakeConfig.UnitTests.CoverageReportFilePath)
    //                         .AppendSwitchQuoted(@"/d", ":", "sonar.cs.xunit.reportsPaths=" + cakeConfig.UnitTests.XUnitOutputFile);
    //                 }
    //                 if (!string.IsNullOrEmpty(cakeConfig.UnitTests.JsTestPath))
    //                 {
    //                     arguments
    //                         .AppendSwitchQuoted("/d",":", "sonar.javascript.lcov.reportPath=jsTests.lcov");
    //                 }
    //             }   
    //             )
    //         )
    //     )
    // {
    //     process.WaitForExit();
    //     if (process.GetExitCode() != 0) throw new CakeException("Could not start SonarQube analysis");
    // }
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure java is installed on the machine",
            "ENSURE THE UNIT TESTS HAVE AT LEAST 1 XUNIT TEST",
            "Check for file locks"
        },
        true
        );
});

Task("End-SonarQube")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Complete SonarQube Analysis.");
    }
    // using (var process = StartAndReturnProcess(
    //         "./tools/SonarQube.MSBuild.Runner/tools/MSBuild.SonarQube.Runner.exe", 
    //         new ProcessSettings()
    //             .SetRedirectStandardOutput(true)
    //             .WithArguments(
    //                 arguments => {
    //                     arguments.Append("end");
    //                     }
    //                 )
    //             )
    //         )
    // {
    //     Information("--------------------------------------------------------------------------------");
    //     Information("Starting stdout capture");
    //     Information("--------------------------------------------------------------------------------");
    //     process.WaitForExit();
    //     IEnumerable<string> stdout = process.GetStandardOutput();
    //     Information("Aggregating.....");      
    //     string filename = string.Format("reallyLameFileToNeed{0}.txt",Guid.NewGuid());  
    //     System.IO.File.WriteAllLines(filename, stdout);
    //     cakeConfig.UnitTests.SqAnalysisUrl = GetSonarQubeURL(System.IO.File.ReadAllLines(filename));
    //     DeleteFile(filename);
    //     Information("--------------------------------------------------------------------------------");
    //     Information("Check " + cakeConfig.UnitTests.SqAnalysisUrl + " for a sonarqube update status.");
    //     Information("--------------------------------------------------------------------------------");
    //}
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure java is installed on the machine",
            "ENSURE THE UNIT TESTS HAVE AT LEAST 1 XUNIT TEST",
            "Check for file locks"
        },
        true
        );
});

Task("Check-Quality-Gate")
    .WithCriteria(() => !String.IsNullOrEmpty(cakeConfig.UnitTests.SqAnalysisUrl))
    .Does(() => 
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Check Quality Gate.");
    }
    // cakeConfig.UnitTests.QualityGateReady = IsAnalysisComplete(cakeConfig.UnitTests.SqAnalysisUrl);
    // int timeoutCount = 0;
    // while(!cakeConfig.UnitTests.QualityGateReady) // Giving it up to two minutes to complete
    // {
    //     if (cakeConfig.UnitTests.MaxQualityGateTimeoutCount < timeoutCount) throw new CakeException("Could not get quality gate from SonarQube");
    //     cakeConfig.UnitTests.QualityGateReady = IsAnalysisComplete(cakeConfig.UnitTests.SqAnalysisUrl);
    //     System.Threading.Thread.Sleep(cakeConfig.UnitTests.QualityGateSleepLengthPerCount);
    //     timeoutCount++;
    // }
    // cakeConfig.UnitTests.QualityGateStatus = CheckQualityGate(cakeConfig.ProjectInfo.ProjectName);
    // if (string.IsNullOrEmpty(cakeConfig.UnitTests.QualityGateStatus))
    // {
    //     Environment.Exit(1);
    // }
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure sonarqube is available and not too busy",
            "Try again... the server can get overloaded"
        },
        false
        );
});

//////////////////////////////////////////////////////////////
// Deploy Nuget
//////////////////////////////////////////////////////////////

Task("PackNugetPackage")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Pack Nuget Package.");
    }
    var nuGetPackSettings   = new NuGetPackSettings {
        Id                          = cakeConfig.Nuget.Id ?? "TestNuget",
        Version                     = cakeConfig.Nuget.Version ?? "0.0.0.1",
        Title                       = cakeConfig.Nuget.nugetTitle ?? "The tile of the package",
        Authors                     = cakeConfig.Nuget.nugetAuthors ?? new[] {"John Doe"},
        Owners                      = cakeConfig.Nuget.nugetOwners ?? new[] {"Contoso"},
        Description                 = cakeConfig.Nuget.nugetDescription ?? "The description of the package",
        Summary                     = cakeConfig.Nuget.nugetSummary ?? "Excellent summary of what the package does",
        ProjectUrl                  = cakeConfig.Nuget.nugetProjectUrl ?? new Uri("https://github.com/SomeUser/TestNuget/"),
        IconUrl                     = cakeConfig.Nuget.nugetIconUrl ?? new Uri("http://cdn.rawgit.com/SomeUser/TestNuget/master/icons/testnuget.png"),
        LicenseUrl                  = cakeConfig.Nuget.licenseUrl ?? new Uri("https://github.com/SomeUser/TestNuget/blob/master/LICENSE.md"),
        Copyright                   = cakeConfig.Nuget.copyright ?? "Some company 2015",
        ReleaseNotes                = cakeConfig.Nuget.releaseNotes ?? new [] {"Bug fixes", "Issue fixes", "Typos"},
        Tags                        = cakeConfig.Nuget.tags ?? new [] {"Cake", "Script", "Build"},
        RequireLicenseAcceptance    = cakeConfig.Nuget.requireLicenseAcceptance ?? false,
        Symbols                     = cakeConfig.Nuget.symbols ?? false,
        NoPackageAnalysis           = cakeConfig.Nuget.noPackageAnalysis ?? true,
        Files                       = cakeConfig.Nuget.files
                                        // we want a null here if it is null
                                        // ?? new [] {
                                        //     new NuSpecContent {Source = "bin/TestNuget.dll", Target = "bin"},
                                        // }
                                        ,
        BasePath                    = cakeConfig.Nuget.basePath ?? "./src/TestNuget/bin/release",
        OutputDirectory             = cakeConfig.Nuget.outputDirectory ?? "./nuget"
        IncludeReferencedProjects   = cakeConfig.Nuget.nugetIncludeReferencedProjects ?? false;
    };

    Context.NuGetPack(cakeConfig.Nuget.NugetPackPath ?? "./nuspec/TestNuget.nuspec", nuGetPackSettings);
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure nuspec is possible",
            "Ensure the nuget server is up",
            "Ensure nuget got installed"
        },
        true
        );
});

Task("DeployNugetPackage")
    .Does(() =>
{
    if (cakeConfig.ConfigurableSettings.postSlackSteps)
    {
        cakeConfig.CakeMethods.SendSlackNotification(cakeConfig, "Starting Deploy Nuget Package.");
    }
    NuGetPush(cakeConfig.Nuget.NugetPackPath, new NuGetPushSettings {
        Source = cakeConfig.Nuget.nugetServerFeed ?? "http://example.com/nugetfeed",
        ApiKey = cakeConfig.Nuget.nugetAPIKey
    });
})
    .ReportError(exception =>
{
    cakeConfig.DispalyException(
        exception,
        new string[] {
            "Ensure nuspec exists",
            "Ensure the nuget server is up",
            "Ensure nuget got installed",
            "Ensure NUGET_APIKEY is an environmental variable"
        },
        true
        );
});