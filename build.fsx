#I @"artifacts/#build_deps/FAKE/4.12.0/tools"
#r @"FakeLib.dll"
#load @"build/common.fsx"
#load "artifacts/#build_deps/SourceLink.Fake/1.1.0/tools/SourceLink.fsx"

open System.IO
open Fake
open Fake.AssemblyInfoFile
open Fake.Git.Information
open Fake.SemVerHelper
open Fake.Testing.XUnit2
open FSharp.Data;
open SourceLink;


let nugetPackageRepositoryPath = FullName "./artifacts/#build_deps"
let buildArtifactPath = "./artifacts/nuget_packages"
let nugetWorkingPath = FullName "./artifacts/#temp"
let appveryorNugetAccountFeed = environVarOrDefault "APPVEYOR_NUGET_ACCOUNT_FEED" ""

let assemblyVersion = "6.0.0.0"
let baseVersion = "6.0.0"

let semVersion : SemVerInfo = parse baseVersion

let Version = semVersion.ToString()

let branch = (fun _ ->
  (environVarOrDefault "APPVEYOR_REPO_BRANCH" (getBranchName "."))
)

let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/D3-LucaPiombino"

let FileVersion = (environVarOrDefault "APPVEYOR_BUILD_VERSION" (Version + "." + "0"))

let informationalVersion = (fun _ ->
  let branchName = (branch ".")
  let label = " (" + branchName + "/" + (getCurrentSHA1 ".").[0..7] + ")"
  (FileVersion + label)
)

let nugetVersion = (fun _ ->
  let branchName = (branch ".").Replace("_", "")

  let label = if branchName="master" then "" else "-" + branchName.Substring(0, min 20 branchName.Length)
  (Version + label)
)

let InfoVersion = informationalVersion()
let NuGetVersion = nugetVersion()

type packageInfo = {
    Project: string
    ProjectJson: string
    Summary: string
    Dependencies : NugetDependencies -> NugetDependencies 
    Files: list<string*string option*string option>
}

let nugs = [| { Project = "NEventStore"
                Summary = "NEventStore is a persistence agnostic event sourcing library for .NET. The primary use is most often associated with CQRS."
                ProjectJson = @".\src\NEventStore\project.json"
                Files = [ (@"..\..\src\NEventStore\bin\Release\NEventStore*", Some @"lib\net45", Some @"**\*.pdb*") ] 
                Dependencies = id
            };
            { 
                Project = "NEventStore.symbols"
                Summary = "SourceLinked pdb files for NEventStore"
                ProjectJson = @".\src\NEventStore\project.json"
                Files = [ (@"..\..\src\NEventStore\bin\Release\NEventStore.pdb*", Some @"build\net45", None);
                          (@"..\..\src\NEventStore\#nupkg\build\*", Some @"build\net45", None) 
                        ] 
                Dependencies = fun dep -> [ ("NEventStore", NuGetVersion) ]
            }
    |]

let testDlls = [ "./src/*.Tests/bin/Release/*.Tests.dll" ]


printfn "Using version: %s" Version

Target "Clean" (fun _ ->
  ensureDirectory buildArtifactPath
  ensureDirectory nugetWorkingPath

  CleanDir buildArtifactPath
  CleanDir nugetWorkingPath
)


Target "RestorePackages" (fun _ -> 
     "./src/NEventStore.sln"
     |> RestoreMSSolutionPackages (fun p ->
         { p with
             OutputPath = nugetPackageRepositoryPath
             Retries = 4 })
)



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
                "Configuration", buildMode
                "TargetFrameworkVersion", "v4.5"
                "Platform", "Any CPU"
                // Note that this important, without this msbuild
                // will try to resolve package references from C:\Users\<user>\.nuget\packages during build
                "NuGetPackagesDirectory", nugetPackageRepositoryPath 
            ]
  }

  build setParams @".\src\NEventStore.sln"
      |> DoNothing

)


