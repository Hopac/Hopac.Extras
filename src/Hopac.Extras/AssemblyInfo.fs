namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Hopac.Extras")>]
[<assembly: AssemblyProductAttribute("Hopac.Extras")>]
[<assembly: AssemblyDescriptionAttribute("The Hopac.Extras project contains useful abstractions implemented with Hopac.")>]
[<assembly: AssemblyVersionAttribute("0.1.33")>]
[<assembly: AssemblyFileVersionAttribute("0.1.33")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.1.33"
    let [<Literal>] InformationalVersion = "0.1.33"
