#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open System.Text

open Fake
open Fake.RestorePackageHelper
open Fake.DocFxHelper
open Fake.TaskRunnerHelper
open Fake.Testing
open Fake.AssemblyInfoFile

//--------------------------------------------------------------------------------
// Information about the project for Nuget and Assembly info files
//--------------------------------------------------------------------------------

let product = "Petabridge.Cmd"
let authors = [ "Petabridge, LLC" ]
let copyright = "Copyright 2015-2017"
let company = "Petabridge"
let description = ""
let tags = ["akka.net"; "akka"; "cli"; "management"; "cluster"]
let configuration = "Release"

//--------------------------------------------------------------------------------
// Read release notes and version
//--------------------------------------------------------------------------------

let parsedRelease =
    File.ReadLines "./RELEASE_NOTES.md"
    |> ReleaseNotesHelper.parseReleaseNotes

let envBuildNumber = System.Environment.GetEnvironmentVariable("BUILD_NUMBER") //populated by TeamCity build agent
let buildNumber = if String.IsNullOrWhiteSpace(envBuildNumber) then "0" else envBuildNumber
let version = parsedRelease.AssemblyVersion + "." + buildNumber
let preReleaseVersion = version + "-beta" //suffixes the assembly for pre-releases
let isUnstableDocs = hasBuildParam "unstable"
let isPreRelease = hasBuildParam "nugetprerelease"
let release = if isPreRelease then ReleaseNotesHelper.ReleaseNotes.New(version, version + "-beta", parsedRelease.Notes) else parsedRelease
let supportedAkkaVersion = "1.2.3"

//--------------------------------------------------------------------------------
// Directories
//--------------------------------------------------------------------------------

let output = __SOURCE_DIRECTORY__  @@ "bin"
let outputNuGet = output @@ "nuget"
let outputChocolatey = output @@ "chocolatey"
let outputZip = output @@ "zipRelease"
let workingNuGet = output @@ "build"
let workingDocfx = FullName "./docs" @@ "_site"
log workingDocfx

Target "Clean" (fun _ ->
    CleanDir output
    CleanDir outputNuGet
    CleanDir workingNuGet
    CleanDir workingDocfx
    CleanDirs !! "./**/bin"
    CleanDirs !! "./**/obj"
)

//--------------------------------------------------------------------------------
// Build targets
//--------------------------------------------------------------------------------

Target "RestorePackages" (fun _ ->
    let solution = "./src/LightHouse.sln"

    solution 
    |> RestoreMSSolutionPackages 
        (fun p ->
            { p with
                OutputPath = "./packages"
                Retries = 4 })
)