Target "UnitTests" (fun _ ->
    testDlls 
        |> Seq.collect (fun glob -> !!glob)
        |> xUnit2 (fun p -> 
            {p with HtmlOutputPath = Some (buildArtifactPath @@ "xunit.html")})
)


Target "CreateNuget" (fun _ ->

  trace "Create nuget packages..."

  nugs
    |> Array.iter (fun nug ->

      let getDeps daNug : NugetDependencies = Common.Nuget3.getProjectDependencies daNug.ProjectJson

      let setParams defaults = {
        defaults with 
          Authors = ["NEventStore Dev Team" ]
          Description = "The purpose of the EventStore is to represent a series of events as a stream. Furthermore, it provides 
          hooks whereby any events committed to the stream can be dispatched to interested parties."
          OutputPath = buildArtifactPath
          Project = nug.Project
          Dependencies = nug.Dependencies (getDeps nug)
          Summary = nug.Summary
          SymbolPackage = NugetSymbolPackage.None
          Version = NuGetVersion
          WorkingDir = nugetWorkingPath
          Files = nug.Files
      } 


//      let setParamsEx (defaults:Common.Nuget3.NuGetParamsEx) = {
//        defaults with 
//          Authors = ["NEventStore Dev Team" ]
//          Description = "The purpose of the EventStore is to represent a series of events as a stream. Furthermore, it provides 
//          hooks whereby any events committed to the stream can be dispatched to interested parties."
//          OutputPath = buildArtifactPath
//          Project = nug.Project
//          Dependencies = nug.Dependencies (getDeps nug)
//          Summary = nug.Summary
//          SymbolPackage = NugetSymbolPackage.None
//          Version = NuGetVersion
//          WorkingDir = nugetWorkingPath
//          Files = nug.Files
//          ContentFiles = []//[(@"..\..\src\NEventStore\bin\Release\NEventStore.pdb", None, None, None, None)]
//      } 
//
//      let nuspecFile = Common.Nuget3.createNuSpecFromTemplateEx setParamsEx (FullName "./template.nuspec") 
//      NuGetPackDirectly setParams nuspecFile
        NuGet setParams (FullName "./template.nuspec")
    )
)

Target "Default" (fun _ ->
  trace "Build starting..."
)



Target "PublishNugetToAppVeyor" (fun _ ->
    
    nugs
    |> Array.iter (fun nug ->

      let setParams defaults = {
        defaults with 
          AccessKey = environVarOrFail "APPVEYOR_NUGET_ACCOUNT_APIKEY"
          Publish = true
          PublishUrl = environVarOrFail "APPVEYOR_NUGET_ACCOUNT_FEED"
          Project = nug.Project
          SymbolPackage = NugetSymbolPackage.None
          Version = NuGetVersion
          WorkingDir = nugetWorkingPath
          OutputPath = buildArtifactPath
      } 
      NuGetPublish setParams 
    )
)


Target "SourceLink" (fun _ ->
    match SourceLink.Pdbstr.tryFind() with
    | Some(_) -> 
        let baseUrl = sprintf "%s/%s/{0}/%%var2%%" gitRaw "NEventStore"
        !! "src/**/*.??proj"
        |> Seq.iter (fun projFile ->
            let proj = VsProj.LoadRelease projFile
            SourceLink.Index proj.CompilesNotLinked proj.OutputFilePdb __SOURCE_DIRECTORY__ baseUrl
        )
    | _ -> traceImportant "SourceLinking: Skipped because pdbstr.exe is not installed (or cannot be found in the canonical paths). 
    The best way is to install using chocolately. See 'build.markdown' for more details."
)

"Clean"
  ==> "RestorePackages"
  ==> "Build"
  ==> "UnitTests"
  =?> ("SourceLink", not isLinux)
  ==> "CreateNuget"
  =?> ("PublishNugetToAppVeyor", appveryorNugetAccountFeed <> "")
  ==> "Default"

RunTargetOrDefault "Default"