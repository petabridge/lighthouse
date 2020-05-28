#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open System.Text

open Fake
open Fake.DotNetCli
open Fake.DocFxHelper

// Information about the project for Nuget and Assembly info files
let product = "Akka.CQRS"
let configuration = "Release"

// Metadata used when signing packages and DLLs
let signingName = "My Library"
let signingDescription = "My REALLY COOL Library"
let signingUrl = "https://signing.is.cool/"

// Read release notes and version
let solutionFile = FindFirstMatchingFile "*.sln" __SOURCE_DIRECTORY__  // dynamically look up the solution
let buildNumber = environVarOrDefault "BUILD_NUMBER" "0"
let hasTeamCity = (not (buildNumber = "0")) // check if we have the TeamCity environment variable for build # set
let preReleaseVersionSuffix = "beta" + (if (not (buildNumber = "0")) then (buildNumber) else DateTime.UtcNow.Ticks.ToString())
let versionSuffix = 
    match (getBuildParam "nugetprerelease") with
    | "dev" -> preReleaseVersionSuffix
    | _ -> ""

let releaseNotes =
    File.ReadLines "./RELEASE_NOTES.md"
    |> ReleaseNotesHelper.parseReleaseNotes

// Directories
let toolsDir = __SOURCE_DIRECTORY__ @@ "tools"
let output = __SOURCE_DIRECTORY__  @@ "bin"
let outputTests = __SOURCE_DIRECTORY__ @@ "TestResults"
let outputPerfTests = __SOURCE_DIRECTORY__ @@ "PerfResults"
let outputNuGet = output @@ "nuget"

exception ConnectionFailure of string

Target "Clean" (fun _ ->
    ActivateFinalTarget "KillCreatedProcesses"

    CleanDir output
    CleanDir outputTests
    CleanDir outputPerfTests
    CleanDir outputNuGet
    CleanDir "docs/_site"
)

Target "AssemblyInfo" (fun _ ->
    XmlPokeInnerText "./src/common.props" "//Project/PropertyGroup/VersionPrefix" releaseNotes.AssemblyVersion    
    XmlPokeInnerText "./src/common.props" "//Project/PropertyGroup/PackageReleaseNotes" (releaseNotes.Notes |> String.concat "\n")
)

Target "Build" (fun _ ->          
    DotNetCli.Build
        (fun p -> 
            { p with
                Project = solutionFile
                Configuration = configuration }) // "Rebuild"  
)


//--------------------------------------------------------------------------------
// Tests targets 
//--------------------------------------------------------------------------------
module internal ResultHandling =
    let (|OK|Failure|) = function
        | 0 -> OK
        | x -> Failure x

    let buildErrorMessage = function
        | OK -> None
        | Failure errorCode ->
            Some (sprintf "xUnit2 reported an error (Error Code %d)" errorCode)

    let failBuildWithMessage = function
        | DontFailBuild -> traceError
        | _ -> (fun m -> raise(FailedTestsException m))

    let failBuildIfXUnitReportedError errorLevel =
        buildErrorMessage
        >> Option.iter (failBuildWithMessage errorLevel)

Target "RunTests" (fun _ ->
    let projects = 
        match (isWindows) with 
        | true -> !! "./src/**/*.Tests.csproj"
        | _ -> !! "./src/**/*.Tests.csproj" // if you need to filter specs for Linux vs. Windows, do it here

    let runSingleProject project =
        let arguments =
            match (hasTeamCity) with
            | true -> (sprintf "test -c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --results-directory %s -- -parallel none -teamcity" (outputTests))
            | false -> (sprintf "test -c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --results-directory %s -- -parallel none" (outputTests))

        let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0) 
        
        ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result  

    projects |> Seq.iter (log)
    projects |> Seq.iter (runSingleProject)
)

Target "NBench" <| fun _ ->
    let projects = 
        match (isWindows) with 
        | true -> !! "./src/**/*.Tests.Performance.csproj"
        | _ -> !! "./src/**/*.Tests.Performance.csproj" // if you need to filter specs for Linux vs. Windows, do it here


    let runSingleProject project =
        let arguments =
            match (hasTeamCity) with
            | true -> (sprintf "nbench --nobuild --teamcity --concurrent true --trace true --output %s" (outputPerfTests))
            | false -> (sprintf "nbench --nobuild --concurrent true --trace true --output %s" (outputPerfTests))

        let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0) 
        
        ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result
    
    projects |> Seq.iter runSingleProject
    
