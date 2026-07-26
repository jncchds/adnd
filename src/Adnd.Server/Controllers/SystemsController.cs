using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/systems")]
public class SystemsController(ISystemRegistry systemRegistry) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(systemRegistry.GetBuiltInSystems());
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var system = systemRegistry.GetById(id);
        if (system is null)
            return NotFound(new { error = $"System '{id}' not found." });
        return Ok(system);
    }
}
