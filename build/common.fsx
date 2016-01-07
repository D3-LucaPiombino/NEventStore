#r @"../artifacts/#build_deps/FSharp.Data/2.2.5/lib/net40/FSharp.Data.dll"

open FSharp.Data;
open System.IO;

module Nuget3 =

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