using LimnosServerControl.Services;
using Microsoft.AspNetCore.Mvc;

namespace LimnosServerControl.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArmaProcessController : ControllerBase
    {
        private readonly AuthService authService;
        private readonly ArmaProcessService armaProcessService;

        public ArmaProcessController(AuthService authService, ArmaProcessService armaProcessService)
        {
            this.authService = authService;
            this.armaProcessService = armaProcessService;
        }

        [HttpGet("restart")]
        public ActionResult RestartServer()
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            armaProcessService.RestartServer();
            return NoContent();
        }

        [HttpGet("start")]
        public ActionResult StartServer()
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            armaProcessService.StartServer();
            return NoContent();
        }

        [HttpGet("stop")]
        public ActionResult StopServer()
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            armaProcessService.StopServer();
            return NoContent();
        }
    }
}
