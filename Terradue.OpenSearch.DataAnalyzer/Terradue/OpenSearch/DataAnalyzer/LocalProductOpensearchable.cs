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
using log4net;

namespace Terradue.OpenSearch.DataAnalyzer
{
    public class LocalProductOpensearchable : IOpenSearchable
    {

        private static readonly ILog log = LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private OpenSearchEngine ose;

        /// <summary>
        /// List of local data
        /// </summary>
        List<LocalProduct> products;

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

        readonly Uri searchBaseUrl;

        public LocalProductOpensearchable(List<LocalProduct> locals, string workflowname, string runid, OpenSearchEngine ose, Uri searchBaseUrl)
        {
            this.searchBaseUrl = searchBaseUrl;
            this.products = locals;
            this.WorkflowName = workflowname;
            this.RunId = runid;
            this.ose = ose;
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

            osd.ShortName = "CIOP products";
            osd.Attribution = "Terradue";
            osd.Contact = "info@terradue.com";
            osd.Developer = "Terradue GeoSpatial Development Team";
            osd.SyndicationRight = "open";
            osd.AdultContent = "false";
            osd.Language = "en-us";
            osd.OutputEncoding = "UTF-8";
            osd.InputEncoding = "UTF-8";
            osd.Description = string.Format("This Search Service performs queries in the products in the CIOP run {0}. There are several URL templates that return the results in different formats. " +
                                            "This search service is in accordance with the OGC 10-032r3 specification.", RunId);

            var searchExtensions = ose.Extensions;
            List<OpenSearchDescriptionUrl> urls = new List<OpenSearchDescriptionUrl>();

            NameValueCollection parameters = GetOpenSearchParameters(this.DefaultMimeType);

            UriBuilder searchUrl = new UriBuilder(string.Format("{0}/{1}/{2}/products/search", searchBaseUrl, this.WorkflowName, this.RunId));
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
            UriBuilder descriptionhUrl = new UriBuilder(string.Format("{0}/{1}/{2}/products/description", searchBaseUrl, this.WorkflowName, this.RunId));
            urls.Add(new OpenSearchDescriptionUrl("application/opensearchdescription+xml",
                                                  descriptionhUrl.ToString(),
                                                  "self"));
            osd.Url = urls.ToArray();

            return osd;
        }

        public System.Collections.Specialized.NameValueCollection GetOpenSearchParameters(string mimeType)
        {
            NameValueCollection nvc = OpenSearchFactory.GetBaseOpenSearchParameter();
            nvc.Add("uid", "{geo:uid?}");
            nvc.Add("view", "{t2:view?}");
            return nvc;
        }

        public string Identifier
        {
            get
            {
                return "local Ciop products";
            }
        }