Target "Build" (fun _ ->      
    let solution = Seq.singleton "./src/Lighthouse.sln"

    solution
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

Target "AssemblyInfo" (fun _ ->
    let version = release.AssemblyVersion

    CreateCSharpAssemblyInfoWithConfig "src/SharedAssemblyInfo.cs" [
        Attribute.Company company
        Attribute.Copyright copyright
        Attribute.Version version
        Attribute.FileVersion version ] <| AssemblyInfoFileConfig(false)
)

//--------------------------------------------------------------------------------
// Test targets
//--------------------------------------------------------------------------------

Target "RunTests" (fun _ ->
        let xunitToolPath = findToolInSubPath "xunit.console.exe" "./tools/xunit.runner.console/tools"        
        let testAssemblies = !! "./src/**/bin/Release/*.Tests.dll"

        let runSingleAssembly assembly =
            xUnit2
                (fun p -> { p with ToolPath = xunitToolPath }) 
                (Seq.singleton assembly)

        testAssemblies |> Seq.iter runSingleAssembly
)

Target "CopyOutput" (fun _ ->        
        let copyOutput project = 
            let sourceDir = "./src" @@ project @@ "bin" @@ "Release"
            let destDir = output @@ project
            CopyDir destDir sourceDir allFiles
        
        let projects = [ "Lighthouse" ]
        
        projects |> List.iter copyOutput
)

//--------------------------------------------------------------------------------
// NuGet targets
//--------------------------------------------------------------------------------

Target "CreateNuget" (fun _ ->
    let getLighthouseDependencies project = // no project dependencies
        match project with
        | _ -> []

    let getProjectVersion project =
         match project with
         | _ -> release.NugetVersion

    let mutable dirName = 1
    let removeDir dir = 
        let del _ = 
            DeleteDir dir
            not (directoryExists dir)
        runWithRetries del 3 |> ignore

    let getDirName workingDir dirCount =
        workingDir + dirCount.ToString()

    let getReleaseFiles project releaseDir =
        match project with
        | _ ->
            !! (releaseDir @@ project + ".dll")
            ++ (releaseDir @@ project + ".exe")
            ++ (releaseDir @@ project + ".pdb")
            ++ (releaseDir @@ project + ".xml")

    let getExternalPackages project packagesFile =
        match project with
        | "Lighthouse" -> getDependencies packagesFile
        | _ -> []

    CleanDir workingNuGet

    ensureDirectory outputNuGet
    let nuspecFiles = !! "src/**/*.nuspec"

    for nuspec in nuspecFiles do
        printfn "Creating nuget packages for %s" nuspec
        
        let project = Path.GetFileNameWithoutExtension nuspec 
        let projectDir = Path.GetDirectoryName nuspec
        let projectFile = (!! (projectDir @@ project + ".*sproj")) |> Seq.head
        let releaseDir = projectDir @@ @"bin\Release"
        let packages = projectDir @@ "packages.config"
        let packageDependencies = getExternalPackages project packages
        let dependencies = packageDependencies @ getLighthouseDependencies project
        let releaseVersion = getProjectVersion project

        let pack outputDir symbolPackage =
            NuGetHelper.NuGet
                (fun p ->
                    { p with
                        Description = description
                        Authors = authors
                        Copyright = copyright
                        Project =  project
                        Properties = ["Configuration", "Release"]
                        ReleaseNotes = release.Notes |> String.concat "\n"
                        Version = releaseVersion
                        Tags = tags |> String.concat " "
                        OutputPath = outputDir
                        WorkingDir = workingNuGet
                        SymbolPackage = symbolPackage
                        Dependencies = dependencies })
                nuspec

        // Copy dll, pdb and xml to libdir = workingDir/lib/net45/
        let libDir = workingNuGet @@ @"lib\net45"
        printfn "Creating output directory %s" libDir
        ensureDirectory libDir
        CleanDir libDir
        getReleaseFiles project releaseDir
        |> CopyFiles libDir

        // Copy all src-files (.cs and .fs files) to workingDir/src
        let nugetSrcDir = workingNuGet @@ @"src/"
        CleanDir nugetSrcDir

        let isCs = hasExt ".cs"
        let isFs = hasExt ".fs"
        let isAssemblyInfo f = (filename f).Contains("AssemblyInfo")
        let isSrc f = (isCs f || isFs f) && not (isAssemblyInfo f) 
        CopyDir nugetSrcDir projectDir isSrc
        
        //Remove workingDir/src/obj and workingDir/src/bin
        removeDir (nugetSrcDir @@ "obj")
        removeDir (nugetSrcDir @@ "bin")

        // Create both normal nuget package and symbols nuget package. 
        // Uses the files we copied to workingDir and outputs to nugetdir
        pack outputNuGet NugetSymbolPackage.Nuspec
)

Target "PublishNuget" (fun _ ->
    let rec publishPackage url accessKey trialsLeft packageFile =
        let nugetExe = "./tools/nuget.exe"
        let tracing = enableProcessTracing
        enableProcessTracing <- false
        let args p =
            match p with
            | (pack, key, "") -> sprintf "push \"%s\" %s" pack key
            | (pack, key, url) -> sprintf "push \"%s\" %s -source %s" pack key url

        tracefn "Pushing %s Attempts left: %d" (FullName packageFile) trialsLeft
        try 
            let result = ExecProcess (fun info -> 
                    info.FileName <- nugetExe
                    info.WorkingDirectory <- (Path.GetDirectoryName (FullName packageFile))
                    info.Arguments <- args (packageFile, accessKey,url)) (System.TimeSpan.FromMinutes 1.0)
            enableProcessTracing <- tracing
            if result <> 0 then failwithf "Error during NuGet symbol push. %s %s" nugetExe (args (packageFile, "key omitted",url))
        with exn -> 
            if (trialsLeft > 0) then (publishPackage url accessKey (trialsLeft-1) packageFile)
            else raise exn
    let shouldPushNugetPackages = hasBuildParam "nugetkey"
    let shouldPushSymbolsPackages = (hasBuildParam "symbolspublishurl") && (hasBuildParam "symbolskey")
    
    if (shouldPushNugetPackages || shouldPushSymbolsPackages) then
        printfn "Pushing nuget packages"
        if shouldPushNugetPackages then
            let normalPackages= 
                !! (outputNuGet @@ "*.nupkg") 
                -- (outputNuGet @@ "*.symbols.nupkg") |> Seq.sortBy(fun x -> x.ToLower())
            for package in normalPackages do
                try
                    publishPackage (getBuildParamOrDefault "nugetpublishurl" "") (getBuildParam "nugetkey") 3 package
                with exn ->
                    printfn "%s" exn.Message

        if shouldPushSymbolsPackages then
            let symbolPackages= !! (outputNuGet @@ "*.symbols.nupkg") |> Seq.sortBy(fun x -> x.ToLower())
            for package in symbolPackages do
                try
                    publishPackage (getBuildParam "symbolspublishurl") (getBuildParam "symbolskey") 3 package
                with exn ->
                    printfn "%s" exn.Message
)


//--------------------------------------------------------------------------------
// DocFx targets
//--------------------------------------------------------------------------------

Target "DocFx" (fun _ ->
    let docFxToolPath = findToolInSubPath "docfx.exe" "./tools/docfx.console/tools" 
    let docsPath = "./docs"

    DocFx (fun p -> 
                { p with 
                    Timeout = TimeSpan.FromMinutes 5.0; 
                    WorkingDirectory  = docsPath; 
                    DocFxJson = docsPath @@ "docfx.json" })
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
      " * Build      Builds"
      " * Nuget      Create and optionally publish nugets packages"
      " * RunTests   Runs tests"
      " * All        Builds, run tests, creates and optionally publish nuget packages"
      " * DocFx       Builds and creates documentation"
      ""
      " Other Targets"
      " * Help       Display this help" 
      ""]

//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------

Target "BuildRelease" DoNothing
Target "Nuget" DoNothing
Target "All" DoNothing

// build dependencies
"Clean" ==> "RestorePackages" ==> "AssemblyInfo" ==> "Build" ==> "CopyOutput" ==> "BuildRelease"

// tests dependencies
"Clean" ==> "RestorePackages" ==> "Build" ==> "RunTests"

// nuget dependencies
"Clean" ==> "RestorePackages" ==> "BuildRelease" ==> "CreateNuget"
"CreateNuget" ==> "PublishNuget" ==> "Nuget"

// all
"BuildRelease" ==> "All"
"RunTests" ==> "All"

RunTargetOrDefault "Help"