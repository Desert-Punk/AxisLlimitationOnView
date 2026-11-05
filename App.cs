using System;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace AxisLimitationOnView
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class App : IExternalApplication
    {
        public static string AssemblyPath = Assembly.GetExecutingAssembly().Location;

        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "ASE";
            try { application.CreateRibbonTab(tabName); } catch { /* Вкладка уже есть */ }

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "ASE_Tools");

            PushButtonData buttonData = new PushButtonData(
                "btnAlignGrids",
                "Обрезать\nоси",
                AssemblyPath,
                "AxisLimitationOnView.AlignGridsCommand"); // Ссылка на команду

            PushButton pushButton = panel.AddItem(buttonData) as PushButton;

            if (pushButton != null)
            {
                pushButton.ToolTip = "Обрезает оси по границам CropBox с заданным отступом.";
                pushButton.LongDescription = "Запускает окно ввода отступа и выравнивает видимые оси на текущем виде.";
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
    }
}