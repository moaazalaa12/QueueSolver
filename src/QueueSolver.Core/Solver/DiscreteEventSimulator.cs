using QueueSolver.Core.Models;


namespace QueueSolver.Core.Solver
{
    public class EventComparer : IComparer<SimulationEvent>
    {
        public int Compare(SimulationEvent? x, SimulationEvent? y)
        {
            if (x == null || y == null) return 0;
            int timeComparison = x.Time.CompareTo(y.Time);
            if (timeComparison != 0) return timeComparison;

            if (x.Type == EventType.Departure && y.Type == EventType.Arrival) return -1;
            if (x.Type == EventType.Arrival && y.Type == EventType.Departure) return 1;

            return 0;
        }
    }
    public record SimulationEvent(EventType Type, double Time, Customer Customer);

    public class DiscreteEventSimulator
    {
        private readonly Random _rng = new Random();
        private const double Epsilon = 1e-9;

        public SimulationStats Run(SimulationInput input, Action<SimulationSnapshot>? onStep = null)
        {
            var customers = new List<Customer>();
            var eventQueue = new PriorityQueue<SimulationEvent, SimulationEvent>(new EventComparer());
            var stats = new SimulationStats { IsAnalytical = false };

            // State variables
            double currentTime = 0;
            int busyServers = 0;
            int queueCount = 0;
            int systemCount = input.InitialCustomers;

            var waitingQueue = new List<Customer>();

            if (input.InitialCustomers > 0)
            {
                for (int i = 1; i <= input.InitialCustomers; i++)
                {
                    var svc = GenerateService(input);
                    var c = new Customer { Id = i, ArrivalTime = 0, ServiceDuration = svc };
                    customers.Add(c);

                    if (i <= input.Servers)
                    {
                        c.ServiceStartTime = 0;
                        c.CompletionTime = svc;
                        var depEvt = new SimulationEvent(EventType.Departure, svc, c);
                        eventQueue.Enqueue(depEvt, depEvt);
                        busyServers++;
                    }
                    else
                    {
                        waitingQueue.Add(c);
                        queueCount++;
                    }
                }
            }

            double t = 0;
            for (int i = input.InitialCustomers + 1; i <= input.N + input.InitialCustomers; i++)
            {
                t += GenerateInterarrival(input);
                var svc = GenerateService(input);
                var c = new Customer { Id = i, ArrivalTime = t, ServiceDuration = svc };
                customers.Add(c);

                var evt = new SimulationEvent(EventType.Arrival, t, c);
                eventQueue.Enqueue(evt, evt);
            }

            // Initialize History & Area
            RecordHistory(stats, 0, systemCount, queueCount);
            double areaL = 0;
            double areaLq = 0;
            double lastEventTime = 0;

            // --- 3. Simulation Loop ---
            while (eventQueue.Count > 0)
            {
                var evt = eventQueue.Dequeue();
                double previousTime = currentTime;
                currentTime = evt.Time;

                // حساب المساحات (Time Weighted Averages)
                double duration = currentTime - previousTime;
                areaL += systemCount * duration;
                areaLq += queueCount * duration;
                lastEventTime = currentTime;

                // تسجيل الحالة القديمة للرسم (لإنشاء Step Chart)
                RecordHistory(stats, previousTime, systemCount, queueCount);

                // --- معالجة الحدث ---
                if (evt.Type == EventType.Arrival)
                {
                    HandleArrival(input, evt.Customer, ref systemCount, ref busyServers, ref queueCount, waitingQueue, eventQueue, currentTime, stats);
                }
                else // Departure
                {
                    HandleDeparture(ref systemCount, ref busyServers, ref queueCount, waitingQueue, eventQueue, currentTime, input);
                }

                // تسجيل الحالة الجديدة بعد التغيير
                RecordHistory(stats, currentTime, systemCount, queueCount);
            }

            // --- 4. Finalize Calculation & Ti Logic ---
            double rho = input.Lambda / (input.Servers * input.Mu);

            // **منطق Ti المُصحح بناءً على طلبك:**
            if (rho > 1.0) // Overloaded Case (μ < λ) - Ti هو أول وقت النظام يصبح فيه ممتلئ
            {
                // Ti تم تسجيله بالفعل كـ FirstBusyTime في HandleArrival
            }
            else
            {
                var lastInitialCustomer = customers.Take(input.InitialCustomers).OrderByDescending(c => c.CompletionTime).FirstOrDefault();
                if (lastInitialCustomer?.CompletionTime > 0)
                {
                    stats.FirstBusyTime = lastInitialCustomer.CompletionTime;
                }
            }

            stats.Results = customers;
            return stats;
        }

        

