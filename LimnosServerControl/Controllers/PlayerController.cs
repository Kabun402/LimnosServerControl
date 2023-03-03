using LimnosServerControl.Models;
using LimnosServerControl.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace LimnosServerControl.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayerController : ControllerBase
    {
        private readonly AuthService authService;
        private readonly PlayerService playerService;
        private readonly RConService rconService;
        private readonly BanService banService;

        public PlayerController(AuthService authService, PlayerService playerService, RConService rconService, BanService banService)
        {
            this.authService = authService;
            this.playerService = playerService;
            this.rconService = rconService;
            this.banService = banService;
        }

        [HttpGet("list")]
        public ActionResult GetPlayers()
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            return Ok(playerService.GetPlayers());
        }

        [HttpPost("kick/{pid:length(17)}")]
        public ActionResult KickPlayerBySteamID(string pid, [FromForm] string reason)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            playerService.KickPlayerBySteamID(pid, reason);
            return NoContent();
        }

        [HttpPost("kick/{guid:length(32)}")]
        public ActionResult KickPlayerByGUID(string guid, [FromForm] string reason)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            playerService.KickPlayerByGuid(guid, reason);
            return NoContent();
        }

        [HttpGet("banlist")]
        public async Task<ActionResult> GetBanList()
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            return Ok(await banService.GetBanListAsync());
        }

        [HttpPost("ban/{guid:length(32)}")]
        public ActionResult BanPlayer(string guid, [FromForm] long timestamp, [FromForm] string reason)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            banService.BanPlayerAsync(guid, timestamp, reason);
            return NoContent();
        }

        [HttpDelete("unban/{guid:length(32)}")]
        public ActionResult UnBanPlayer(string guid)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            banService.UnbanPlayerAsync(guid);
            return NoContent();
        }

        [HttpDelete("unban/{ipAddr}")]
        public ActionResult UnBanPlayer(IPAddress ipAddr)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            banService.UnbanPlayerAsync(ipAddr);
            return NoContent();
        }

        [HttpPost("msg")]
        public ActionResult SendGlobalMsg([FromForm] string msg)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            if (rconService.SendGlobalMsg(msg))
                return NoContent();
            else
                return StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpPost("msg/{pid:length(17)}")]
        public ActionResult SendMsgBySteamID(string pid, [FromForm] string msg)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            var player = playerService.Players.FirstOrDefault(p => p.SteamID == pid);
            if (player is null)
                return NotFound();

            if (rconService.SendPrivateMsg(msg, player.ID))
                return NoContent();
            else
                return StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpPost("msg/{guid:length(32)}")]
        public ActionResult SendMsgByGUID(string guid, [FromForm] string msg)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            var player = playerService.Players.FirstOrDefault(p => p.Guid == guid);
            if (player is null)
                return StatusCode(StatusCodes.Status404NotFound);

            if (rconService.SendPrivateMsg(msg, player.ID))
                return NoContent();
            else
                return StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpPost("whitelist/reload")]
        public ActionResult ReloadWhitelist()
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            playerService.ReloadWhitelist();
            return NoContent();
        }

        [HttpPost("whitelist/{pid:length(17)}")]
        public ActionResult ModifyWhitelist(string pid, [FromForm] NameWhitelist nameWhitelist, [FromForm] SteamWhitelist steamWhitelist, [FromForm] IPWhitelist ipWhitelist)
        {
            if (!authService.IsAuhorized(Request))
                return Unauthorized();

            playerService.ModifyWhitelist(pid, nameWhitelist, steamWhitelist, ipWhitelist);
            return NoContent();
        }
    }
}
