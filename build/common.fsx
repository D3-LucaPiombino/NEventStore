#I @"../artifacts/#build_deps/FAKE/4.12.0/tools"
#r @"FakeLib.dll"
#r @"../artifacts/#build_deps/FSharp.Data/2.2.5/lib/net40/FSharp.Data.dll"
#r "System.Xml.Linq.dll"

open Fake
open FSharp.Data;
open System.IO;
open System;
open System.Xml.Linq

module Nuget3 =

    type private DependencyType =
    | Default
    | Build
    | Unknown

    let getProjectDependencies (projectJsonFile:string) = 

        let json = JsonValue.Load( new StreamReader(projectJsonFile))
        let deps = json.GetProperty("dependencies")
    
        let(|VersionProperty|_|) x =
            match x with
            | ("version", JsonValue.String version ) -> Some(version)
            | _ -> None
        
        let(|TypeProperty|_|) x =
            match x with
            | ("type", JsonValue.String "build"   ) -> Some(Build)
            | ("type", JsonValue.String "default" ) -> Some(Default)
            | _                                     -> None

        
        let rec computeRuntimeDependencies packageId jsonValue =
            match jsonValue with
            | JsonValue.String version  -> Some (packageId , version)
            | JsonValue.Record properties    -> 
                let rec scanProperties properties packageType packageVersion =
                    match properties with
                    | jsonProperty::tail -> 
                        match (jsonProperty, packageType, packageVersion) with
                        | ( TypeProperty(Default)   , Unknown    , Some(version) ) -> Some (packageId , version)
                        | ( VersionProperty(version), Default    , _             ) -> Some (packageId , version)
                        | ( VersionProperty(version), Unknown    , _             ) -> scanProperties tail packageType (Some version) // look for "type"
                        | ( TypeProperty(Default)   , Unknown    , None          ) -> scanProperties tail Default packageVersion     // look for "version"
                        | ( TypeProperty(Build)     , Unknown    , _             ) -> None                                           // discard this
                        | _                                                        -> scanProperties tail packageType packageVersion
                    | [] -> None
                scanProperties (Array.toList properties) Unknown None
            | _ -> None

        deps
            .Properties() 
            |> Array.toList
            |> List.map(fun (packageId, packageInfo) -> computeRuntimeDependencies packageId packageInfo) 
            |> List.choose id



    

    type ContentFilesBuildAction = 
        | Compile
        | None

    /// Nuget parameter type
    type NuGetParamsEx = 
        { ToolPath : string
          TimeOut : TimeSpan
          Version : string
          Authors : string list
          Project : string
          Title : string
          Summary : string
          Description : string
          Tags : string
          ReleaseNotes : string
          Copyright : string
          WorkingDir : string
          OutputPath : string
          PublishUrl : string
          AccessKey : string
          NoDefaultExcludes : bool
          NoPackageAnalysis : bool
          ProjectFile : string
          Dependencies : NugetDependencies
          DependenciesByFramework : NugetFrameworkDependencies list
          References : NugetReferences
          ReferencesByFramework : NugetFrameworkReferences list
          FrameworkAssemblies : NugetFrameworkAssemblyReferences list
          IncludeReferencedProjects : bool
          PublishTrials : int
          Publish : bool
          SymbolPackage : NugetSymbolPackage
          Properties : list<string * string>
          Files : list<string*string option*string option>
          ContentFiles : list<string*string option*ContentFilesBuildAction option*bool option*bool option>
          }
    
    /// NuGet default parameters  
    let NuGetExDefaults() = 
        { ToolPath = findNuget (currentDirectory @@ "tools" @@ "NuGet")
          TimeOut = TimeSpan.FromMinutes 5.
          Version = 
              if not isLocalBuild then buildVersion
              else "0.1.0.0"
          Authors = []
          Project = ""
          Title = ""
          Summary = null
          ProjectFile = null
          Description = null
          Tags = null
          ReleaseNotes = null
          Copyright = null
          Dependencies = []
          DependenciesByFramework = []
          References = []
          ReferencesByFramework = []
          FrameworkAssemblies = []
          IncludeReferencedProjects = false
          OutputPath = "./NuGet"
          WorkingDir = "./NuGet"
          PublishUrl = null
          AccessKey = null
          NoDefaultExcludes = false
          NoPackageAnalysis = false
          PublishTrials = 5
          Publish = false
          SymbolPackage = NugetSymbolPackage.ProjectFile
          Properties = []
          Files = [] 
          ContentFiles = []}

    let createNuSpecFromTemplateEx setParams (templateNuSpecPath:string) =
        let templateNuSpec = fileInfo templateNuSpecPath
        let parameters = NuGetExDefaults() |> setParams

        let specFile = parameters.WorkingDir @@ (templateNuSpec.Name.Replace("nuspec", "") + parameters.Version + ".nuspec")
                        |> FullName
        tracefn "Creating .nuspec file at %s" specFile

        templateNuSpec.CopyTo(specFile, true) |> ignore

        let getFrameworkGroup (frameworkTags : (string * string) seq) =
            frameworkTags
            |> Seq.map (fun (frameworkVersion, tags) ->
                        if isNullOrEmpty frameworkVersion then sprintf "<group>%s</group>" tags
                        else sprintf "<group targetFramework=\"%s\">%s</group>" frameworkVersion tags)
            |> toLines

        let getGroup items toTags =
            if items = [] then ""
            else sprintf "<group>%s</group>" (items |> toTags)

        let getReferencesTags references = 
            references
            |> Seq.map (fun assembly -> sprintf "<reference file=\"%s\" />" assembly)
            |> toLines
    
        let references = getGroup parameters.References getReferencesTags
    
        let referencesByFramework = 
            parameters.ReferencesByFramework
            |> Seq.map (fun x -> (x.FrameworkVersion, getReferencesTags x.References))
            |> getFrameworkGroup

        let referencesXml = sprintf "<references>%s</references>" (references + referencesByFramework)
    
        let getFrameworkAssemblyTags references =
            references
            |> Seq.map (fun x ->
                        if x.FrameworkVersions = [] then sprintf "<frameworkAssembly assemblyName=\"%s\" />" x.AssemblyName
                        else sprintf "<frameworkAssembly assemblyName=\"%s\" targetFramework=\"%s\" />" x.AssemblyName (x.FrameworkVersions |> separated ", "))
            |> toLines

        let frameworkAssembliesXml =
            if parameters.FrameworkAssemblies = [] then ""
            else sprintf "<frameworkAssemblies>%s</frameworkAssemblies>" (parameters.FrameworkAssemblies |> getFrameworkAssemblyTags)

        let getDependenciesTags dependencies = 
            dependencies
            |> Seq.map (fun (package, version) -> sprintf "<dependency id=\"%s\" version=\"%s\" />" package version)
            |> toLines
    
        let dependencies = getGroup parameters.Dependencies getDependenciesTags
    
        let dependenciesByFramework = 
            parameters.DependenciesByFramework
            |> Seq.map (fun x -> (x.FrameworkVersion, getDependenciesTags x.Dependencies))
            |> getFrameworkGroup
    
        let dependenciesXml = sprintf "<dependencies>%s</dependencies>" (dependencies + dependenciesByFramework)
    
        let filesTags =
            parameters.Files
            |> Seq.map (fun (source, target, exclude) -> 
                let excludeStr = 
                    if exclude.IsSome then sprintf " exclude=\"%s\"" exclude.Value
                    else String.Empty
                let targetStr = 
                    if target.IsSome then sprintf " target=\"%s\"" target.Value
                    else String.Empty

                sprintf "<file src=\"%s\"%s%s />" source targetStr excludeStr)
            |> toLines

        let filesXml = sprintf "<files>%s</files>" filesTags


        let contentFilesTags =
            parameters.ContentFiles
            |> Seq.map (fun (includeGlob, exclude, buildAction, copyToOutput, flatten) -> 
                let excludeStr = 
                    if exclude.IsSome then sprintf " exclude=\"%s\"" exclude.Value
                    else String.Empty

                let buildActionStr = 
                    if buildAction.IsSome then sprintf " buildAction=\"%s\"" (buildAction.Value.ToString())
                    else String.Empty

                let copyToOutputStr = 
                    if copyToOutput.IsSome then sprintf " copyToOutput=\"%b\"" copyToOutput.Value
                    else String.Empty

                let flattenStr = 
                    if copyToOutput.IsSome then sprintf " flatten=\"%b\"" flatten.Value
                    else String.Empty

                sprintf "<files include=\"%s\"%s%s%s%s />" includeGlob excludeStr buildActionStr copyToOutputStr flattenStr)
            |> toLines

        let contentFilesXml = sprintf "<contentFiles>%s</contentFiles>" contentFilesTags
    
        let xmlEncode (notEncodedText : string) = 
            if System.String.IsNullOrWhiteSpace notEncodedText then ""
            else XText(notEncodedText).ToString().Replace("ß","&szlig;")

        let toSingleLine (text:string) =
            if text = null then null 
            else text.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
               
        let replacements = 
            [ "@build.number@", parameters.Version
              "@title@", parameters.Title
              "@authors@", parameters.Authors |> separated ", "
              "@project@", parameters.Project
              "@summary@", parameters.Summary |> toSingleLine
              "@description@", parameters.Description |> toSingleLine
              "@tags@", parameters.Tags
              "@releaseNotes@", parameters.ReleaseNotes
              "@copyright@", parameters.Copyright
            ]
            |> List.map (fun (placeholder, replacement) -> placeholder, xmlEncode replacement)
            |> List.append [ "@dependencies@", dependenciesXml
                             "@references@", referencesXml
                             "@frameworkAssemblies@", frameworkAssembliesXml
                             "@files@", filesXml 
                             "@contentFiles@", contentFilesXml ]
    
        processTemplates replacements [ specFile ]
        tracefn "Created nuspec file %s" specFile
        specFile