using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using log4net;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Engine.Extensions;
using Terradue.OpenSearch.Request;
using Terradue.OpenSearch.Result;
using Terradue.OpenSearch.Schema;

namespace Terradue.OpenSearch.DataAnalyzer
{
    public class LocalCiopRunOpenSearchable : IOpenSearchable
    {

        private static readonly ILog log = LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static OpenSearchEngine ose;

        /// <summary>
        /// List of local data
        /// </summary>
        List<LocalCiopRun> locals;

        /// <summary>
        /// Gets the opensearch engine.
        /// </summary>
        /// <value>The open search engine.</value>
        public static OpenSearchEngine OpenSearchEngine
        {

            get
            {
                if (ose == null)
                {
                    ose = new OpenSearchEngine();
                    ose.LoadPlugins();
                }

                return ose;
            }
        }

        readonly Uri searchBaseUrl;

        public LocalCiopRunOpenSearchable(IEnumerable<LocalCiopRun> locals, Uri searchBaseUrl)
        {
            this.searchBaseUrl = searchBaseUrl;
            this.locals = new List<LocalCiopRun>(locals);
        }



        #region IOpenSearchable implementation

        public QuerySettings GetQuerySettings(Terradue.OpenSearch.Engine.OpenSearchEngine ose)
        {
            return new QuerySettings(this.DefaultMimeType, new AtomOpenSearchEngineExtension().ReadNative);
        }

        public Terradue.OpenSearch.Request.OpenSearchRequest Create(QuerySettings querySettings, System.Collections.Specialized.NameValueCollection parameters)
        {
            UriBuilder builder = new UriBuilder("http://" + System.Environment.MachineName);
            string[] queryString = Array.ConvertAll(parameters.AllKeys, key => string.Format("{0}={1}", key, parameters[key]));
            builder.Query = string.Join("&", queryString);
            AtomOpenSearchRequest request = new AtomOpenSearchRequest(new OpenSearchUrl(builder.ToString()), GenerateSyndicationFeed);

            return request;
        }

        public Terradue.OpenSearch.Schema.OpenSearchDescription GetOpenSearchDescription()
        {
            OpenSearchDescription osd = new OpenSearchDescription();

            osd.ShortName = "Local CIOP run";
            osd.Attribution = "Terradue";
            osd.Contact = "info@terradue.com";
            osd.Developer = "Terradue GeoSpatial Development Team";
            osd.SyndicationRight = "open";
            osd.AdultContent = "false";
            osd.Language = "en-us";
            osd.OutputEncoding = "UTF-8";
            osd.InputEncoding = "UTF-8";
            osd.Description = "This Search Service performs queries in the local CIOP run (WPS ...). There are several URL templates that return the results in different formats. " +
                                            "This search service is in accordance with the OGC 10-032r3 specification.";

            var searchExtensions = OpenSearchEngine.Extensions;
            List<OpenSearchDescriptionUrl> urls = new List<OpenSearchDescriptionUrl>();

            NameValueCollection parameters = GetOpenSearchParameters(this.DefaultMimeType);

            UriBuilder searchUrl = new UriBuilder(searchBaseUrl);
            searchUrl.Path += "/search";
            NameValueCollection queryString = HttpUtility.ParseQueryString("");
            parameters.AllKeys.FirstOrDefault(k =>
            {
                queryString.Add(k, parameters[k]);
                return false;
            });

            foreach (int code in searchExtensions.Keys)
            {

                queryString.Set("format", searchExtensions[code].Identifier);
                string[] queryStrings = Array.ConvertAll(queryString.AllKeys, key => string.Format("{0}={1}", key, queryString[key]));
                searchUrl.Query = string.Join("&", queryStrings);
                urls.Add(new OpenSearchDescriptionUrl(searchExtensions[code].DiscoveryContentType,
                                                      searchUrl.ToString(),
                                                      "results"));

            }
            UriBuilder descriptionUrl = new UriBuilder(searchBaseUrl);
            descriptionUrl.Path += "/description";
            urls.Add(new OpenSearchDescriptionUrl("application/opensearchdescription+xml",
                                                  searchUrl.ToString(),
                                                  "self"));
            osd.Url = urls.ToArray();

            return osd;
        }

        public System.Collections.Specialized.NameValueCollection GetOpenSearchParameters(string mimeType)
        {
            NameValueCollection nvc = OpenSearchFactory.GetBaseOpenSearchParameter();
            nvc.Set("uid", "{geo:uid?}");
            return nvc;
        }

        public string Identifier
        {
            get
            {
                return "localCiopRun";
            }
        }

        public long TotalResults
        {
            get
            {
                return locals.Count;
            }
        }

        public string DefaultMimeType
        {
            get
            {
                return "application/atom+xml";
            }
        }

        private AtomFeed GenerateSyndicationFeed(NameValueCollection parameters)
        {

            AtomFeed feed = new AtomFeed("Discovery feed for local CIOP run (WPS ...)",
                                         "This OpenSearch Service allows the discovery of the different items which are part of the local CIOP results. " +
                                         "This search service is in accordance with the OGC 10-032r3 specification.",
                                         searchBaseUrl, searchBaseUrl.ToString(), DateTimeOffset.UtcNow);



            feed.Generator = "Terradue OpenSearch Data Analyzer";

            List<AtomItem> items = new List<AtomItem>();

            var pds = new Terradue.OpenSearch.Request.PaginatedList<LocalCiopRun>();

            if (!string.IsNullOrEmpty(parameters["q"]))
            {
                string q = parameters["q"];
                locals = locals.Where(p => p.Runid.ToLower().Contains(q.ToLower()) || (p.Wfid.ToLower().Contains(q.ToLower()))).ToList();
            }

            if (!string.IsNullOrEmpty(parameters["uid"]))
                locals = locals.Where(p => p.Runid == parameters["uid"]).ToList();

            pds.StartIndex = 1;
            if (!string.IsNullOrEmpty(parameters["startIndex"])) pds.StartIndex = int.Parse(parameters["startIndex"]);

            pds.AddRange(locals);

            pds.PageNo = 1;
            if (!string.IsNullOrEmpty(parameters["startPage"])) pds.PageNo = int.Parse(parameters["startPage"]);

            pds.PageSize = 20;
            if (!string.IsNullOrEmpty(parameters["count"])) pds.PageSize = int.Parse(parameters["count"]);

            if (this.Identifier != null) feed.ElementExtensions.Add("identifier", "http://purl.org/dc/elements/1.1/", this.Identifier);

            foreach (LocalCiopRun s in pds.GetCurrentPage())
            {
                log.Debug(s.Identifier);
                AtomItem item = (s as IAtomizable).ToAtomItem(parameters);
                if (item != null) items.Add(item);
            }

            feed.Items = items;

            return feed;
        }

        public bool CanCache
        {
            get
            {
                return false;
            }
        }

        public void ApplyResultFilters(OpenSearchRequest request, ref IOpenSearchResultCollection osr, string finalContentType)
        {
        }
        #endregion
    }
}
