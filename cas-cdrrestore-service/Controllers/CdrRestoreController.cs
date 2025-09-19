using cas_cdrrestore_service.Models;
using cas_cdrrestore_service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace cas_cdrrestore_service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CdrRestoreController : ControllerBase
    {
        private readonly RestoreService _restoreService;
        public CdrRestoreController(RestoreService restoreService)
        {
            _restoreService = restoreService;
        }
        [HttpPost("RestoreAsync")]
        public async Task<IActionResult> RestoreAsync([FromBody] RestoreRequest request)
        {
            return new OkObjectResult( await _restoreService.RestoreSingleAsync(request.CallDate, request.CallId, CancellationToken.None));
        }
        [HttpPost("RestoreManyAsync")]
        public async Task<IActionResult> RestoreManyAsync([FromBody] List<RestoreRequest> request)
        {
            return new OkObjectResult(await _restoreService.RestoreManyAsync(request));
        }
    }
}
