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

namespace Terradue.OpenSearch.DataAnalyzer {
    [assembly: log4net.Config.XmlConfigurator(Watch = true)]
    public class LocalData : IAtomizable {

        public static void Configure(){
            GdalConfiguration.ConfigureGdal();
            GdalConfiguration.ConfigureOgr();
        }

        private static readonly ILog log = LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string inputFile { get; set; }

        private string inputFilename {
            get {
                if (!inputFile.Contains("/")) return inputFile;
                return inputFile.Substring(inputFile.LastIndexOf("/") + 1);
            }
        }

        private long size { get; set; }

        public Dataset dataset { get; set; }

        string remoteUrl;

        //------------------------------------------------------------------------------------------------------------------------

        public LocalData(string input, string remoteUrl) {

            log.Info("Creating new LocalData: Input=" + input);
            this.remoteUrl = remoteUrl;
            inputFile = input;
            try{
                dataset = OSGeo.GDAL.Gdal.Open( input, Access.GA_ReadOnly );
            }catch(Exception e){
                log.Error(e.Message + " -- " + e.StackTrace);
                Console.WriteLine(e.Message + " -- " + e.StackTrace);
                dataset = null;
            }
            System.IO.FileInfo f = new System.IO.FileInfo(input);
            size = f.Length;
            log.Info("File size = " + size);
            Console.WriteLine("File size = " + size);
        }

        #region IAtomizable implementation

        public AtomItem ToAtomItem(NameValueCollection parameters) {

            Console.WriteLine("ToAtomItem : " + this.inputFilename);

            string identifier = this.inputFilename;

            string name = identifier;
            string description = null;

            if (parameters["q"] != null) {
                string q = parameters["q"];
                if (!(name.Contains(q))) return null;
            }
                
            OwsContextAtomEntry entry = new OwsContextAtomEntry();
            entry.ElementExtensions.Add("identifier", OwcNamespaces.Dc, identifier);
            entry.Title = new Terradue.ServiceModel.Syndication.TextSyndicationContent(identifier);
            entry.LastUpdatedTime = DateTimeOffset.Now;
            entry.PublishDate = DateTimeOffset.Now;
            entry.Links.Add(Terradue.ServiceModel.Syndication.SyndicationLink.CreateMediaEnclosureLink(new Uri(remoteUrl), "application/octet-stream", size));

            if (dataset != null) {
                whereType georss = new whereType();

                PolygonType polygon = new PolygonType();
                Geometry geometry = LocalDataFunctions.OSRTransform(dataset);
                if (geometry != null) {
                    string geometryGML = geometry.ExportToGML();
                    Console.WriteLine("Adding geometry : " + geometryGML);
                    polygon.exterior = new AbstractRingPropertyType();
                    //        System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(LinearRingType), "http://www.opengis.net/gml");
                    //
                    //        using (TextReader reader = new StringReader(geometryGML)) {
                    //            polygon.exterior.Item = (LinearRingType)serializer.Deserialize(reader);
                    //        }
                    polygon.exterior.Item.Item = new DirectPositionListType();
                    polygon.exterior.Item.Item.srsDimension = "2";

                    string box = "";

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
                }
            }
            
            List<OwcOffering> offerings = new List<OwcOffering>();
            OwcOffering offering = new OwcOffering();
            OwcContent content = new OwcContent();
            content.Url = remoteUrl;

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
            } else offering.Code = null;



            List<OwcContent> contents = new List<OwcContent>();
            contents.Add(content);
            offering.Contents = contents.ToArray();
            offerings.Add(offering);
            entry.Offerings = offerings;

            return new AtomItem(entry);
        }

        #endregion
    }
}

