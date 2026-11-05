using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AxisLimitationOnView
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AlignGridsCommand : IExternalCommand
    {
        private const double DefaultOffsetMm = 200.0;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;

            if (!view.CropBoxActive)
            {
                TaskDialog.Show("Ошибка", "На активном виде не включена область обрезки (CropBox).");
                return Result.Cancelled;
            }

            // 1. Вызов окна (View)
            OffsetDialog dialog = new OffsetDialog(DefaultOffsetMm);
            new WindowInteropHelper(dialog) { Owner = commandData.Application.MainWindowHandle };

            if (dialog.ShowDialog() != true)
                return Result.Cancelled;

            // 2. Подготовка данных (Model)
            double offsetFt = UnitConverter.MmToInternal(dialog.OffsetMm);
            BoundingBoxXYZ cropBox = view.CropBox;
            XYZ minPt = cropBox.Min;
            XYZ maxPt = cropBox.Max;

            List<Line> borders = new List<Line>
            {
                Line.CreateBound(new XYZ(minPt.X, minPt.Y, minPt.Z), new XYZ(maxPt.X, minPt.Y, minPt.Z)),
                Line.CreateBound(new XYZ(minPt.X, maxPt.Y, minPt.Z), new XYZ(maxPt.X, maxPt.Y, minPt.Z)),
                Line.CreateBound(new XYZ(minPt.X, minPt.Y, minPt.Z), new XYZ(minPt.X, maxPt.Y, minPt.Z)),
                Line.CreateBound(new XYZ(maxPt.X, minPt.Y, minPt.Z), new XYZ(maxPt.X, maxPt.Y, minPt.Z))
            };

            int processed = 0, skipped = 0;

            // 3. Выполнение в Revit
            using (Transaction t = new Transaction(doc, "Обрезка осей"))
            {
                t.Start();

                FilteredElementCollector collector = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid));
                foreach (Grid grid in collector.Cast<Grid>())
                {
                    IList<Curve> curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    if (curves == null || curves.Count == 0)
                    {
                        skipped++;
                        continue;
                    }

                    Curve newCurve = GridAlignmentLogic.GetAlignedCurve(curves[0], offsetFt, minPt, maxPt, borders);
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

            TaskDialog.Show("Результат", $"Обработано осей: {processed}, пропущено: {skipped}.\nОтступ: {dialog.OffsetMm} мм.");
            return Result.Succeeded;
        }
    }
}