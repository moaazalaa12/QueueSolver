using System;
using System.Collections.Generic;
using System.Text;

namespace QueueSolver.Core.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public double ArrivalTime { get; set; }
        public double ServiceDuration { get; set; }
        public int Priority { get; set; } = 0; // Lower number = Higher priority

        // Computed
        public double? ServiceStartTime { get; set; }
        public double? CompletionTime { get; set; }
        public bool IsRejected { get; set; }

        public double WaitingTime => (ServiceStartTime ?? 0) - ArrivalTime;
        public double SystemTime => (CompletionTime ?? 0) - ArrivalTime;
    }
}
