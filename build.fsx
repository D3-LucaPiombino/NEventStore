#r @"artifacts/#build_deps/FAKE/4.12.0/tools/FakeLib.dll"

open System.IO
open Fake
open Fake.AssemblyInfoFile
open Fake.Git.Information
open Fake.SemVerHelper
open Fake.Testing.XUnit2


let buildArtifactPath = "./artifacts/nuget_packages"
let nugetWorkingPath = FullName "./artifacts/#temp"



let assemblyVersion = "6.0.0.0"
let baseVersion = "6.0.0"

let semVersion : SemVerInfo = parse baseVersion

let Version = semVersion.ToString()

let branch = (fun _ ->
  (environVarOrDefault "APPVEYOR_REPO_BRANCH" (getBranchName "."))
)

let FileVersion = (environVarOrDefault "APPVEYOR_BUILD_VERSION" (Version + "." + "0"))

let informationalVersion = (fun _ ->
  let branchName = (branch ".")
  let label = " (" + branchName + "/" + (getCurrentSHA1 ".").[0..7] + ")"
  (FileVersion + label)
)

let nugetVersion = (fun _ ->
  let branchName = (branch ".")
  let label = if branchName="master" then "" else "-" + branchName
  (Version + label)
)

let InfoVersion = informationalVersion()
let NuGetVersion = nugetVersion()


printfn "Using version: %s" Version

Target "Clean" (fun _ ->
  ensureDirectory buildArtifactPath
  ensureDirectory nugetWorkingPath

  CleanDir buildArtifactPath
  CleanDir nugetWorkingPath
)

//Target "RestorePackages" (fun _ -> 
//     "./src/MassTransit.sln"
//     |> RestoreMSSolutionPackages (fun p ->
//         { p with
//             OutputPath = packagesPath
//             Retries = 4 })
//)

Target "Build" (fun _ ->

  CreateCSharpAssemblyInfo @".\src\VersionAssemblyInfo.cs"
    [ 
      Attribute.Version assemblyVersion
      Attribute.FileVersion FileVersion
      Attribute.InformationalVersion InfoVersion
    ]

  let buildMode = getBuildParamOrDefault "buildMode" "Release"
  let setParams defaults = { 
    defaults with
        Verbosity = Some(Quiet)
        Targets = ["Clean"; "Build"]
        Properties =
            [
                "Optimize", "True"
                "DebugSymbols", "True"
                //"RestorePackages", "True"
                "Configuration", buildMode
                "TargetFrameworkVersion", "v4.5"
                "Platform", "Any CPU"
            ]
  }

  build setParams @".\src\NEventStore.sln"
      |> DoNothing

  let unsignedSetParams defaults = { 
    defaults with
        Verbosity = Some(Quiet)
        Targets = ["Build"]
        Properties =
            [
                "Optimize", "True"
                "DebugSymbols", "True"
                "Configuration", "Release"
                "TargetFrameworkVersion", "v4.5"
                "Platform", "Any CPU"
            ]
  }




  build unsignedSetParams @".\src\NEventStore.sln"
      |> DoNothing
)

let testDlls = [ "./src/*.Tests/bin/Release/*.Tests.dll" ]


Target "UnitTests" (fun _ ->
    testDlls 
        |> Seq.collect (fun glob -> !!glob)
        |> xUnit2 (fun p -> 
            {p with HtmlOutputPath = Some (buildArtifactPath @@ "xunit.html")})
)

type packageInfo = {
    Project: string
    PackageFile: string
    Summary: string
    Files: list<string*string option*string option>
}

Target "Package" (fun _ ->

  let nugs = [| { Project = "NEventStore"
                  Summary = "NEventStore is a persistence agnostic event sourcing library for .NET. The primary use is most often associated with CQRS."
                  PackageFile = @".\src\MassTransit\packages.config"
                  Files = [ (@"..\src\MassTransit\bin\Release\MassTransit.*", Some @"lib\net45", None);
                            (@"..\src\MassTransit\**\*.cs", Some "src", None) ] }
             |]

  nugs
    |> Array.iter (fun nug ->

      let getDeps daNug : NugetDependencies =
        if daNug.Project = "MassTransit" then (getDependencies daNug.PackageFile)
        else if daNug.Project = "MassTransit.Host" then (("MassTransit", NuGetVersion) :: ("MassTransit.Autofac", NuGetVersion) :: ("MassTransit.Log4Net", NuGetVersion) :: (getDependencies daNug.PackageFile))
        else ("MassTransit", NuGetVersion) :: (getDependencies daNug.PackageFile)

      let setParams defaults = {
        defaults with 
          Authors = ["NEventStore Dev Team" ]
          Description = "The purpose of the EventStore is to represent a series of events as a stream. Furthermore, it provides hooks whereby any events committed to the stream can be dispatched to interested parties."
          OutputPath = buildArtifactPath
          Project = nug.Project
          Dependencies = (getDeps nug)
          Summary = nug.Summary
          SymbolPackage = NugetSymbolPackage.Nuspec
          Version = NuGetVersion
          WorkingDir = nugetWorkingPath
          Files = nug.Files
      } 

      NuGet setParams (FullName "./template.nuspec")
    )
)

Target "Default" (fun _ ->
  trace "Build starting..."
)


Target "DebugTest" (fun _ ->

    let testDlls2 = [ "**/bin/Release/*.Tests.dll" ] |> Seq.collect (fun glob -> !!glob) 
    // trace glob
    let i = [| 
        for globexpr in testDlls2 do
            for file in !! globexpr -> file
    |]
    i
    |> Seq.iter (fun file -> trace file )

)

"Clean"
//  ==> "RestorePackages"
  ==> "Build"
  // ==> "Package"
  ==> "UnitTests"
  ==> "Default"

RunTargetOrDefault "Default"