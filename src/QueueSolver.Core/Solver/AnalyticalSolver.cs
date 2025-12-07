using QueueSolver.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace QueueSolver.Core.Solver
{
    public static class AnalyticalSolver
    {
        public static SimulationStats? Solve(SimulationInput input)
        {
            // Only M/M/c or M/M/c/K supported for pure analytics here
            if (input.ArrivalDist != DistributionType.Markovian || input.ServiceDist != DistributionType.Markovian)
                return null;

            double lambda = input.Lambda;
            double mu = input.Mu;
            int c = input.Servers;
            double rho = lambda / (c * mu);

            // Check stability for infinite queues
            if (input.Capacity == int.MaxValue && rho >= 1.0) return null;

            var stats = new SimulationStats { IsAnalytical = true };

            if (c == 1 && input.Capacity == int.MaxValue) // M/M/1
            {
                stats.L = rho / (1 - rho);
                stats.Lq = (rho * rho) / (1 - rho);
                stats.W = 1 / (mu - lambda);
                stats.Wq = rho / (mu - lambda);
                stats.Rho = rho;
            }
            else if (c > 1 && input.Capacity == int.MaxValue) // M/M/c
            {
                // Erlang-C calculation
                double sum = 0;
                for (int n = 0; n < c; n++) sum += Math.Pow(c * rho, n) / Factorial(n);
                double term = Math.Pow(c * rho, c) / (Factorial(c) * (1 - rho));
                double Po = 1.0 / (sum + term);

                stats.Lq = (Po * Math.Pow(lambda / mu, c) * rho) / (Factorial(c) * Math.Pow(1 - rho, 2));
                stats.L = stats.Lq + (lambda / mu);
                stats.Wq = stats.Lq / lambda;
                stats.W = stats.Wq + (1 / mu);
                stats.Rho = rho;
            }
            // Add M/M/1/K logic here if needed...
            else
            {
                return null; // Fallback to simulation
            }

            return stats;
        }

        private static long Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
    }
}
