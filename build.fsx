#I @"artifacts/#build_deps/FAKE/4.12.0/tools"
#r @"FakeLib.dll"
#r @"artifacts/#build_deps/FSharp.Data/2.2.5/lib/net40/FSharp.Data.dll"
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
    Dependencies : NugetDependencies
    Files: list<string*string option*string option>
}

let nugs = [| { Project = "NEventStore"
                Summary = "NEventStore is a persistence agnostic event sourcing library for .NET. The primary use is most often associated with CQRS."
                ProjectJson = @".\src\NEventStore\project.json"
                Files = [ (@"..\..\src\NEventStore\bin\Release\NEventStore*", Some @"lib\net45", None);
                        (@"..\..\src\NEventStore\**\*.cs", Some "src", None) ] 
                Dependencies = [ ]
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



let getProjectDependencies (projectJsonFile:string) = 
    let json = JsonValue.Load( new StreamReader(projectJsonFile))
    let deps = json.GetProperty("dependencies")
    

    let(|PkgVersion|_|) x =
        match x with
        | ("version", JsonValue.String version ) -> Some(version)
        | _ -> None

    
    let(|RuntimeDep|_|) x =
        match x with
        | ("type", JsonValue.String "default" ) -> Some(RuntimeDep)
        | _                                     -> None

    let(|BuildDep|_|) x =
        match x with
        | ("type", JsonValue.String "build"   ) -> Some(BuildDep)
        | _                                     -> None

    

    let rec computeRuntimeDependencies packageId jsonValue =
        match jsonValue with
        | JsonValue.String version  -> Some (packageId , version)
        | JsonValue.Record properties    -> 
            let rec scanProperties propertylist packageType packageVersion =
                match propertylist with
                | jsonProperty::remainingProperties -> 
                    match (jsonProperty, packageType, packageVersion) with
                    | ( RuntimeDep         , _          , Some(version) ) -> Some (packageId , version)
                    | ( PkgVersion(version), Some(true) , _             ) -> Some (packageId , version)
                    | ( PkgVersion(version), _          , _             ) -> scanProperties remainingProperties packageType (Some version) // look for "type"
                    | ( RuntimeDep         , _          , None          ) -> scanProperties remainingProperties (Some true) packageVersion // look for "version"
                    | ( BuildDep           , _          , _             ) -> None                                                          // discard this
                    | _                                                   -> scanProperties remainingProperties packageType packageVersion
                | [] -> None
            scanProperties (Array.toList properties) None None
        | _ -> None

    deps
        .Properties() 
        |> Array.toList
        |> List.map(fun (packageId, packageInfo) -> computeRuntimeDependencies packageId packageInfo) 
        |> List.choose id
    
    
    


Target "CreateNuget" (fun _ ->

  trace "Create nuget packages..."

  nugs
    |> Array.iter (fun nug ->

      let getDeps daNug : NugetDependencies = getProjectDependencies daNug.ProjectJson

      let setParams defaults = {
        defaults with 
          Authors = ["NEventStore Dev Team" ]
          Description = "The purpose of the EventStore is to represent a series of events as a stream. Furthermore, it provides hooks whereby any events committed to the stream can be dispatched to interested parties."
          OutputPath = buildArtifactPath
          Project = nug.Project
          Dependencies = (getDeps nug) @ nug.Dependencies
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
  trace "Test ..."
)

Target "PublishNugetToAppVeyor" (fun _ ->
    
    nugs
    |> Array.iter (fun nug ->

      let getDeps daNug : NugetDependencies = getProjectDependencies daNug.ProjectJson
      
      let setParams defaults = {
        defaults with 
          AccessKey = environVarOrFail "APPVEYOR_NUGET_ACCOUNT_APIKEY"
          Publish = true
          PublishUrl = environVarOrFail "APPVEYOR_NUGET_ACCOUNT_FEED"
          Project = nug.Project
          SymbolPackage = NugetSymbolPackage.Nuspec
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
  //==> "DebugTest"
  ==> "RestorePackages"
  ==> "Build"
  ==> "UnitTests"
  =?> ("SourceLink", not isLinux)
  ==> "CreateNuget"
  =?> ("PublishNugetToAppVeyor", not isLocalBuild)
  ==> "Default"

RunTargetOrDefault "Default"