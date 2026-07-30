using System;

namespace MilitaryTrainingApp.Controls
{
    public class GlobalContextEventArgs : EventArgs
    {
        public int PlanId { get; set; }
        public int PlanTargetId { get; set; }
        public int? TimeNodeId { get; set; }
    }
}