Target "RunTestsOnRuntimes" (fun _ ->

    let LighthouseConnectTimeout = 20.0 // in seconds

    let dockerFileForTest = 
        match (isWindows) with
        | true -> "src/Lighthouse/Dockerfile-windows" 
        | _ -> "src/Lighthouse/Dockerfile-linux" 
    
    let installPbm () =
        // Install pbm client to test connections
        ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.Arguments <- "tool install --global pbm") (TimeSpan.FromMinutes 5.0) |> ignore // this is fine if tool is already installed

    let startLighthouseDocker dockerFile =
        printfn "Starting Lighthouse..."
        let runArgs = "run -d --name lighthouse --hostname lighthouse1 -p 4053:4053 -p 9110:9110 --env CLUSTER_IP=127.0.0.1 --env CLUSTER_SEEDS=akka.tcp://some@lighthouse1:4053 --env CLUSTER_PORT=4053 lighthouse:latest"
        let runResult = ExecProcess(fun info -> 
            info.FileName <- "docker"
            info.WorkingDirectory <- (Directory.GetParent dockerFile).FullName
            info.Arguments <- runArgs) (System.TimeSpan.FromMinutes 5.0) 
        if runResult <> 0 then failwith "Unable to start Lighthouse in Docker"
    
    let stopLighthouseDocker dockerFile = 
        printfn "Stopping Lighthouse..."
        ExecProcess(fun info -> 
                info.FileName <- "docker"
                info.WorkingDirectory <- (Directory.GetParent dockerFile).FullName
                info.Arguments <- "rm -f lighthouse") (System.TimeSpan.FromMinutes 5.0) |> ignore // cleanup failure should not fail the test
                
    let startLighhouseLocally exePath =
        printfn "Starting Lighthouse locally..."
        try
            let runResult = ExecProcess(fun info -> 
                info.FileName <- exePath) (System.TimeSpan.FromSeconds LighthouseConnectTimeout) 
            if runResult <> 0 then failwithf "Unable to start Lighthouse from %s" exePath
        with 
            | _ -> () // Local instance process should just timeout, this is fine
                
    let connectLighthouse () =
        printfn "Connecting Lighthouse..."
        try
            ExecProcess(fun info -> 
                info.FileName <- "pbm") (System.TimeSpan.FromSeconds LighthouseConnectTimeout) |> ignore
            // If process returned, this means that pbm failed to connect
            raise (ConnectionFailure "Failed to connect Lighthouse from pbm")
        with
            | ConnectionFailure(str) -> reraise()
            // If timed out, Lighthouse was connected successfully
            | _ -> printfn "Lighthouse was connected successfully"
    
    installPbm()
    startLighthouseDocker dockerFileForTest
    try       
        connectLighthouse()
    finally
        stopLighthouseDocker dockerFileForTest
        
    // Test Full .NET Framework version under windows only
    // TODO: To make this work, need to start lighthouse and pbm as two parallel processes
    (*
    match (isWindows) with
            | true -> 
                startLighhouseLocally "src/Lighthouse/bin/Release/net461/Lighthouse.exe"
                connectLighthouse()
            | _ -> ()
    *)
)


//--------------------------------------------------------------------------------
// Code signing targets
//--------------------------------------------------------------------------------
Target "SignPackages" (fun _ ->
    let canSign = hasBuildParam "SignClientSecret" && hasBuildParam "SignClientUser"
    if(canSign) then
        log "Signing information is available."
        
        let assemblies = !! (outputNuGet @@ "*.nupkg")

        let signPath =
            let globalTool = tryFindFileOnPath "SignClient.exe"
            match globalTool with
                | Some t -> t
                | None -> if isWindows then findToolInSubPath "SignClient.exe" "tools/signclient"
                          elif isMacOS then findToolInSubPath "SignClient" "tools/signclient"
                          else findToolInSubPath "SignClient" "tools/signclient"

        let signAssembly assembly =
            let args = StringBuilder()
                    |> append "sign"
                    |> append "--config"
                    |> append (__SOURCE_DIRECTORY__ @@ "appsettings.json") 
                    |> append "-i"
                    |> append assembly
                    |> append "-r"
                    |> append (getBuildParam "SignClientUser")
                    |> append "-s"
                    |> append (getBuildParam "SignClientSecret")
                    |> append "-n"
                    |> append signingName
                    |> append "-d"
                    |> append signingDescription
                    |> append "-u"
                    |> append signingUrl
                    |> toText

            let result = ExecProcess(fun info -> 
                info.FileName <- signPath
                info.WorkingDirectory <- __SOURCE_DIRECTORY__
                info.Arguments <- args) (System.TimeSpan.FromMinutes 5.0) (* Reasonably long-running task. *)
            if result <> 0 then failwithf "SignClient failed.%s" args

        assemblies |> Seq.iter (signAssembly)
    else
        log "SignClientSecret not available. Skipping signing"
)

