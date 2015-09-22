using System;
using Terradue.OpenSearch.Result;
using System.Collections.Specialized;

namespace Terradue.OpenSearch.DataAnalyzer {
    public class LocalDirectory : IAtomizable {

        private string directory { get; set; }

        public LocalDirectory(string directory) {
            this.directory = directory;
        }

        #region IAtomizable implementation

        public AtomItem ToAtomItem(NameValueCollection parameters) {

            string identifier = this.directory;

            string name = identifier;

            if (!string.IsNullOrEmpty(parameters["q"])) {
                string q = parameters["q"];
                if (!(name.Contains(q))) return null;
            }

            if (!string.IsNullOrEmpty(parameters["id"]))
            if ( identifier != parameters["id"] ) return null;

            AtomItem item = new AtomItem(identifier, "", null, identifier, DateTimeOffset.Now);

            string resultRunHdfsPath = System.IO.Path.Combine(identifier, "_results/");
            if (System.IO.Directory.Exists(resultRunHdfsPath)) {
                string runId = this.directory.Substring(this.directory.LastIndexOf("/") + 1);
                var tmp = this.directory.Substring(0, this.directory.LastIndexOf("/"));
                string workflow = tmp.Substring(tmp.LastIndexOf("/") + 1);
                var searchUrl = new UriBuilder(string.Format("http://" + System.Environment.MachineName + "/sbws/wps/" + workflow + "/" + runId + "/results/search"));
                item.Links.Add(new Terradue.ServiceModel.Syndication.SyndicationLink(searchUrl.Uri, "enclosure", "Results search", "application/atom+xml", 0));
            }

            return item;
        }

        public NameValueCollection GetOpenSearchParameters() {
            return OpenSearchFactory.GetBaseOpenSearchParameter();
        }

        #endregion
    }
}

