using System;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Filters;
using System.Collections.Generic;
using Terradue.OpenSearch.Engine.Extensions;
using Terradue.OpenSearch.Schema;
using System.Collections.Specialized;
using System.Web;
using System.Linq;
using Terradue.OpenSearch.Result;
using Terradue.Util;
using Terradue.ServiceModel.Syndication;
using Terradue.OpenSearch.Request;

namespace Terradue.OpenSearch.DataAnalyzer {
    public class LocalDataListOpensearchable : IOpenSearchable {

        private static OpenSearchEngine ose;

        /// <summary>
        /// List of local data
        /// </summary>
        List<LocalData> locals;

        /// <summary>
        /// Gets the opensearch engine.
        /// </summary>
        /// <value>The open search engine.</value>
        public static OpenSearchEngine OpenSearchEngine {

            get {
                if (ose == null) {
                    ose = new OpenSearchEngine();
                    ose.LoadPlugins();
                }

                return ose;
            }
        }

        /// <summary>
        /// Gets or sets the name of the workflow.
        /// </summary>
        /// <value>The name of the workflow.</value>
        public string WorkflowName { get; set; }

        /// <summary>
        /// Gets or sets the run identifier.
        /// </summary>
        /// <value>The run identifier.</value>
        public string RunId { get; set; }

        public LocalDataListOpensearchable(List<LocalData> locals, string workflowname, string runid) {
            this.locals = locals;
            this.WorkflowName = workflowname;
            this.RunId = runid;
        }



        #region IOpenSearchable implementation

        public QuerySettings GetQuerySettings(Terradue.OpenSearch.Engine.OpenSearchEngine ose) {
            return new QuerySettings(this.DefaultMimeType, new AtomOpenSearchEngineExtension().ReadNative);
        }

        public Terradue.OpenSearch.Request.OpenSearchRequest Create(string mimetype, System.Collections.Specialized.NameValueCollection parameters) {
            UriBuilder builder = new UriBuilder("http://"+System.Environment.MachineName);
            string[] queryString = Array.ConvertAll(parameters.AllKeys, key => string.Format("{0}={1}", key, parameters[key]));
            builder.Query = string.Join("&", queryString);
            AtomOpenSearchRequest request = new AtomOpenSearchRequest(new OpenSearchUrl(builder.ToString()), GenerateSyndicationFeed);

            return request;
        }

        public Terradue.OpenSearch.Schema.OpenSearchDescription GetOpenSearchDescription() {
            OpenSearchDescription osd = new OpenSearchDescription();

            osd.ShortName = " WPS Local data Catalogue";
            osd.Attribution = "Terradue";
            osd.Contact = "info@terradue.com";
            osd.Developer = "Terradue GeoSpatial Development Team";
            osd.SyndicationRight = "open";
            osd.AdultContent = "false";
            osd.Language = "en-us";
            osd.OutputEncoding = "UTF-8";
            osd.InputEncoding = "UTF-8";
            osd.Description = "This Search Service performs queries in the index {0}. There are several URL templates that return the results in different formats. " +
                "This search service is in accordance with the OGC 10-032r3 specification.";

            var searchExtensions = OpenSearchEngine.Extensions;
            List<OpenSearchDescriptionUrl> urls = new List<OpenSearchDescriptionUrl>();

            NameValueCollection parameters = GetOpenSearchParameters(this.DefaultMimeType);

            UriBuilder searchUrl = new UriBuilder(string.Format("http://" + System.Environment.MachineName + "/sbws/wps/" + this.WorkflowName + "/" + this.RunId + "/results/search"));
            NameValueCollection queryString = HttpUtility.ParseQueryString("?format=format");
            parameters.AllKeys.FirstOrDefault(k => {
                queryString.Add(k, parameters[k]);
                return false;
            });

            foreach (int code in searchExtensions.Keys) {

                queryString.Set("format", searchExtensions[code].Identifier);
                searchUrl.Query = queryString.ToString();
                urls.Add(new OpenSearchDescriptionUrl(searchExtensions[code].DiscoveryContentType, 
                                                      searchUrl.ToString(),
                                                      "results"));

            }
            searchUrl = new UriBuilder(string.Format("http://" + System.Environment.MachineName + "/sbws/wps/" + this.WorkflowName + "/" + this.RunId + "/results/description"));
            urls.Add(new OpenSearchDescriptionUrl("application/opensearchdescription+xml", 
                                                  searchUrl.ToString(),
                                                  "self"));
            osd.Url = urls.ToArray();

            return osd; 
        }

        public System.Collections.Specialized.NameValueCollection GetOpenSearchParameters(string mimeType) {
            NameValueCollection nvc = OpenSearchFactory.GetBaseOpenSearchParameter();
            nvc.Add("id", "{geo:uid?}");
            return nvc;
        }

        public string Identifier {
            get {
                return "localdata";
            }
        }

        public long TotalResults {
            get {
                return locals.Count;
            }
        }

        public string DefaultMimeType {
            get {
                return "application/atom+xml";
            }
        }

        private AtomFeed GenerateSyndicationFeed(NameValueCollection parameters) {
            UriBuilder myUrl = new UriBuilder("http://"+System.Environment.MachineName);
            string[] queryString = Array.ConvertAll(parameters.AllKeys, key => String.Format("{0}={1}", key, parameters[key]));
            myUrl.Query = string.Join("&", queryString);

            AtomFeed feed = new AtomFeed("Discovery feed for WPS result local data",
                                         "This OpenSearch Service allows the discovery of the different items which are part of the "+this.Identifier+" collection. " +
                                         "This search service is in accordance with the OGC 10-032r3 specification.",
                                         myUrl.Uri, myUrl.ToString(), DateTimeOffset.UtcNow);

            feed.Generator = "Terradue Web Server";

            List<AtomItem> items = new List<AtomItem>();

            // Load all avaialable Datasets according to the context

            var pds = new Terradue.OpenSearch.Request.PaginatedList<LocalData>();

            pds.StartIndex = 1;
            if (!string.IsNullOrEmpty(parameters["startIndex"])) pds.StartIndex = int.Parse(parameters["startIndex"]);

            pds.AddRange(locals);

            pds.PageNo = 1;
            if (!string.IsNullOrEmpty(parameters["startPage"])) pds.PageNo = int.Parse(parameters["startPage"]);

            pds.PageSize = 20;
            if (!string.IsNullOrEmpty(parameters["count"])) pds.PageSize = int.Parse(parameters["count"]);

            pds.StartIndex--;
            pds.PageNo--;

            if(this.Identifier != null) feed.ElementExtensions.Add("identifier", "http://purl.org/dc/elements/1.1/", this.Identifier);

            foreach (LocalData s in pds.GetCurrentPage()) {
                AtomItem item = (s as IAtomizable).ToAtomItem(parameters);
                if(item != null) items.Add(item);
            }

            feed.Items = items;

            return feed;
        }

        public void ApplyResultFilters(OpenSearchRequest request, ref IOpenSearchResultCollection osr, string finalContentType) {}

        public bool CanCache {
            get {
                return false;
            }
        }

        #endregion
    }
}

