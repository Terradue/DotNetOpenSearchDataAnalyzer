using System;
using System.Collections.Specialized;
using System.IO;
using System.Text.RegularExpressions;
using Terradue.OpenSearch.Result;
using Terradue.ServiceModel.Syndication;

namespace Terradue.OpenSearch.DataAnalyzer
{
    public class LocalCiopRun: IAtomizable
    {
        DirectoryInfo directory { get; set; }

        readonly Uri searchBaseUrl;

        string wfid;

        string runid;


        /// <summary>
        /// Initializes a new instance of the <see cref="T:Terradue.OpenSearch.DataAnalyzer.LocalCiopRun"/> class.
        /// </summary>
        /// <param name="directory">Directory local on the current machine</param>
        public LocalCiopRun(DirectoryInfo directory, Uri searchBaseUrl) {
            this.searchBaseUrl = searchBaseUrl;
            this.directory = directory;
            string identifierPattern = @"^(?:\/[\w-]*)*\/(?'wfid'[\w-]*)\/(?'runid'[\w-]*)$";

            Match match = Regex.Match(directory.FullName, identifierPattern);

            this.Wfid = match.Groups["wfid"].Value;
            this.Runid = match.Groups["runid"].Value;
        }

        public string Identifier
        {
            get
            {
                return directory.FullName;
            }
        }

        public string Runid
        {
            get
            {
                return runid;
            }

            set
            {
                runid = value;
            }
        }

        public string Wfid
        {
            get
            {
                return wfid;
            }

            set
            {
                wfid = value;
            }
        }

        #region IAtomizable implementation

        public AtomItem ToAtomItem(NameValueCollection parameters) {



            if (!string.IsNullOrEmpty(parameters["q"])) {
                string q = parameters["q"];
                if (!(Runid.ToLower().Contains(q.ToLower())) && !(Wfid.ToLower().Contains(q.ToLower()))) return null;
            }

            if (!string.IsNullOrEmpty(parameters["uid"]))
            if ( Runid != parameters["uid"] ) return null;

            AtomItem item = new AtomItem(string.Format("{0} run : {1}", Wfid, Runid), "", null, Runid, directory.LastWriteTimeUtc);
            item.PublishDate = directory.CreationTimeUtc;
            item.Identifier = Runid;

            item.Categories.Add(new SyndicationCategory(Wfid, null, "workflow"));

            string runResultDirectory = System.IO.Path.Combine(directory.FullName, "_results/");
            if (System.IO.Directory.Exists(runResultDirectory)) {
                var searchUrl = new UriBuilder(searchBaseUrl);
                searchUrl.Path += string.Format("/{0}/{1}/products/description", Wfid, Runid);
                item.Links.Add(new Terradue.ServiceModel.Syndication.SyndicationLink(searchUrl.Uri, "search", "Search results for run " + Runid, "application/opensearchdescription+xml", 0));
            }

            return item;
        }

        public NameValueCollection GetOpenSearchParameters() {
            NameValueCollection nvc = OpenSearchFactory.GetBaseOpenSearchParameter();
            nvc.Set("uid", "{geo:uid}");
            return nvc;
        }

        #endregion
    }
}
