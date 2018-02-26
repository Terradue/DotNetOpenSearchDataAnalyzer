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
using System.Text.RegularExpressions;
using Kajabity.Tools.Java;
using HtmlAgilityPack;
using System.Web.UI;
using Terradue.GeoJson.GeoRss;
using System.Security.Cryptography;
using System.Text;
using System.Collections.ObjectModel;
using Terradue.ServiceModel.Ogc.Owc.AtomEncoding;

namespace Terradue.OpenSearch.DataAnalyzer
{

    public class LocalProduct : IAtomizable
    {

        public static void Configure()
        {
            GdalConfiguration.ConfigureGdal();
            GdalConfiguration.ConfigureOgr();
        }

        private static readonly ILog log = LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public FileInfo ProductFile { get; private set; }

        Dataset dataset;

        public LocalProduct QuicklookProduct { get; set; }

        public Collection<LocalProduct> AuxiliaryProducts { get; private set;}

        public bool ReplicationInformationOnly { get; set; }

        string wfid;

        string runid;

        string relativeFileName;

        string identifier;

        Uri productRemoteUrl;

        JavaProperties properties;

        XDocument xmlDocument;


        //------------------------------------------------------------------------------------------------------------------------

        public LocalProduct(FileInfo productFile)
        {

            log.Info("Creating new LocalData: Input=" + productFile);
            this.ProductFile = productFile;
            this.AuxiliaryProducts = new Collection<LocalProduct>();
            this.ReplicationInformationOnly = false;
            SetIdentifiers();

        }



        public LocalProduct(FileInfo productFile, Uri remoteUri) : this(productFile)
        {
            this.productRemoteUrl = remoteUri;
        }

        public Dataset Dataset
        {
            get
            {
                if (dataset == null)
                {
                    dataset = GetGdalDataset(ProductFile);
                }
                return dataset;

            }
        }

        public Uri ProductRemoteUrl
        {
            get
            {
                return productRemoteUrl;
            }

        }

        public string Identifier
        {
            get
            {
                return identifier;
            }

            set
            {
                identifier = value;
            }
        }

        private void SetIdentifiers()
        {
            string identifierPattern = @"^(?:\/[\w-]*)*\/(?'wfid'[\w-]*)\/(?'runid'[\w-]*)\/_results\/(?'filename'[\/\w-.]*)$";

            Match match = Regex.Match(ProductFile.FullName, identifierPattern);

            if (!match.Success)
                throw new ImpossibleSearchException("The file do not match the results path");

            this.wfid = match.Groups["wfid"].Value;
            this.runid = match.Groups["runid"].Value;
            this.relativeFileName = match.Groups["filename"].Value;

            this.Identifier = string.Format("{0}/{1}", runid, relativeFileName);

        }

        private static Dataset GetGdalDataset(FileInfo file)
        {
            try
            {
                log.Debug("Before GDAL");
                return OSGeo.GDAL.Gdal.Open(file.FullName, Access.GA_ReadOnly);
            }
            catch (Exception e)
            {
                log.Debug("Error GDAL : " + e.Message + " -- " + e.StackTrace);
                log.Error(e.Message + " -- " + e.StackTrace);
                log.Debug(e.Message + " -- " + e.StackTrace);
            }

            return null;
        }

        private void FindAndProcessProperties()
        {
            FileInfo propertiesProductFile = new FileInfo(ProductFile.FullName + ".properties");

            if (propertiesProductFile.Exists)
            {
                properties = new JavaProperties();
                log.Info("Reading properties for " + ProductFile.FullName);
                using (var stream = propertiesProductFile.OpenRead())
                {
                    this.properties.Load(stream);
                }
            }
        }

        private void FindAndProcessXmlDocument()
        {
            FileInfo xmlProductFile = new FileInfo(ProductFile.FullName + ".xml");

            if (xmlProductFile.Exists)
            {
                log.Info("Reading XML for " + ProductFile.FullName);
                using (var stream = xmlProductFile.OpenRead())
                {
                    this.xmlDocument = XDocument.Load(stream);
                }
            }
        }

        private string GetProductTitle()
        {
            if (this.properties != null && !string.IsNullOrEmpty(properties["title"]))
                return properties["title"];

            return relativeFileName;
        }

