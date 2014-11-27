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

        List<LocalData> locals;

        public static OpenSearchEngine OpenSearchEngine {

            get {
                if (ose == null) {
                    ose = new OpenSearchEngine();
                    ose.LoadPlugins();
                }

                return ose;
            }
        }

        public LocalDataListOpensearchable(List<LocalData> locals) {
            this.locals = locals;
        }



        #region IOpenSearchable implementation

        public QuerySettings GetQuerySettings(Terradue.OpenSearch.Engine.OpenSearchEngine ose) {
            return new QuerySettings(this.DefaultMimeType, AtomOpenSearchEngineExtension.TransformAtomResponseToAtomFeed);
        }

        public Terradue.OpenSearch.Request.OpenSearchRequest Create(string mimetype, System.Collections.Specialized.NameValueCollection parameters) {
            UriBuilder builder = new UriBuilder("http://"+System.Environment.MachineName);
            string[] queryString = Array.ConvertAll(parameters.AllKeys, key => string.Format("{0}={1}", key, parameters[key]));
            builder.Query = string.Join("&", queryString);
//            MemoryOpenSearchRequest request = (MemoryOpenSearchRequest)MemoryOpenSearchRequest.Create(new OpenSearchUrl(builder.ToString()));
            MemoryOpenSearchRequest request = new MemoryOpenSearchRequest(new OpenSearchUrl(builder.ToString()), this.DefaultMimeType);

            System.IO.Stream input = request.MemoryInputStream;

            GenerateSyndicationFeed(input, parameters);

            return request;
        }

        public Terradue.OpenSearch.Schema.OpenSearchDescription GetOpenSearchDescription() {
            OpenSearchDescription osd = new OpenSearchDescription();

            osd.ShortName = " Elastic Catalogue";
            osd.Attribution = "Terradue";
            osd.Contact = "info@terradue.com";
            osd.Developer = "Terradue GeoSpatial Development Team";
            osd.SyndicationRight = "open";
            osd.AdultContent = "false";
            osd.Language = "en-us";
            osd.OutputEncoding = "UTF-8";
            osd.InputEncoding = "UTF-8";
            osd.Description = "This Search Service performs queries in the index {0}. There are several URL templates that return the results in different formats." +
                                            "This search service is in accordance with the OGC 10-032r3 specification.";

            var searchExtensions = ose.Extensions;
            List<OpenSearchDescriptionUrl> urls = new List<OpenSearchDescriptionUrl>();

            NameValueCollection parameters = GetOpenSearchParameters(this.DefaultMimeType);

            UriBuilder searchUrl = new UriBuilder(string.Format("local://gdal/localdata/search"));
            NameValueCollection queryString = HttpUtility.ParseQueryString("?format=format");
            parameters.AllKeys.FirstOrDefault(k => {
                queryString.Add(parameters[k], "{" + k + "?}");
                return false;
            });

            foreach (int code in searchExtensions.Keys) {

                queryString.Set("format", searchExtensions[code].Identifier);
                searchUrl.Query = queryString.ToString();
                urls.Add(new OpenSearchDescriptionUrl(searchExtensions[code].DiscoveryContentType, 
                                                      searchUrl.ToString(),
                                                      "results"));

            }
            searchUrl = new UriBuilder(string.Format("local://gdal/localdata/description"));
            urls.Add(new OpenSearchDescriptionUrl("application/opensearchdescription+xml", 
                                                  searchUrl.ToString(),
                                                  "self"));
            osd.Url = urls.ToArray();

            return osd; 
        }

        public System.Collections.Specialized.NameValueCollection GetOpenSearchParameters(string mimeType) {
            return OpenSearchFactory.GetBaseOpenSearchParameter();
        }

        public void ApplyResultFilters(Terradue.OpenSearch.Request.OpenSearchRequest request, ref Terradue.OpenSearch.Result.IOpenSearchResultCollection osr) {

        }

        public string Identifier {
            get {
                return "localData";
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

        private void GenerateSyndicationFeed(System.IO.Stream stream, NameValueCollection parameters) {
            UriBuilder myUrl = new UriBuilder("http://"+System.Environment.MachineName);
            string[] queryString = Array.ConvertAll(parameters.AllKeys, key => String.Format("{0}={1}", key, parameters[key]));
            myUrl.Query = string.Join("&", queryString);

            AtomFeed feed = new AtomFeed("Discovery feed for WPS result local data",
                                         "This OpenSearch Service allows the discovery of the different items which are part of the "+this.Identifier+" collection" +
                                         "This search service is in accordance with the OGC 10-032r3 specification.",
                                         myUrl.Uri, myUrl.ToString(), DateTimeOffset.UtcNow);

            feed.Generator = "Terradue Web Server";

            List<AtomItem> items = new List<AtomItem>();

            // Load all avaialable Datasets according to the context

            PaginatedList<LocalData> pds = new PaginatedList<LocalData>();

            int startIndex = 0;
            if (parameters["startIndex"] != null) startIndex = int.Parse(parameters["startIndex"]);

            pds.AddRange(locals);

            pds.PageNo = 1;
            if (parameters["startPage"] != null) pds.PageNo = int.Parse(parameters["startPage"]);

            pds.PageSize = 20;
            if (parameters["count"] != null) pds.PageSize = int.Parse(parameters["count"]);

            pds.StartIndex = startIndex;

            if(this.Identifier != null) feed.ElementExtensions.Add("identifier", "http://purl.org/dc/elements/1.1/", this.Identifier);

            foreach (LocalData s in pds.GetCurrentPage()) {
                AtomItem item = (s as IAtomizable).ToAtomItem(parameters);
                if(item != null) items.Add(item);
            }

            feed.Items = items;

            //Atomizable.SerializeToStream ( res, stream.OutputStream );
            var sw = System.Xml.XmlWriter.Create(stream);
            Atom10FeedFormatter atomFormatter = new Atom10FeedFormatter(feed.Feed);
            atomFormatter.WriteTo(sw);
            sw.Flush();
            sw.Close();
        }

        #endregion
    }
}