//--------------------------------------------------------------------------------
// Nuget targets 
//--------------------------------------------------------------------------------

let overrideVersionSuffix (project:string) =
    match project with
    | _ -> versionSuffix // add additional matches to publish different versions for different projects in solution
Target "CreateNuget" (fun _ ->    
    let projects = !! "src/**/*.csproj" 
                   -- "src/**/*Tests.csproj" // Don't publish unit tests
                   -- "src/**/*Tests*.csproj"

    let runSingleProject project =
        DotNetCli.Pack
            (fun p -> 
                { p with
                    Project = project
                    Configuration = configuration
                    AdditionalArgs = ["--include-symbols --no-build"]
                    VersionSuffix = overrideVersionSuffix project
                    OutputPath = outputNuGet })

    projects |> Seq.iter (runSingleProject)
)

Target "PublishNuget" (fun _ ->
    let projects = !! "./bin/nuget/*.nupkg" -- "./bin/nuget/*.symbols.nupkg"
    let apiKey = getBuildParamOrDefault "nugetkey" ""
    let source = getBuildParamOrDefault "nugetpublishurl" ""
    let symbolSource = getBuildParamOrDefault "symbolspublishurl" ""
    let shouldPublishSymbolsPackages = not (symbolSource = "")

    if (not (source = "") && not (apiKey = "") && shouldPublishSymbolsPackages) then
        let runSingleProject project =
            DotNetCli.RunCommand
                (fun p -> 
                    { p with 
                        TimeOut = TimeSpan.FromMinutes 10. })
                (sprintf "nuget push %s --api-key %s --source %s --symbol-source %s" project apiKey source symbolSource)

        projects |> Seq.iter (runSingleProject)
    else if (not (source = "") && not (apiKey = "") && not shouldPublishSymbolsPackages) then
        let runSingleProject project =
            DotNetCli.RunCommand
                (fun p -> 
                    { p with 
                        TimeOut = TimeSpan.FromMinutes 10. })
                (sprintf "nuget push %s --api-key %s --source %s" project apiKey source)

        projects |> Seq.iter (runSingleProject)
)

//--------------------------------------------------------------------------------
// Docker images
//--------------------------------------------------------------------------------  
Target "PublishCode" (fun _ ->    
    let projects = !! "src/**/Lighthouse.csproj" // publish services only

    let runSingleProject project =
        DotNetCli.Publish
            (fun p -> 
                { p with
                    Project = project
                    Configuration = configuration
                    VersionSuffix = overrideVersionSuffix project
                    Framework = "netcoreapp3.1"
                    })

    projects |> Seq.iter (runSingleProject)
)

let mapDockerImageName (projectName:string) =
    match projectName with
    | "Lighthouse" -> Some("lighthouse")
    | _ -> None

let composedGetDirName (p:string) =
    System.IO.Path.GetDirectoryName p

let composedGetFileNameWithoutExtension (p:string) =
    System.IO.Path.GetFileNameWithoutExtension p

