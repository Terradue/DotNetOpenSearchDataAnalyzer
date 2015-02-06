using System;
using Terradue.OpenSearch;
using OSGeo.GDAL;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Engine.Extensions;
using System.Collections.Specialized;
using Terradue.OpenSearch.Filters;
using Terradue.OpenSearch.Result;
using Terradue.ServiceModel.Ogc.OwsContext;
using OSGeo.OGR;
using System.Collections.Generic;
using log4net;
using Terradue.GDAL;
using System.IO;
using Terradue.ServiceModel.Syndication;

namespace Terradue.OpenSearch.DataAnalyzer {
    [assembly: log4net.Config.XmlConfigurator(Watch = true)]
    public class LocalData : IAtomizable {

        public static void Configure() {
            GdalConfiguration.ConfigureGdal();
            GdalConfiguration.ConfigureOgr();
        }

        private static readonly ILog log = LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string filepath { get; set; }

        public Dataset dataset { get; set; }

        Uri remoteUri;

        //------------------------------------------------------------------------------------------------------------------------

        public LocalData(string filepath, Uri remoteUri) {

            log.Info("Creating new LocalData: Input=" + filepath);
            this.remoteUri = remoteUri;
            this.filepath = filepath;
            try {
                dataset = OSGeo.GDAL.Gdal.Open(filepath, Access.GA_ReadOnly);
            } catch (Exception e) {
                log.Error(e.Message + " -- " + e.StackTrace);
                dataset = null;
            }
        }

        #region IAtomizable implementation

        public AtomItem ToAtomItem(NameValueCollection parameters) {

            string identifier = this.filepath;

            string name = identifier;

            if (!string.IsNullOrEmpty(parameters["q"])) {
                string q = parameters["q"];
                if (!(name.Contains(q)))
                    return null;
            }

            if (!string.IsNullOrEmpty(parameters["id"]))
            if (identifier != parameters["id"])
                return null;

                
            OwsContextAtomEntry entry = new OwsContextAtomEntry();

            System.IO.FileInfo f = new System.IO.FileInfo(filepath);

            entry.ElementExtensions.Add("identifier", OwcNamespaces.Dc, identifier);
            entry.Title = new Terradue.ServiceModel.Syndication.TextSyndicationContent(f.Name);
            entry.LastUpdatedTime = f.LastWriteTimeUtc;
            entry.PublishDate = f.CreationTimeUtc;
            entry.Links.Add(Terradue.ServiceModel.Syndication.SyndicationLink.CreateMediaEnclosureLink(remoteUri, "application/octet-stream", f.Length));

            string summary = "";

            if (dataset != null) {
                whereType georss = new whereType();

                PolygonType polygon = new PolygonType();
                Geometry geometry = LocalDataFunctions.OSRTransform(dataset);
                if (geometry != null) {
                    string geometryGML = geometry.ExportToGML();
                    Console.WriteLine("Adding geometry : " + geometryGML);
                    polygon.exterior = new AbstractRingPropertyType();
                    polygon.exterior.Item.Item = new DirectPositionListType();
                    polygon.exterior.Item.Item.srsDimension = "2";

                    double minLat = 1000, minLon = 1000, maxLat = -1000, maxLon = -1000;
                    for (int i = 0; i < geometry.GetPointCount(); i++) {
                        double[] p = new double[3];
                        geometry.GetPoint(i, p);
                        minLat = Math.Min(minLat, p[1]);
                        maxLat = Math.Max(maxLat, p[1]);
                        minLon = Math.Min(minLon, p[0]);
                        maxLon = Math.Max(maxLon, p[0]);
                        polygon.exterior.Item.Item.Text += p[1] + " " + p[0] + " ";
                    }

                    georss.Item = polygon;
                    entry.Where = georss;

                    entry.ElementExtensions.Add("box", OwcNamespaces.GeoRss, minLat + " " + minLon + " " + maxLat + " " + maxLon);


                    summary += string.Format("<ul><li><b>Name</b>: {0} </li><li><b>Type</b>: {1} </li> <li> File size: {2} </li> <li> Creation Date: {3} </li> <li> Projection: {4} </li> <li> RasterCount: {5} </li> <li>RasterSize: ({6}) </li> ",
                                             f.Name, dataset.GetDriver().LongName, f.Length, f.CreationTimeUtc.ToString("u"), dataset.GetProjectionRef(), dataset.RasterCount, dataset.RasterXSize + "," + dataset.RasterYSize);

                    string[] metadata = dataset.GetMetadata("");
                    if (metadata.Length > 0) {
                        summary += string.Format("<li>Metadata:<ul>");
                        for (int iMeta = 0; iMeta < metadata.Length; iMeta++) {
                            summary += string.Format("<li>" + iMeta + ":  " + metadata[iMeta]);
                        }
                        summary += string.Format("</ul></li>");
                    }
                    summary += "</ul>";


                

                } else {
                    summary += string.Format("<ul><li><b>Name</b>: {0} </li><li><b>Type</b>: {1} </li> <li> File size: {2} </li> <li> Creation Date: {3} </li> ",
                                         f.Name, f.Extension, f.Length, f.CreationTimeUtc.ToString("u"));

                }
            }


            summary += string.Format("<a href='{0}' class='btn btn-mini btn-info'><i class='icon-download'></i> Download</a>", remoteUri);

            entry.Summary = new TextSyndicationContent(summary);
            
            List<OwcOffering> offerings = new List<OwcOffering>();
            OwcOffering offering = new OwcOffering();
            OwcContent content = new OwcContent();
            content.Url = remoteUri.ToString();

            if (dataset != null) {
                switch (dataset.GetDriver().ShortName) {
                    case "GIF":
                        content.Type = "image/gif";
                        offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/gif";
                        break;
                    case "GTiff":
                        content.Type = "image/tiff";
                        offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/geotiff";
                        break;
                    case "JPEG":
                        content.Type = "image/jpg";
                        offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/jpg";
                        break;
                    case "PNG":
                        content.Type = "image/png";
                        offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/png";
                        break;
                    default:
                        content.Type = "application/octet-stream";
                        offering.Code = null;
                        break;
                }
            } else
                offering.Code = null;



            List<OwcContent> contents = new List<OwcContent>();
            contents.Add(content);
            offering.Contents = contents.ToArray();
            offerings.Add(offering);
            entry.Offerings = offerings;

            return new AtomItem(entry);
        }

        public NameValueCollection GetOpenSearchParameters() {
            return OpenSearchFactory.GetBaseOpenSearchParameter();
        }

        #endregion


    }
}

