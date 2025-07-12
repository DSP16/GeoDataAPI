using NetTopologySuite.Geometries;
using NetTopologySuite.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System;
using GeoDataAPI.Models;

namespace GeoDataAPI.Services
{
    public class KmlParser : IKmlParser
    {
        private readonly GeometryFactory _geometryFactory;

        public KmlParser()
        {
            _geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        }

        public List<Field> ParseFields(string fieldsKmlPath, string centroidsKmlPath)
        {
            var centroids = ParseCentroids(centroidsKmlPath);
            return ParseFieldPolygons(fieldsKmlPath, centroids);
        }

        private Dictionary<string, Models.Point> ParseCentroids(string kmlPath)
        {
            var doc = XDocument.Load(kmlPath);
            return doc.Descendants("{http://www.opengis.net/kml/2.2}Placemark")
                .ToDictionary(
                    pm => pm.Element("{http://www.opengis.net/kml/2.2}name").Value,
                    pm => ParsePoint(pm.Element("{http://www.opengis.net/kml/2.2}Point"))
                );
        }

        private List<Field> ParseFieldPolygons(string kmlPath, Dictionary<string, Models.Point> centroids)
        {
            var doc = XDocument.Load(kmlPath);
            var fields = new List<Field>();

            foreach (var placemark in doc.Descendants("{http://www.opengis.net/kml/2.2}Placemark"))
            {
                var id = placemark.Element("{http://www.opengis.net/kml/2.2}name").Value;
                var polygonElement = placemark.Element("{http://www.opengis.net/kml/2.2}Polygon");

                if (polygonElement == null || !centroids.ContainsKey(id))
                    continue;

                var coordinates = polygonElement
                    .Descendants("{http://www.opengis.net/kml/2.2}coordinates")
                    .First().Value;

                var polygonPoints = ParseCoordinates(coordinates);
                var area = CalculateArea(polygonPoints);

                fields.Add(new Field
                {
                    Id = id,
                    Name = $"Поле {id}",
                    Size = area,
                    Locations = new FieldLocations
                    {
                        Center = centroids[id],
                        Polygon = polygonPoints
                    }
                });
            }

            return fields;
        }

        private Models.Point ParsePoint(XElement pointElement)
        {
            var coordinates = pointElement.Element("{http://www.opengis.net/kml/2.2}coordinates").Value;
            var parts = coordinates.Split(',');
            return new Models.Point(double.Parse(parts[1]), double.Parse(parts[0]));
        }

        private List<Models.Point> ParseCoordinates(string coordinatesText)
        {
            return coordinatesText.Trim()
                .Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(coord =>
                {
                    var parts = coord.Split(',');
                    return new Models.Point(double.Parse(parts[1]), double.Parse(parts[0]));
                })
                .ToList();
        }

        public double CalculateArea(List<Models.Point> points)
        {
            var coordinates = points
                .Select(p => new Coordinate(p.Lng, p.Lat))
                .ToList();

            if (!coordinates[0].Equals(coordinates[^1]))
                coordinates.Add(coordinates[0]);

            var polygon = _geometryFactory.CreatePolygon(coordinates.ToArray());
            return polygon.Area;
        }

        public double CalculateDistance(Models.Point point1, Models.Point point2)
        {
            var coord1 = new Coordinate(point1.Lng, point1.Lat);
            var coord2 = new Coordinate(point2.Lng, point2.Lat);
            return _geometryFactory.CreatePoint(coord1).Distance(_geometryFactory.CreatePoint(coord2)) * 100000;
        }

        public bool IsPointInPolygon(Models.Point point, List<Models.Point> polygonPoints)
        {
            var pointGeometry = _geometryFactory.CreatePoint(new Coordinate(point.Lng, point.Lat));
            var coordinates = polygonPoints.Select(p => new Coordinate(p.Lng, p.Lat)).ToList();

            if (!coordinates[0].Equals(coordinates[^1]))
                coordinates.Add(coordinates[0]);

            var polygon = _geometryFactory.CreatePolygon(coordinates.ToArray());
            return polygon.Contains(pointGeometry);
        }
    }
}
