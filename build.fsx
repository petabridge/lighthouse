#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open System.Text

open Fake
open Fake.RestorePackageHelper
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

//--------------------------------------------------------------------------------
// Directories
//--------------------------------------------------------------------------------

let output = __SOURCE_DIRECTORY__  @@ "bin"
let outputNuGet = output @@ "nuget"
let outputChocolatey = output @@ "chocolatey"
let outputZip = output @@ "zipRelease"
let workingNuGet = output @@ "build"

Target "Clean" (fun _ ->
    CleanDir output
    CleanDir outputNuGet
    CleanDir outputChocolatey
    CleanDir outputZip
    CleanDir workingNuGet
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

// all
"BuildRelease" ==> "All"
"RunTests" ==> "All"

RunTargetOrDefault "Help"