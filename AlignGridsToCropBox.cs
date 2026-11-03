using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyRevitPlugin.Utils;
using Units = MyRevitPlugin.Utils.Units;

namespace MyRevitPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AlignGridsToCropBox : IExternalCommand
    {
        // Отступ от границы CropBox в мм (аналог IN[0] из Dynamo)
        private const double OffsetMm = 200.0;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;

            // Проверка: CropBox должен быть активен, иначе Min/Max не отражают реальную границу
            if (!view.CropBoxActive)
            {
                TaskDialog.Show("Ошибка",
                    "На активном виде не включена область обрезки (CropBox).");
                return Result.Cancelled;
            }

            // Корректный перевод мм → внутренние единицы через UnitUtils
            double offsetFt = Units.MmToInternal(OffsetMm);

            BoundingBoxXYZ cropBox = view.CropBox;
            XYZ minPt = cropBox.Min;
            XYZ maxPt = cropBox.Max;

            // Линии границ CropBox (в координатах вида)
            List<Line> borders = new List<Line>
            {
                Line.CreateBound(new XYZ(minPt.X, minPt.Y, minPt.Z), new XYZ(maxPt.X, minPt.Y, minPt.Z)), // низ
                Line.CreateBound(new XYZ(minPt.X, maxPt.Y, minPt.Z), new XYZ(maxPt.X, maxPt.Y, minPt.Z)), // верх
                Line.CreateBound(new XYZ(minPt.X, minPt.Y, minPt.Z), new XYZ(minPt.X, maxPt.Y, minPt.Z)), // лево
                Line.CreateBound(new XYZ(maxPt.X, minPt.Y, minPt.Z), new XYZ(maxPt.X, maxPt.Y, minPt.Z))  // право
            };

            int processed = 0;
            int skipped = 0;

            using (Transaction t = new Transaction(doc, "Выравнивание осей по CropBox"))
            {
                t.Start();

                FilteredElementCollector collector =
                    new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid));

                foreach (Grid grid in collector.Cast<Grid>())
                {
                    IList<Curve> curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    if (curves == null || curves.Count == 0)
                    {
                        skipped++; // ось не отображается на этом виде
                        continue;
                    }

                    Curve newCurve = GetAlignedCurve(curves[0], offsetFt, minPt, maxPt, borders);
                    if (newCurve == null)
                    {
                        skipped++;
                        continue;
                    }

                    grid.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);
                    processed++;
                }

                t.Commit();
            }

            // Обратный перевод для отображения пользователю
            TaskDialog.Show("Результат", string.Format(
                "Обработано осей: {0}, пропущено: {1}.{2}Отступ: {3} мм.",
                processed, skipped, Environment.NewLine, Units.InternalToMm(offsetFt)));

            return Result.Succeeded;
        }

        /// <summary>
        /// Возвращает новую кривую оси, выровненную по границам CropBox с отступом.
        /// </summary>
        private static Curve GetAlignedCurve(
            Curve curve,
            double offset,
            XYZ minPt,
            XYZ maxPt,
            List<Line> borders)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            XYZ vec = end - start;

            if (vec.GetLength() < 0.0001)
                return null;

            XYZ direction = vec.Normalize();

            bool isVertical = Math.Abs(direction.X) < 0.001
                              && Math.Abs(direction.Z) < 0.001;
            bool isHorizontal = Math.Abs(direction.Y) < 0.001
                                && Math.Abs(direction.Z) < 0.001;

            // Вертикальная ось — выравниваем по Y
            if (isVertical)
            {
                double x = start.X, z = start.Z;
                XYZ ns = new XYZ(x, minPt.Y - offset, z);
                XYZ ne = new XYZ(x, maxPt.Y + offset, z);
                return Line.CreateBound(ns, ne);
            }

            // Горизонтальная ось — выравниваем по X
            if (isHorizontal)
            {
                double y = start.Y, z = start.Z;
                XYZ ns = new XYZ(minPt.X - offset, y, z);
                XYZ ne = new XYZ(maxPt.X + offset, y, z);
                return Line.CreateBound(ns, ne);
            }

            // Наклонная ось — ищем пересечения бесконечной линии с границами CropBox
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

            if (intersections.Count < 2)
                return null; // ось не пересекает CropBox — пропускаем

            // Сортируем точки вдоль направления линии и берём крайние
            var pairs = intersections
                .Select(pt => new { T = (pt - origin).DotProduct(direction), Pt = pt })
                .OrderBy(p => p.T)
                .ToList();

            XYZ firstPt = pairs[0].Pt;
            XYZ lastPt = pairs[pairs.Count - 1].Pt;

            // Смещаем крайние точки вдоль линии на offset
            XYZ newStart = firstPt - direction * offset;
            XYZ newEnd = lastPt + direction * offset;

            if (newStart.DistanceTo(newEnd) < 0.001)
                return null;

            return Line.CreateBound(newStart, newEnd);
        }
    }
}