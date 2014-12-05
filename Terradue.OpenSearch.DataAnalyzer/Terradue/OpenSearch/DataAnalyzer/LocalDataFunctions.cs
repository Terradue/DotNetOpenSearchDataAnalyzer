using System;
using OSGeo.OGR;
using OSGeo.OSR;
using OSGeo.GDAL;
using System.Collections.Generic;

namespace Terradue.OpenSearch.DataAnalyzer {
    public class LocalDataFunctions {

        /// <summary>
        /// Transforms a Dataset in a Geometry using projections
        /// </summary>
        /// <returns>The transform.</returns>
        /// <param name="ds">Ds.</param>
        public static Geometry OSRTransform(Dataset ds){

            double[] adfGeoTransform = new double[6];
            double dfGeoX, dfGeoY;

            List<double[]> dsPoints = new List<double[]>();
            //Upper left
            dsPoints.Add(new double[]{ 0, 0, 0 });
            //Lower left
            dsPoints.Add(new double[]{ 0, ds.RasterYSize, 0 });
            //Upper right
            dsPoints.Add(new double[]{ ds.RasterXSize, 0, 0 });
            //Lower right
            dsPoints.Add(new double[]{ ds.RasterXSize, ds.RasterYSize, 0 });

            string val = "";
            Geometry geometry = new Geometry(wkbGeometryType.wkbLinearRing);

            SpatialReference src = new SpatialReference(ds.GetProjectionRef());
            SpatialReference dst = new SpatialReference("");
            dst.ImportFromProj4("+proj=latlong +datum=WGS84 +no_defs");

            ds.GetGeoTransform(adfGeoTransform);
            ds.GetProjection();

            CoordinateTransformation ct;
            try{
                ct = new CoordinateTransformation(src, dst);
            }catch(Exception e){
                ct = null;
            }
            foreach (double[] p in dsPoints) {
                double x = p[0], y = p[1], z = p[2];
                dfGeoX = adfGeoTransform[0] + adfGeoTransform[1] * x + adfGeoTransform[2] * y;
                dfGeoY = adfGeoTransform[3] + adfGeoTransform[4] * x + adfGeoTransform[5] * y;
                if (ct != null) {
                    ct.TransformPoint(p, dfGeoX, dfGeoY, z);
                    geometry.AddPoint(p[0], p[1], p[2]);
                } else {
                    geometry.AddPoint(dfGeoX, dfGeoY, p[2]);
                }
            }

            geometry.CloseRings();
            return geometry;
        }

    }
}

