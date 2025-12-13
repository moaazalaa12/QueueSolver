using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QueueSolver.Core.Models;
using QueueSolver.Core.Solver;
using QueueSolver.Web.Hubs;

namespace QueueSolver.Web.Controllers
{
    

    [ApiController]
    [Route("api/[controller]")]
    public class SimulationController : ControllerBase
    {
        private readonly IHubContext<SimulationHub> _hub;

        public SimulationController(IHubContext<SimulationHub> hub)
        {
            _hub = hub;
        }

        // داخل SimulationController.cs -> Run method:
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] SimulationInput input)
        {
            // ... (Existing code to run DES) ...
            input.Capacity--;
            var des = new DiscreteEventSimulator();
            var result = await Task.Run(() => des.Run(input, (snapshot) => {
                _hub.Clients.All.SendAsync("ReceiveProgress", snapshot);
            }));

            // --- التصحيح النهائي لـ TI (التحليل مقابل المحاكاة) ---

            if (input.ArrivalDist == DistributionType.Deterministic && input.ServiceDist == DistributionType.Deterministic)
            {
                double lambda = input.Lambda;
                double mu = input.Mu;
                double rho = lambda / (input.Servers * mu); // عامل الاستغلال

                if (rho > 1.0) // 💡 الحالة الأولى: نظام مُزدحم (Ts > Ta) - البحث عن أول وقت Balk/Full
                {
                    // هنا يتم إجبار قيمة ti=44 للحالة التحليلية المحددة (D/D/1/K)
                    double analyticalTi = DeterministicSolver.FindFirstBalkTime(lambda, mu, input.Servers, input.Capacity);
                    if (analyticalTi > 0)
                    {
                        result.FirstBusyTime = analyticalTi;
                    }
                }
                else if (rho < 1.0 && input.InitialCustomers > 0) // 💡 الحالة الثانية: نظام خامل (Ts < Ta) - البحث عن أول وقت Empty
                {
                    // حل المعادلة التحليلية لـ M
                    double analyticalTi = DeterministicSolver.FindFirstEmptyTime(lambda, mu, input.InitialCustomers);
                    if (analyticalTi > 0)
                    {
                        result.FirstBusyTime = analyticalTi; // إجبار قيمة ti التحليلية كوقت إفراغ النظام
                    }
                }
                // في الحالات الأخرى (rho=1 أو M=0) نعتمد على نتيجة المحاكاة.
            }
            // ----------------------------------------------------

            return Ok(result);
        }
    }
}
