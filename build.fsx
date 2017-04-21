#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open Fake

//--------------------------------------------------------------------------------
// Build targets
//--------------------------------------------------------------------------------

Target "Clean" (fun _ ->
    CleanDirs !! "./**/bin"
    CleanDirs !! "./**/obj"
)

Target "RestorePackages" (fun _ ->
    let solution = "./src/Petabridge.Cmd.sln"

    solution 
    |> RestoreMSSolutionPackages 
        (fun p ->
            { p with
                OutputPath = "./packages"
                Retries = 4 })
)

Target "Build" (fun _ ->      
    let solution = Seq.singleton "./Lighthouse.sln"

    solution
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

Target "BuildDockerImage" (fun _ ->
    let result = ExecProcess (fun info ->
        info.FileName <- "powershell.exe"
        info.Arguments <- "./build-docker-image.ps1") (System.TimeSpan.FromMinutes 5.0)
    enableProcessTracing <- true
    if result <> 0 then failwith "couldn't build docker image"
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
      " * Docker     Builds Docker Image"

      ""
      " Other Targets"
      " * Help       Display this help" 
      ""]

Target "BuildRelease" DoNothing
Target "Docker" DoNothing

// build dependencies
"Clean" ==> "Build" ==> "BuildRelease"
"Clean" ==> "Build" ==> "BuildDockerImage" ==> "Docker"

RunTargetOrDefault "Help"