using System.Reflection;
using System.Runtime.CompilerServices;

/*!

\namespace Terradue.OpenSearch.DataAnalyzer
@{
    Terradue.Cloud Software Package

    \xrefitem sw_version "Versions" "Software Package Version" 1.0.13

    \xrefitem sw_link "Links" "Software Package List" [DotNetOpenSearchDataAnalyzer](https://github.com/Terradue/DotNetOpenSearchDataAnalyzer)

    \xrefitem sw_license "License" "Software License" [GPLv3](https://github.com/DotNetOpenSearch/Terradue.OpenSearch/blob/master/LICENSE.txt)

    \xrefitem sw_req "Require" "Software Dependencies" \ref Terradue.OpenSearch
    
    \xrefitem sw_req "Require" "Software Dependencies" \ref Terradue.GDAL.Native

    \ingroup OpenSearch
@}

*/

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.
[assembly: AssemblyTitle ("Terradue.OpenSearch.DataAnalyzer")]
[assembly: AssemblyDescription ("Terradue .Net OpenSearch DataAnalyzer Module Library")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("Terradue")]
[assembly: AssemblyProduct ("Terradue.OpenSearch.DataAnalyzer")]
[assembly: AssemblyCopyright ("Terradue")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]
// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.
[assembly: AssemblyVersion ("1.0.18.*")]
[assembly: AssemblyInformationalVersion ("1.0.18")]
// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.
//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config",Watch = true)]