        private string GetProductIdentifier()
        {
            if (this.properties != null && !string.IsNullOrEmpty(properties["identifier"]))
                return properties["identifier"];

            return this.Identifier;
        }

        private DateTimeInterval GetProductDate()
        {
            if (this.properties != null && !string.IsNullOrEmpty(properties["date"]))
                return DateTimeInterval.Parse("date");

            return null;
        }

        private GeometryObject GetProductGeometry()
        {
            if (this.properties != null && !string.IsNullOrEmpty(properties["geometry"]))
            {
                try
                {
                    return WktExtensions.WktToGeometry(properties["geometry"]);
                }
                catch (Exception) { }

            }

            //if (Dataset != null)
            //{
            //    var gdalGeom = LocalDataFunctions.OSRTransform(Dataset);
            //    if (gdalGeom != null)
            //    {
            //        string wkt;
            //        if (gdalGeom.ExportToWkt(out wkt) == 0)
            //        {
            //            log.Debug(wkt);
            //            return WktExtensions.WktToGeometry(wkt);
            //        }
            //        log.Debug(wkt);
            //    }
            //}

            return null;
        }

        private KeyValuePair<string, string> GetProductImage()
        {
            if (this.properties != null && !string.IsNullOrEmpty(properties["image_url"]))
            {
                string imageUrl = properties["image_url"];
                if (imageUrl.StartsWith("file://"))
                {
                    log.Debug("Found image url : " + imageUrl);

                    var imageFilePath = imageUrl.Substring(7);//skip 'file://'
                    var imageFile = new System.IO.FileInfo(imageFilePath);

                    //if file does not exist, skip
                    if (!imageFile.Exists)
                    {
                        log.Debug("File does not exists");
                        return new KeyValuePair<string, string>(null, null);
                    }

                    //if file is > 64kb, skip
                    if (imageFile.Length > 64 * 1000)
                    {
                        log.Debug("File is too big");
                        return new KeyValuePair<string, string>(null, null);
                    }

                    try
                    {
                        //if file < 64kb, include it in the summary table in base64 image encoded
                        string format;
                        switch (imageFile.Extension)
                        {
                            case ".jpeg":
                            case ".jpg":
                                format = "image/jpeg";
                                break;
                            case ".png":
                                format = "image/png";
                                break;
                            case ".tiff":
                                format = "image/tiff";
                                break;
                            case ".bmp":
                                format = "image/bmp";
                                break;
                            case ".gif":
                                format = "image/gif";
                                break;
                            case ".ico":
                                format = "image/x-icon";
                                break;
                            default:
                                format = "image/jpeg";
                                break;
                        }

                        // Convert byte[] to Base64 String
                        return new KeyValuePair<string, string>(format, Convert.ToBase64String(File.ReadAllBytes(imageFilePath)));

                    }
                    catch (Exception e)
                    {
                        log.Debug("Error : " + e.Message + " - " + e.StackTrace);
                    }
                }
                else {
                    log.Debug("Found image url : " + imageUrl);
                    return new KeyValuePair<string, string>("uri", imageUrl);
                }
            }
            return new KeyValuePair<string, string>(null, null);
        }

