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
                case "STAGE":
                    // Logic vĩ mô (Năm -> Giai đoạn, Giai đoạn -> Tháng)
                    return new YearToStageStrategy();
                case "MONTH":
                    // Logic vi mô chi tiết (Tháng -> Tuần)
                    return new DetailedLeafStrategy();
                case "WEEK":
                    // Tuần -> Ngày (Nếu có mở rộng)
                    return new DetailedLeafStrategy();
                default:
                    return new DetailedLeafStrategy();
            }
        }
    }
}