        private void HandleArrival(SimulationInput input, Customer c, ref int sysCount, ref int busy, ref int qCount, List<Customer> q, PriorityQueue<SimulationEvent, SimulationEvent> eq, double now, SimulationStats stats)
        {
            if (stats.FirstBusyTime == null && busy >= input.Servers)
            {
 
                stats.FirstBusyTime = now;
            }

            // 2. Capacity Check
            if (input.Capacity != int.MaxValue && sysCount >= input.Capacity)
            {
                c.IsRejected = true;
                c.CompletionTime = now;
                return;
            }

            sysCount++;

            if (busy < input.Servers)
            {
                busy++;
                c.ServiceStartTime = now;
                double finish = now + c.ServiceDuration;
                c.CompletionTime = finish;

                var depEvt = new SimulationEvent(EventType.Departure, finish, c);
                eq.Enqueue(depEvt, depEvt);
            }
            else
            {
                qCount++;
                q.Add(c);
            }
        }

        private void HandleDeparture(ref int sysCount, ref int busy, ref int qCount, List<Customer> q, PriorityQueue<SimulationEvent, SimulationEvent> eq, double now, SimulationInput input)
        {
            sysCount--;

            if (q.Count > 0)
            {
                qCount--;

                Customer next = (input.Discipline == QueueDiscipline.LIFO) ? q.Last() : q.First();
                q.Remove(next);

                next.ServiceStartTime = now;
                double finish = now + next.ServiceDuration;
                next.CompletionTime = finish;

                var depEvt = new SimulationEvent(EventType.Departure, finish, next);
                eq.Enqueue(depEvt, depEvt);
            }
            else
            {
                busy--;
            }
        }

        private void RecordHistory(SimulationStats stats, double t, int sys, int q)
        {
            if (stats.SystemSizeHistory.Count > 0 && Math.Abs(stats.SystemSizeHistory.Last().Time - t) < Epsilon)
            {
                stats.SystemSizeHistory.Last().Count = sys;
                stats.QueueSizeHistory.Last().Count = q;
            }
            else
            {
                stats.SystemSizeHistory.Add(new TimePoint { Time = t, Count = sys });
                stats.QueueSizeHistory.Add(new TimePoint { Time = t, Count = q });
            }
        }

        // Helper methods (GenerateInterarrival / GenerateService) - تأكد أنها موجودة كما في الرد السابق
        private double GenerateInterarrival(SimulationInput input)
        {
            if (input.ArrivalDist == DistributionType.Deterministic) return 1.0 / input.Lambda;
            return -Math.Log(1.0 - _rng.NextDouble()) / input.Lambda;
        }

        private double GenerateService(SimulationInput input)
        {
            if (input.ServiceDist == DistributionType.Deterministic) return 1.0 / input.Mu;
            return -Math.Log(1.0 - _rng.NextDouble()) / input.Mu;
        }
    }

    public enum EventType { Arrival, Departure }
    public class SimulationInput
    {
        public DistributionType ArrivalDist { get; set; }
        public DistributionType ServiceDist { get; set; }
        public int Servers { get; set; }
        public int Capacity { get; set; }
        public QueueDiscipline Discipline { get; set; }
        public double Lambda { get; set; }
        public double Mu { get; set; }
        public int N { get; set; } = 100;
        public bool ForceSimulation { get; set; }
        public int InitialCustomers { get; set; } = 0;
    }
    public class TimePoint
    {
        public double Time { get; set; }
        public int Count { get; set; }
    }

    public class SimulationStats
    {
        public bool IsAnalytical { get; set; }

        public double? FirstBusyTime { get; set; }

        public double W { get; set; }
        public double Wq { get; set; }
        public double L { get; set; }
        public double Lq { get; set; }
        public double Rho { get; set; }

        public List<Customer> Results { get; set; } = new();

        public List<TimePoint> SystemSizeHistory { get; set; } = new();

        public List<TimePoint> QueueSizeHistory { get; set; } = new();
    }
    public class SimulationSnapshot
    {
        public double CurrentTime { get; set; }
        public int QueueLength { get; set; }
        public List<Customer> Customers { get; set; }
    }
}