        private string GetHtmlSummary()
        {


            // Initialize StringWriter instance.
            StringWriter stringWriter = new StringWriter();

            // Put HtmlTextWriter in using block because it needs to call Dispose.
            using (HtmlTextWriter writer = new HtmlTextWriter(stringWriter))
            {

                writer.RenderBeginTag(HtmlTextWriterTag.Table);
                writer.RenderBeginTag(HtmlTextWriterTag.Tbody);

                writer.RenderBeginTag(HtmlTextWriterTag.Tr);
                writer.RenderBeginTag(HtmlTextWriterTag.Td);
                writer.AddAttribute(HtmlTextWriterAttribute.Valign, "top");

                writer.RenderBeginTag(HtmlTextWriterTag.Table);
                writer.RenderBeginTag(HtmlTextWriterTag.Tbody);

                writer.RenderBeginTag(HtmlTextWriterTag.Tr);
                writer.RenderBeginTag(HtmlTextWriterTag.Td);
                writer.RenderBeginTag(HtmlTextWriterTag.Strong);
                writer.Write("Identifier");
                writer.RenderEndTag();
                writer.RenderEndTag();
                writer.RenderBeginTag(HtmlTextWriterTag.Td);
                writer.Write(GetProductIdentifier());
                writer.RenderEndTag();
                writer.RenderEndTag();

                var date = GetProductDate();
                if (date != null)
                {
                    writer.RenderBeginTag(HtmlTextWriterTag.Tr);
                    writer.RenderBeginTag(HtmlTextWriterTag.Td);
                    writer.RenderBeginTag(HtmlTextWriterTag.Strong);
                    writer.Write("Time");
                    writer.RenderEndTag();
                    writer.RenderEndTag();
                    writer.RenderBeginTag(HtmlTextWriterTag.Td);
                    writer.Write(date.ToString());
                    writer.RenderEndTag();
                    writer.RenderEndTag();
                }

                if (this.properties != null && properties.Count > 0)
                {
                    foreach (string key in properties)
                    {
                        writer.RenderBeginTag(HtmlTextWriterTag.Tr);
                        writer.RenderBeginTag(HtmlTextWriterTag.Td);
                        writer.RenderBeginTag(HtmlTextWriterTag.Strong);
                        writer.Write(key);
                        writer.RenderEndTag();
                        writer.RenderEndTag();
                        writer.RenderBeginTag(HtmlTextWriterTag.Td);
                        writer.Write(properties[key]);
                        writer.RenderEndTag();
                        writer.RenderEndTag();

                    }
                }

                writer.RenderEndTag();
                writer.RenderEndTag();

                writer.RenderEndTag();
                writer.RenderEndTag();

                writer.RenderBeginTag(HtmlTextWriterTag.Tr);
                writer.RenderBeginTag(HtmlTextWriterTag.Td);
                writer.AddAttribute(HtmlTextWriterAttribute.Valign, "top");

                var imageUrl = GetProductImage();
                if (imageUrl.Key != null)
                {
                    writer.RenderBeginTag(HtmlTextWriterTag.Tr);
                    writer.RenderBeginTag(HtmlTextWriterTag.Td);
                    writer.RenderBeginTag(HtmlTextWriterTag.Img);
                    string src = string.Format("{0}{1}",
                                imageUrl.Key != "uri" ? "" : "data:",
                                               imageUrl.Value);
                    writer.AddAttribute(HtmlTextWriterAttribute.Src, src);
                    writer.RenderEndTag();
                    writer.RenderEndTag();
                }

                writer.RenderEndTag();
                writer.RenderEndTag();

                writer.RenderEndTag();
                writer.RenderEndTag();


            }

            return stringWriter.ToString();

        }

        List<OwcOffering> GetProductOfferings()
        {
            List<OwcOffering> offerings = new List<OwcOffering>();

            offerings.AddRange(GetProductOfferings(this));
            if (QuicklookProduct != null)
            {
                offerings.AddRange(GetProductOfferings(QuicklookProduct));
            }

            return offerings;
        }


        private static List<OwcOffering> GetProductOfferings(LocalProduct product)
        {
            List<OwcOffering> offerings = new List<OwcOffering>();

            if (product.Dataset != null)
            {
                OwcOffering offering = new OwcOffering();

                OwcContent content = new OwcContent();
                content.Url = product.ProductRemoteUrl;

                if (product.Dataset.RasterCount > 0)
                {
                    switch (product.Dataset.GetDriver().ShortName)
                    {
                        case "GIF":
                            content.Type = "image/gif";
                            offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/img";
                            break;
                        case "GTiff":
                            content.Type = "image/tiff";
                            offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/geotiff";
                            break;
                        case "JPEG":
                            content.Type = "image/jpg";
                            offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/img";
                            break;
                        case "PNG":
                            content.Type = "image/png";
                            offering.Code = "http://www.opengis.net/spec/owc-atom/1.0/req/img";
                            break;
                        default:
                            return null;
                    }
                }

                List<OwcContent> contents = new List<OwcContent>();
                contents.Add(content);
                offering.Contents = contents.ToArray();
                offerings.Add(offering);

            }
            return offerings;

        }



