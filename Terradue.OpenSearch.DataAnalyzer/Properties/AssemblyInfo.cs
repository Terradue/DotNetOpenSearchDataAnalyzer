/*!

\namespace Terradue.OpenSearch.DataAnalyzer
@{
    Terradue.OpenSearch.DataAnalyzer provides with a data harvester to scan files using GDAL and to extract Geo, Time and other metadata to export them as profiled structured ATOM feed.

    \xrefitem sw_version "Versions" "Software Package Version" 1.0.30

    \xrefitem sw_link "Links" "Software Package List" [DotNetOpenSearchDataAnalyzer](https://github.com/Terradue/DotNetOpenSearchDataAnalyzer)

    \xrefitem sw_license "License" "Software License" [AGPL](https://github.com/DotNetOpenSearch/Terradue.OpenSearch/blob/master/LICENSE)

    \xrefitem sw_req "Require" "Software Dependencies" \ref Terradue.OpenSearch
    
    \xrefitem sw_req "Require" "Software Dependencies" \ref Terradue.GDAL.Native

    \ingroup OpenSearch
@}

*/

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.
using System.Reflection;
using System.Runtime.CompilerServices;
using NuGet4Mono.Extensions;

[assembly: AssemblyTitle ("Terradue.OpenSearch.DataAnalyzer")]
[assembly: AssemblyDescription ("Terradue .Net OpenSearch DataAnalyzer Module Library")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("Terradue")]
[assembly: AssemblyProduct ("Terradue.OpenSearch.DataAnalyzer")]
[assembly: AssemblyCopyright ("Terradue")]
[assembly: AssemblyAuthors("Enguerran Boissier")]
[assembly: AssemblyProjectUrl("https://github.com/Terradue/DotNetOpenSearchDataAnalyzer")]
[assembly: AssemblyLicenseUrl("https://github.com/Terradue/DotNetOpenSearchDataAnalyzer/blob/master/LICENSE")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]
[assembly: AssemblyVersion ("1.0.30.*")]
[assembly: AssemblyInformationalVersion ("1.0.30")]


// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.
//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config",Watch = true)]