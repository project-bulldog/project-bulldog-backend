using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
public class ErrorController : ControllerBase
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    // API Route: /error
    [Route("/error")]
    public IActionResult HandleError()
    {
        var context = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var exception = context?.Error;

        if (exception != null)
        {
            _logger.LogError(exception, "An unhandled exception occurred.");
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return Problem(
            detail: env == "Development" ? exception?.Message : "An unexpected error occurred.",
            statusCode: 500,
            title: "Server Error"
        );
    }
}
