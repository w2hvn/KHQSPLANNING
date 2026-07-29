using System;

namespace MilitaryTrainingApp.Services.Scheduling
{
    public static class AllocationEngineFactory
    {
        public static IAllocationStrategy GetStrategy(string timeNodeLevelCode)
        {
            switch (timeNodeLevelCode.ToUpper())
            {
                case "YEAR":
                    return new YearToStageStrategy();
                case "STAGE":
                    return new StageToMonthStrategy();
                case "MONTH":
                    return new MonthToWeekStrategy();
                case "WEEK":
                    return new WeekToDayStrategy();
                default:
                    return new MonthToWeekStrategy();
            }
        }
    }
}
