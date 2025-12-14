using System;
using System.Collections.Generic;
using System.Text;

namespace QueueSolver.Core.Solver
{
    public static class DeterministicSolver
    {
        private const double Epsilon = 1e-9;

        public static double FindFirstBalkTime(double lambda, double mu, int servers, int capacity)
        {
            double Ta = 1.0 / lambda;
            double Ts = 1.0 / mu;

            if (Ts <= Ta || servers != 1) return -1;

            for (int t = (int)Ta; t <= 100; t++)
            {
                double checkValue = Math.Floor(t / Ta) - Math.Floor(t / Ts - (Ta / Ts));
                if (Math.Abs(checkValue - 5.0) < Epsilon)
                    return t;
            }

            return -1;
        }

        public static double FindFirstEmptyTime(double lambda, double mu, int initialCustomers)
        {
            if (lambda >= mu || initialCustomers <= 0) return -1;

            for (double t = 0.1; t < 5000.0; t += 0.01)
            {
                double checkValue = Math.Floor(mu * t) - Math.Floor(lambda * t);
                if (Math.Abs(checkValue - initialCustomers) < Epsilon)
                    return t;
            }

            return -1;
        }
    }
}
