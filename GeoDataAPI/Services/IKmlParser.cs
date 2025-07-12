using GeoDataAPI.Models;
using System.Collections.Generic;

namespace GeoDataAPI.Services
{
    public interface IKmlParser
    {
        List<Field> ParseFields(string fieldsKmlPath, string centroidsKmlPath);
        double CalculateArea(List<Point> points);
        double CalculateDistance(Point point1, Point point2);
        bool IsPointInPolygon(Point point, List<Point> polygon);
    }
}
