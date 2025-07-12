using GeoDataAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using GeoDataAPI.Services;

namespace GeoDataAPI.Controllers
{
    [ApiController]
    [Route("api/fields")]
    public class FieldsController : Controller
    {
        private readonly List<Field> _fields;

        public FieldsController(IKmlParser kmlParser)
        {
            // В реальном приложении пути следует брать из конфигурации
            var fieldsKmlPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "fields.kml");
            var centroidsKmlPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "centroids.kml");

            _fields = kmlParser.ParseFields(fieldsKmlPath, centroidsKmlPath);
        }

        // 1. Получение всех полей
        [HttpGet]
        public IActionResult GetAllFields()
        {
            return Ok(_fields);
        }

        // 2. Получение площади поля по ID
        [HttpGet("{id}/area")]
        public IActionResult GetFieldArea(string id)
        {
            var field = _fields.FirstOrDefault(f => f.Id == id);
            if (field == null) return NotFound();

            return Ok(new { id = field.Id, size = field.Size });
        }

        // 3. Получение расстояния от центра поля до точки
        [HttpGet("{id}/distance")]
        public IActionResult GetDistanceFromCenter(string id, [FromQuery] double lat, [FromQuery] double lng)
        {
            var field = _fields.FirstOrDefault(f => f.Id == id);
            if (field == null) return NotFound();

            var center = field.Locations.Center;
            var distance = CalculateDistance(center.Lat, center.Lng, lat, lng);

            return Ok(new { distance });
        }

        // 4. Проверка принадлежности точки к одному из полей
        [HttpGet("contains")]
        public IActionResult GetFieldContainingPoint([FromQuery] double lat, [FromQuery] double lng)
        {
            var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var point = geometryFactory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(lng, lat));

            foreach (var field in _fields)
            {
                var coordinates = field.Locations.Polygon
                    .Select(p => new NetTopologySuite.Geometries.Coordinate(p.Lng, p.Lat))
                    .ToList();

                if (!coordinates[0].Equals(coordinates[coordinates.Count - 1]))
                {
                    coordinates.Add(new NetTopologySuite.Geometries.Coordinate(coordinates[0].X, coordinates[0].Y));
                }

                var polygon = geometryFactory.CreatePolygon(coordinates.ToArray());

                if (polygon.Contains(point))
                {
                    return Ok(new { id = field.Id, name = field.Name });
                }
            }

            return Ok(false);
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var d1 = lat1 * (Math.PI / 180.0);
            var num1 = lon1 * (Math.PI / 180.0);
            var d2 = lat2 * (Math.PI / 180.0);
            var num2 = lon2 * (Math.PI / 180.0) - num1;
            var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) +
                     Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);

            return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
        }
    }
}