        private string ComputeSha1Sum()
        {
            if(ProductFile.Length > 1024*1024*1024) return null;
            using (FileStream fs = ProductFile.OpenRead())
            using (BufferedStream bs = new BufferedStream(fs))
            {
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    byte[] hash = sha1.ComputeHash(bs);
                    StringBuilder formatted = new StringBuilder(2 * hash.Length);
                    foreach (byte b in hash)
                    {
                        formatted.AppendFormat("{0:X2}", b);
                    }
                    return formatted.ToString();
                }
            }
        }


        #region IAtomizable implementation

        public AtomItem ToAtomItem(NameValueCollection parameters)
        {

            if (!string.IsNullOrEmpty(parameters["q"]))
            {
                string q = parameters["q"];
                if (!(Identifier.ToLower().Contains(q.ToLower()))) return null;
            }

            if (!string.IsNullOrEmpty(parameters["uid"]))
                if (Identifier != parameters["uid"]) return null;

            log.DebugFormat("processing {0}", Identifier);

            OwsContextAtomEntry entry = new OwsContextAtomEntry();


            var sha1 = ComputeSha1Sum();
            if(sha1 != null) entry.ElementExtensions.Add("sha1", "http://www.terradue.com", sha1);
            entry.ElementExtensions.Add("relativeFilename", "http://www.terradue.com", relativeFileName);

            if (ReplicationInformationOnly)
            {
                entry.Title = new Terradue.ServiceModel.Syndication.TextSyndicationContent(GetProductTitle());
                entry.LastUpdatedTime = ProductFile.LastWriteTimeUtc;
                entry.PublishDate = ProductFile.CreationTimeUtc;
                entry.ElementExtensions.Add("identifier", OwcNamespaces.Dc, GetProductIdentifier());
                AddEnclosures(entry);

                return new AtomItem(entry);

            }

            FindAndProcessProperties();
            FindAndProcessXmlDocument();


            entry.Title = new Terradue.ServiceModel.Syndication.TextSyndicationContent(GetProductTitle());
            entry.LastUpdatedTime = ProductFile.LastWriteTimeUtc;
            entry.PublishDate = ProductFile.CreationTimeUtc;
            entry.Summary = new ServiceModel.Syndication.TextSyndicationContent(GetHtmlSummary(), ServiceModel.Syndication.TextSyndicationContentKind.Html);

            AddEnclosures(entry);


            entry.ElementExtensions.Add("identifier", OwcNamespaces.Dc, GetProductIdentifier());



            //read xml (from file.xml)
            if (this.xmlDocument != null)
            {
                try
                {
                    entry.ElementExtensions.Add(xmlDocument.Root.CreateReader());
                }
                catch (Exception) { }
            }

            if (Dataset != null && Dataset.RasterCount > 0)
            {

                var box = LocalDataFunctions.GetRasterExtent(Dataset);

                if (box != null)
                {
                    log.Debug("extent found!");
                    entry.ElementExtensions.Add("box", "http://www.georss.org/georss", string.Format("{0} {1} {2} {3}", box.MinY, box.MinX, box.MaxY, box.MaxX));
                }
                else
                    log.Debug("extent is null");

            }



            var geometry = GetProductGeometry();

            if (geometry != null)
            {

                entry.ElementExtensions.Add(geometry.ToGeoRss().CreateReader());

            }

            var offerings = GetProductOfferings();

            log.DebugFormat("{0} offerings", offerings.Count);

            if (offerings != null && offerings.Count > 0)
                entry.Offerings = offerings;

            return new AtomItem(entry);
        }

        public NameValueCollection GetOpenSearchParameters()
        {
            return OpenSearchFactory.GetBaseOpenSearchParameter();
        }

        #endregion


        public void AddEnclosures(OwsContextAtomEntry entry)
        {
            var enclosure = Terradue.ServiceModel.Syndication.SyndicationLink.CreateMediaEnclosureLink(ProductRemoteUrl, "application/octet-stream", ProductFile.Length);
            enclosure.Title = string.Format("{0} file", ProductFile.Extension.TrimStart('.'));
            entry.Links.Add(enclosure);

            if (QuicklookProduct != null)
                QuicklookProduct.AddEnclosures(entry);

            foreach (var auxProduct in AuxiliaryProducts)
            {
                auxProduct.AddEnclosures(entry);
            }
        }
    }
}

