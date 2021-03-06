using System;
using Terradue.OpenSearch;
using OSGeo.GDAL;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Engine.Extensions;
using System.Collections.Specialized;
using Terradue.OpenSearch.Filters;
using Terradue.OpenSearch.Result;
using OSGeo.OGR;
using System.Collections.Generic;
using log4net;
using Terradue.GDAL;
using System.Xml.Linq;
using Terradue.GeoJson.Geometry;
using System.Drawing.Imaging;
using System.IO;
using Terradue.ServiceModel.Ogc.Owc.AtomEncoding;
using Terradue.ServiceModel.Ogc.Gml311;
using Terradue.GeoJson.GeoRss;

namespace Terradue.OpenSearch.DataAnalyzer {
    
    public class LocalData : IAtomizable {

        public static void Configure(){
            GdalConfiguration.ConfigureGdal();
            GdalConfiguration.ConfigureOgr();
        }

        private static readonly ILog log = LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string inputFile { get; set; }

        private long size { get; set; }

        public Dataset dataset { get; set; }

        public List<KeyValuePair<string,string>> properties { get; set; }

        public XElement xml { get; set; }

        Uri descriptionUri;
        Uri remoteUri;

        //------------------------------------------------------------------------------------------------------------------------

        public LocalData(string input) {

            log.Info("Creating new LocalData: Input=" + input);
            inputFile = input;
            try{
                log.Debug ("Before GDAL");
                dataset = OSGeo.GDAL.Gdal.Open( input, Access.GA_ReadOnly );
                log.Debug ("After GDAL");
            }catch(Exception e){
                log.Debug ("Error GDAL : " + e.Message + " -- " + e.StackTrace);
                log.Error(e.Message + " -- " + e.StackTrace);
                log.Debug(e.Message + " -- " + e.StackTrace);
                dataset = null;
            }
            System.IO.FileInfo f = new System.IO.FileInfo(input);
            size = f.Length;
            log.Debug("File size = " + size);
        }

        public LocalData(string input, Uri remoteUri) : this(input) {
            this.remoteUri = remoteUri;
        }

        public LocalData(string input, Uri remoteUri, Uri descriptionUri) : this(input, remoteUri) {
            this.descriptionUri = descriptionUri;
        }

        #region IAtomizable implementation

