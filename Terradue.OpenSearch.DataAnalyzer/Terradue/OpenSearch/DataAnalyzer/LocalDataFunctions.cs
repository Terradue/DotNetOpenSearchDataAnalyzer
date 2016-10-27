using System;
using OSGeo.OGR;
using OSGeo.OSR;
using OSGeo.GDAL;
using System.Collections.Generic;
using log4net;
using Terradue.GeoJson.Geometry;

namespace Terradue.OpenSearch.DataAnalyzer
{
    public class LocalDataFunctions
    {

        private static readonly ILog log = LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Transforms a Dataset in a Geometry using projections
        /// </summary>
        /// <returns>The transform.</returns>
        /// <param name="ds">Ds.</param>
        public static Geometry OSRTransform(Dataset ds)
        {

            log.Debug("Dataset = " + ds.RasterXSize + " - " + ds.RasterYSize);

            double[] adfGeoTransform = new double[6];
            double dfGeoX, dfGeoY;

            List<double[]> dsPoints = new List<double[]>();
            //Upper left
            dsPoints.Add(new double[] { 0, 0, 0 });
            //Lower left
            dsPoints.Add(new double[] { 0, ds.RasterYSize, 0 });
            //Lower right
            dsPoints.Add(new double[] { ds.RasterXSize, ds.RasterYSize, 0 });
            //Upper right
            dsPoints.Add(new double[] { ds.RasterXSize, 0, 0 });

            string val = "";
            Geometry geometry = new Geometry(wkbGeometryType.wkbLinearRing);

            string geometryGML = geometry.ExportToGML();
            log.Debug("Geometry = " + geometryGML + " with projection = " + ds.GetProjectionRef());

            SpatialReference src = new SpatialReference(ds.GetProjectionRef());
            SpatialReference dst = new SpatialReference("");
            dst.ImportFromProj4("+proj=latlong +datum=WGS84 +no_defs");

            ds.GetGeoTransform(adfGeoTransform);
            ds.GetProjection();

            Console.Out.WriteLine(string.Join(",", adfGeoTransform));
            if (adfGeoTransform[0] == 0)
                return null;

            CoordinateTransformation ct;
            try
            {
                ct = new CoordinateTransformation(src, dst);
            }
            catch (Exception e)
            {
                log.Debug("Error GDAL : " + e.Message + " -- " + e.StackTrace);
                ct = null;
            }
            foreach (double[] p in dsPoints)
            {
                double x = p[0], y = p[1], z = p[2];
                dfGeoX = adfGeoTransform[0] + adfGeoTransform[1] * x + adfGeoTransform[2] * y;
                dfGeoY = adfGeoTransform[3] + adfGeoTransform[4] * x + adfGeoTransform[5] * y;
                if (ct != null)
                {
                    ct.TransformPoint(p, dfGeoX, dfGeoY, z);
                    geometry.AddPoint(p[0], p[1], p[2]);
                }
                else {
                    geometry.AddPoint(dfGeoX, dfGeoY, p[2]);
                }
            }

            geometry.CloseRings();
            return geometry;
        }


        public static Envelope GetBaseRasterExtent(Dataset ds)
        {

            if (ds.RasterCount > 0)
            {
                Envelope extent = new Envelope();
                double[] geoTransform = new double[6];
                ds.GetGeoTransform(geoTransform);

                extent.MinX = geoTransform[0];
                extent.MinY = geoTransform[3];
                extent.MaxX = geoTransform[0] + (ds.RasterXSize * geoTransform[1]) + (geoTransform[2] * ds.RasterYSize);
                extent.MaxY = geoTransform[3] + (ds.RasterYSize * geoTransform[5]) + (geoTransform[4] * ds.RasterXSize); ;

                return extent;
            }

            return null;

        }


        /// <summary>
        /// Transforms a Dataset in a Geometry using projections
        /// </summary>
        /// <returns>The transform.</returns>
        /// <param name="ds">Ds.</param>
        public static Envelope GetRasterExtent(Dataset ds, string proj4 = "+proj=latlong +datum=WGS84 +no_defs")
        {

            SpatialReference srcSRS = new SpatialReference(ds.GetProjection());
            Envelope extent;

            if (ds.RasterCount == 0)
                return null;

            extent = GetBaseRasterExtent(ds);



            if (string.IsNullOrEmpty(ds.GetProjection()))
            {
                srcSRS = new SpatialReference("");
                srcSRS.ImportFromProj4(proj4);
            }

            if (srcSRS.__str__().Contains("AUTHORITY[\"EPSG\",\"3857\"]"))
            {
                srcSRS.ImportFromProj4("+proj=merc +a=6378137 +b=6378137 +lat_ts=0.0 +lon_0=0.0 +x_0=0.0 +y_0=0 +k=1.0 +units=m +nadgrids=@null +wktext  +no_defs");
            }

            SpatialReference dstSRS = new SpatialReference("");
            dstSRS.ImportFromProj4(proj4);

            if (dstSRS.IsSame(srcSRS) == 1)
            {
                log.Debug("same projection");
                return extent;
            }

            log.DebugFormat("Reprojecting from {0} to {1}", srcSRS.__str__(), dstSRS.__str__());

            CoordinateTransformation ct;

            try
            {
                ct = new CoordinateTransformation(srcSRS, dstSRS);
            }
            catch (Exception e)
            {
                log.Debug("Error GDAL : " + e.Message + " -- " + e.StackTrace);
                return null;
            }

            double[] newXs = new double[] { extent.MaxX, extent.MinX };
            double[] newYs = new double[] { extent.MaxY, extent.MinY };

            ct.TransformPoints(2, newXs, newYs, new double[] { 0, 0 });

            extent.MaxX = newXs[0];
            extent.MinX = newXs[1];
            extent.MaxY = newYs[0];
            extent.MinY = newYs[1];

            return extent;
        }

        internal static bool AreSameSizeRaster(Dataset dataset1, Dataset dataset2)
        {
            if (dataset1 == null || dataset2 == null)
                return false;

            if (dataset1.RasterCount == 0 || dataset2.RasterCount == 0)
                return false;

            if (dataset1.RasterXSize != dataset2.RasterXSize)
                return false;

            if (dataset1.RasterYSize != dataset2.RasterYSize)
                return false;

            return true;

        }
    }
}

