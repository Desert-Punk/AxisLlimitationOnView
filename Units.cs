using Autodesk.Revit.DB;

namespace MyRevitPlugin.Utils
{
    /// <summary>
    /// Перевод единиц для Revit 2019 (через DisplayUnitType).
    /// </summary>
    public static class Units
    {
        /// <summary>Миллиметры → внутренние единицы Revit (футы).</summary>
        public static double MmToInternal(double mm)
        {
            return UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS);
        }

        /// <summary>Внутренние единицы Revit (футы) → миллиметры.</summary>
        public static double InternalToMm(double internalValue)
        {
            return UnitUtils.ConvertFromInternalUnits(internalValue, DisplayUnitType.DUT_MILLIMETERS);
        }
    }
}