        public AtomItem ToAtomItem(NameValueCollection parameters) {

            string identifier = this.inputFile;
            string separator = "_results/";
            if (identifier!= null && identifier.Contains(separator)) identifier = identifier.Substring(identifier.LastIndexOf(separator) + separator.Length);

            string name = identifier;

            if (!string.IsNullOrEmpty(parameters["q"])) {
                string q = parameters["q"];
                if (!(name.Contains(q))) return null;
            }

            if (!string.IsNullOrEmpty(parameters["id"]))
                if (identifier != parameters["id"]) return null;
                            
            OwsContextAtomEntry entry = new OwsContextAtomEntry();

            if (this.descriptionUri != null)
                entry.ElementExtensions.Add("parentIdentifier", OwcNamespaces.Dc, this.descriptionUri);

            entry.Title = new Terradue.ServiceModel.Syndication.TextSyndicationContent(identifier);
            entry.LastUpdatedTime = DateTimeOffset.Now;
            entry.PublishDate = DateTimeOffset.Now;
            entry.Links.Add(Terradue.ServiceModel.Syndication.SyndicationLink.CreateMediaEnclosureLink(remoteUri, "application/octet-stream", size));

            Geometry geometry = null;

            //read properties (from file.properties)
            if (this.properties != null) {
                string propertiesTable = "<table>";
                foreach (var kv in properties) {
                    try{
                        switch (kv.Key) {
                            case "identifier":
                                identifier = kv.Value;
                                propertiesTable += "<tr><td>" + kv.Key + "</td><td>" + kv.Value + "</td></tr>";
                                break;
                            case "date":
                                var date = new DateTimeInterval();
                                if (kv.Value.Contains("/")) {
                                    date.StartDate = DateTime.Parse(kv.Value.Split("/".ToCharArray())[0]);
                                    date.EndDate = DateTime.Parse(kv.Value.Split("/".ToCharArray())[1]);
                                } else {
                                    date.StartDate = DateTime.Parse(kv.Value);
                                    date.EndDate = DateTime.Parse(kv.Value);
                                }
                                entry.Date = date;
                                propertiesTable += "<tr><td>" + kv.Key + "</td><td>" + kv.Value + "</td></tr>";
                                break;
                            case "title":
                                entry.Title = new Terradue.ServiceModel.Syndication.TextSyndicationContent(kv.Value);
                                propertiesTable += "<tr><td>" + kv.Key + "</td><td>" + kv.Value + "</td></tr>";
                                break;
                            case "geometry":
                                entry.ElementExtensions.Add("spatial","http://purl.org/dc/terms/",kv.Value);
                                geometry = Geometry.CreateFromWkt (kv.Value);
                                geometry = geometry.GetBoundary ();
                                geometry.CloseRings ();
								double minLat = 1000, minLon = 1000, maxLat = -1000, maxLon = -1000;
								log.Debug("found " + geometry.GetPointCount() + " points");
								for (int i = 0; i < geometry.GetPointCount(); i++){
									double[] p = new double[3];
									geometry.GetPoint(i, p);
									log.Debug("p[0] = " + p[0] + " ; p[1] = " + p[1]);
									minLat = Math.Min(minLat, p[1]);
									maxLat = Math.Max(maxLat, p[1]);
									minLon = Math.Min(minLon, p[0]);
									maxLon = Math.Max(maxLon, p[0]);
								}
								entry.ElementExtensions.Add("box", OwcNamespaces.GeoRss, minLat + " " + minLon + " " + maxLat + " " + maxLon);
                                propertiesTable += "<tr><td>" + kv.Key + "</td><td>" + kv.Value + "</td></tr>";
                                break;
                            case "image_url":
                                var file = kv.Value;
                                if(file.StartsWith("file://")){
                                    log.Debug("Found image url : " + kv.Value);

                                    var filePath = file.Substring(7);//skip 'file://'
                                    var f = new System.IO.FileInfo(filePath);

                                    //if file does not exist, skip
                                    if(!f.Exists){
                                        log.Debug("File does not exists");
                                        break;
                                    }

                                    //if file is > 64kb, skip
                                    if(f.Length > 64 * 1000){
                                        log.Debug("File is too big");
                                        break;
                                    }

                                    try{
                                        //if file < 64kb, include it in the summary table in base64 image encoded
                                        string htmlformat;
                                        switch(filePath.Substring(filePath.LastIndexOf(".") + 1).ToLower()){
                                            case "jpeg":
                                            case "jpg":
                                                htmlformat = "image/jpeg";
                                                break;
                                            case "png":
                                                htmlformat = "image/png";
                                                break;
                                            case "tiff":
                                                htmlformat = "image/tiff";
                                                break;
                                            case "bmp":
                                                htmlformat = "image/bmp";
                                                break;
                                            case "gif":
                                                htmlformat = "image/gif";
                                                break;
                                            case "ico":
                                                htmlformat = "image/x-icon";
                                                break;
                                            default:
                                                htmlformat = "image/jpeg";
                                                break;
                                        }

                                        // Convert byte[] to Base64 String
                                        string base64String = Convert.ToBase64String(File.ReadAllBytes(filePath));
                                        propertiesTable += "<tr><td></td><td><img src='data:"+ htmlformat +";base64," + base64String + "'></td></tr>";

                                    }catch(Exception e){
                                        log.Debug("Error : " + e.Message + " - " + e.StackTrace);
                                    }
                                } else {
                                    log.Debug("Found image url : " + kv.Value);
                                    //test it is a valid url
                                    var uri = new UriBuilder(kv.Value);
                                    propertiesTable += "<tr><td></td><td><img src='" + kv.Value + "'></img></td></tr>";
                                }
                                break;
                            default:
                                propertiesTable += "<tr><td>" + kv.Key + "</td><td>" + kv.Value + "</td></tr>";
                                break;
                        }
                    }catch(Exception e){
                    }
                }
                propertiesTable += "</table>";
                entry.Summary = new Terradue.ServiceModel.Syndication.TextSyndicationContent(propertiesTable);
            }

            entry.ElementExtensions.Add("identifier", OwcNamespaces.Dc, identifier);

            //read xml (from file.xml)
            if (this.xml != null) {
                try {
                    entry.ElementExtensions.Add(xml.CreateReader());
                } catch (Exception) {}
            }

            if (dataset != null && geometry == null) {
				var box = LocalDataFunctions.GetRasterExtent(dataset);

				if (box != null)
				{
					log.Debug("extent found!");
					entry.ElementExtensions.Add("box", "http://www.georss.org/georss", string.Format("{0} {1} {2} {3}", box.MinY, box.MinX, box.MaxY, box.MaxX));
				}
				else
					log.Debug("extent is null");
            } else log.Debug ("Geometry is null");
            
            List<OwcOffering> offerings = new List<OwcOffering>();
            OwcOffering offering = new OwcOffering();


            if (dataset != null) {
                OwcContent content = new OwcContent();
                if (remoteUri != null)
                    content.Url = remoteUri;
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
                }
                List<OwcContent> contents = new List<OwcContent>();
                contents.Add(content);
                offering.Contents = contents.ToArray();
            } 



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