        public long TotalResults
        {
            get
            {
                return products.Count;
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

            AtomFeed feed = new AtomFeed("Discovery feed for WPS result local data",
                                         "This OpenSearch Service allows the discovery of the different products out of the Ciop run " + RunId + " collection. " +
                                         "This search service is in accordance with the OGC 10-032r3 specification.",
                                         searchBaseUrl, searchBaseUrl.ToString(), DateTimeOffset.UtcNow);

            feed.Generator = "Terradue Web Server";

            List<AtomItem> items = new List<AtomItem>();

            // Load all avaialable Datasets according to the context

            var pds = new Terradue.OpenSearch.Request.PaginatedList<LocalProduct>();

            if (string.IsNullOrEmpty(parameters["view"]))
            {
                products = SimplifyProductList(products);
            }

            if (parameters["view"] == "replica")
            {
                products.ForEach(p => p.ReplicationInformationOnly = true);
            }


            if (!string.IsNullOrEmpty(parameters["q"]))
            {
                string q = parameters["q"];
                products = products.Where(p => p.Identifier.ToLower().Contains(q.ToLower())).ToList();
            }

            if (!string.IsNullOrEmpty(parameters["uid"]))
                products = products.Where(p => p.Identifier == parameters["uid"]).ToList();

            pds.StartIndex = 1;
            if (!string.IsNullOrEmpty(parameters["startIndex"])) pds.StartIndex = int.Parse(parameters["startIndex"]);

            pds.AddRange(products);

            pds.PageNo = 1;
            if (!string.IsNullOrEmpty(parameters["startPage"])) pds.PageNo = int.Parse(parameters["startPage"]);

            pds.PageSize = 20;
            if (!string.IsNullOrEmpty(parameters["count"])) pds.PageSize = int.Parse(parameters["count"]);

            if (this.Identifier != null) feed.ElementExtensions.Add("identifier", "http://purl.org/dc/elements/1.1/", this.Identifier);

            foreach (LocalProduct s in pds.GetCurrentPage())
            {
                AtomItem item = (s as IAtomizable).ToAtomItem(parameters);
                if (item != null) items.Add(item);
            }

            feed.Items = items;

            feed.TotalResults = pds.Count;

            return feed;
        }

        public void ApplyResultFilters(OpenSearchRequest request, ref IOpenSearchResultCollection osr, string finalContentType) { }

        public bool CanCache
        {
            get
            {
                return true;
            }
        }

        #endregion

        List<LocalProduct> SimplifyProductList(List<LocalProduct> products)
        {

            List<LocalProduct> newLocalProducts = new List<LocalProduct>();


            var groups = products.GroupBy(product =>
            {
                log.DebugFormat("Simplifying {0} : {1}", product.ProductFile.Name, product.ProductFile.Name.LastIndexOf(product.ProductFile.Extension));
                if (string.IsNullOrEmpty(product.ProductFile.Extension))
                    return product.ProductFile.Name;


                return product.ProductFile.Name.Substring(0, product.ProductFile.Name.LastIndexOf(product.ProductFile.Extension));
            });

            foreach (var group in groups)
            {

                if (group.Count() == 1)
                {
                    newLocalProducts.Add(group.First());
                    continue;
                }

                log.Debug(group.First().ProductFile.Extension);

                if (group.Any(p => p.ProductFile.Extension == ".pngw"))
                {
                    if (group.Any(p => p.ProductFile.Extension == ".png"))
                    {
                        if (group.Count() == 2)
                        {
                            var png = group.First(p => p.ProductFile.Extension == ".png");
                            png.AuxiliaryProducts.Add(group.First(p => p.ProductFile.Extension == ".pngw"));
                            newLocalProducts.Add(png);
                            continue;
                        }
                        foreach (var otherProduct in group.Where(p => p.ProductFile.Extension != ".pngw" && p.ProductFile.Extension != ".png"))
                        {
                            otherProduct.QuicklookProduct = group.First(p => p.ProductFile.Extension == ".png");
                            otherProduct.AuxiliaryProducts.Add(group.First(p => p.ProductFile.Extension == ".pngw"));
                            newLocalProducts.Add(otherProduct);
                        }
                        continue;
                    }

                }

                if (group.Any(p => p.ProductFile.Extension == ".png"))
                {
                    var png = group.First(p => p.ProductFile.Extension == ".png");
                    bool isquicklook = false;
                    foreach (var otherProduct in group.Where(p => p.ProductFile.Extension != ".png"))
                    {
                        if (LocalDataFunctions.AreSameSizeRaster(png.Dataset, otherProduct.Dataset))
                        {
                            otherProduct.QuicklookProduct = png;
                            isquicklook = true;
                        }
                        newLocalProducts.Add(otherProduct);
                    }
                    if (!isquicklook)
                        newLocalProducts.Add(png);
                    
                    continue;
                }

                if (group.Any(p => p.ProductFile.Extension == ".shp"))
                {
                    var shp = group.First(p => p.ProductFile.Extension == ".shp");
                    foreach (var otherProduct in group.Where(p => p.ProductFile.Extension != ".shp"))
                    {
                        shp.AuxiliaryProducts.Add(otherProduct);
                    }
                    newLocalProducts.Add(shp);
                    continue;
                }

                newLocalProducts.AddRange(group);
            }

            return newLocalProducts;

        }
    }
}