Target "BuildDockerImages" (fun _ ->
    let projects = !! "src/**/*.csproj" 
                   -- "src/**/*Tests.csproj" // Don't publish unit tests
                   -- "src/**/*Tests*.csproj"

    let dockerFile = 
        match (isWindows) with 
        | true -> "Dockerfile-windows"
        | _ -> "Dockerfile-linux"

    let dockerTags (imageName:string, assemblyVersion:string) =
        match(isWindows) with
        | true -> [| imageName + ":" + releaseNotes.AssemblyVersion; imageName + ":" + releaseNotes.AssemblyVersion + "-nanoserver1803"; imageName + ":latest" |]
        | _ -> [| imageName + ":" + releaseNotes.AssemblyVersion; imageName + ":" + releaseNotes.AssemblyVersion + "-linux"; imageName + ":latest" |]


    let remoteRegistryUrl = getBuildParamOrDefault "remoteRegistry" ""

    let buildDockerImage imageName projectPath =
        
        let args = 
            if(hasBuildParam "remoteRegistry") then
                StringBuilder()
                    |> append "build"
                    |> append "-f"
                    |> append dockerFile
                    |> append "-t"
                    |> append (imageName + ":" + releaseNotes.AssemblyVersion) 
                    |> append "-t"
                    |> append (imageName + ":latest") 
                    |> append "-t"
                    |> append (remoteRegistryUrl + "/" + imageName + ":" + releaseNotes.AssemblyVersion) 
                    |> append "-t"
                    |> append (remoteRegistryUrl + "/" + imageName + ":latest") 
                    |> append "."
                    |> toText
            else
                StringBuilder()
                    |> append "build"
                    |> append "-f"
                    |> append dockerFile
                    |> append "-t"
                    |> append (imageName + ":" + releaseNotes.AssemblyVersion) 
                    |> append "-t"
                    |> append (imageName + ":latest") 
                    |> append "."
                    |> toText

        ExecProcess(fun info -> 
                info.FileName <- "docker"
                info.WorkingDirectory <- composedGetDirName projectPath
                info.Arguments <- args) (System.TimeSpan.FromMinutes 5.0) (* Reasonably long-running task. *)

    let runSingleProject project =
        let projectName = composedGetFileNameWithoutExtension project
        let imageName = mapDockerImageName projectName
        let result = match imageName with
                        | None -> 0
                        | Some(name) -> buildDockerImage name project
        if result <> 0 then failwithf "docker build failed. %s" project

    projects |> Seq.iter (runSingleProject)
)

//--------------------------------------------------------------------------------
// Documentation 
//--------------------------------------------------------------------------------  
Target "DocFx" (fun _ ->
    DotNetCli.Restore (fun p -> { p with Project = solutionFile })
    DotNetCli.Build (fun p -> { p with Project = solutionFile; Configuration = configuration })

    let docsPath = "./docs"

    DocFx (fun p -> 
                { p with 
                    Timeout = TimeSpan.FromMinutes 30.0; 
                    WorkingDirectory  = docsPath; 
                    DocFxJson = docsPath @@ "docfx.json" })
)

//--------------------------------------------------------------------------------
// Cleanup
//--------------------------------------------------------------------------------

FinalTarget "KillCreatedProcesses" (fun _ ->
    log "Shutting down dotnet build-server"
    let result = ExecProcess(fun info -> 
            info.FileName <- "dotnet"
            info.WorkingDirectory <- __SOURCE_DIRECTORY__
            info.Arguments <- "build-server shutdown") (System.TimeSpan.FromMinutes 2.0)
    if result <> 0 then failwithf "dotnet build-server shutdown failed"
)

//--------------------------------------------------------------------------------
// Help 
//--------------------------------------------------------------------------------

Target "Help" <| fun _ ->
    List.iter printfn [
      "usage:"
      "./build.ps1 [target]"
      ""
      " Targets for building:"
      " * Build         Builds"
      " * Nuget         Create and optionally publish nugets packages"
      " * SignPackages  Signs all NuGet packages, provided that the following arguments are passed into the script: SignClientSecret={secret} and SignClientUser={username}"
      " * RunTests      Runs tests"
      " * All           Builds, run tests, creates and optionally publish nuget packages"
      " * DocFx         Creates a DocFx-based website for this solution"
      ""
      " Other Targets"
      " * Help       Display this help" 
      ""]

//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------

Target "BuildRelease" DoNothing
Target "All" DoNothing
Target "Docker" DoNothing
Target "Nuget" DoNothing

// build dependencies
"Clean" ==> "AssemblyInfo" ==> "Build" ==> "BuildRelease"

// tests dependencies
"Build" ==> "RunTests"
"PublishCode" ==> "BuildDockerImages" ==> "RunTestsOnRuntimes"

// nuget dependencies
"Clean" ==> "Build" ==> "CreateNuget"
"CreateNuget" ==> "SignPackages" ==> "PublishNuget" ==> "Nuget"

// docs
"Clean" ==> "BuildRelease" ==> "Docfx"

// Docker
"PublishCode" ==> "BuildDockerImages" ==> "Docker"

// all
"BuildRelease" ==> "All"
"RunTests" ==> "All"
"NBench" ==> "All"
"Nuget" ==> "All"

RunTargetOrDefault "Help"