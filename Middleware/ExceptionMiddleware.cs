using System.Text.Json;

namespace FastFoodOrderingSystem.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Return full exception details in development so you can see inner TypeLoadException and stack trace.
            var response = new
            {
                statusCode = context.Response.StatusCode,
                message = "An error occurred while processing your request.",
                detailed = _env.IsDevelopment() ? exception.ToString() : null
            };

            // Use System.Text.Json with default options
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}