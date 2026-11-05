using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AxisLimitationOnView
{
    public static class GridAlignmentLogic
    {
        /// <summary>
        /// Вычисляет новую кривую оси, выровненную по границам CropBox с отступом.
        /// </summary>
        public static Curve GetAlignedCurve(Curve curve, double offset, XYZ minPt, XYZ maxPt, List<Line> borders)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            XYZ vec = end - start;

            if (vec.GetLength() < 0.0001) return null;
            XYZ direction = vec.Normalize();

            bool isVertical = Math.Abs(direction.X) < 0.001 && Math.Abs(direction.Z) < 0.001;
            bool isHorizontal = Math.Abs(direction.Y) < 0.001 && Math.Abs(direction.Z) < 0.001;

            // Вертикальная ось
            if (isVertical)
            {
                double x = start.X, z = start.Z;
                return Line.CreateBound(new XYZ(x, minPt.Y - offset, z), new XYZ(x, maxPt.Y + offset, z));
            }

            // Горизонтальная ось
            if (isHorizontal)
            {
                double y = start.Y, z = start.Z;
                return Line.CreateBound(new XYZ(minPt.X - offset, y, z), new XYZ(maxPt.X + offset, y, z));
            }

            // Наклонная ось
            XYZ origin = start;
            Line unbound = Line.CreateUnbound(origin, direction);
            List<XYZ> intersections = new List<XYZ>();

            foreach (Line border in borders)
            {
                IntersectionResultArray resultArray;
                SetComparisonResult comp = unbound.Intersect(border, out resultArray);
                if (comp == SetComparisonResult.Overlap && resultArray != null)
                {
                    for (int i = 0; i < resultArray.Size; i++)
                    {
                        XYZ pt = resultArray.get_Item(i).XYZPoint;
                        if (!intersections.Any(p => pt.IsAlmostEqualTo(p, 0.001)))
                            intersections.Add(pt);
                    }
                }
            }

            if (intersections.Count < 2) return null;

            var pairs = intersections
                .Select(pt => new { T = (pt - origin).DotProduct(direction), Pt = pt })
                .OrderBy(p => p.T).ToList();

            XYZ newStart = pairs[0].Pt - direction * offset;
            XYZ newEnd = pairs[pairs.Count - 1].Pt + direction * offset;

            if (newStart.DistanceTo(newEnd) < 0.001) return null;

            return Line.CreateBound(newStart, newEnd);
        }
    }
}