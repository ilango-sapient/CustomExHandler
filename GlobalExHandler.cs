using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomExHandler
{
    public sealed class GlobalExHandler(IHostEnvironment env, ILogger<GlobalExHandler> logger) : IExceptionHandler
    {
        private const string UnhandledExceptionMsg = "An unhandled exception has occurred while executing the API request.";
        const string contentType = "application/problem+json";
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        //public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        //{
        //    try
        //    {
        //        await next(context);
        //    }
        //    catch (Exception ex)
        //    {
        //        // _logger.LogError(ex, ex.Message);
        //        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        //        ReadableException readableException = new CustomExceptionHandler.ReadableException
        //        {
        //            status = (int)HttpStatusCode.InternalServerError,
        //            type = "Server error",
        //            title = "Server error",
        //            details = "An internal server error occured.",
        //            errors = new("error", ex.Message)
        //        };

        //        var jsonSettings = new JsonSerializerSettings
        //        {
        //            NullValueHandling = NullValueHandling.Ignore,
        //            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        //        };

        //        // string json = System.Text.Json.JsonSerializer.Serialize<ReadableException>(readableException, jsonSettings);

        //        string json = JsonConvert.SerializeObject(readableException, Newtonsoft.Json.Formatting.Indented);

        //        context.Response.ContentType = "application/json";

        //        await context.Response.WriteAsync(json);

        //    }
        //}

        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
            CancellationToken cancellationToken)
        {
            ///  exception.AddErrorCode();
            logger.LogError(exception, exception is IndexOutOfRangeException ? exception.Message : UnhandledExceptionMsg);

            var problemDetails = CreateProblemDetails(context, exception);
            var json = ToJson(problemDetails);


            context.Response.ContentType = contentType;
            await context.Response.WriteAsync(json, cancellationToken);

            return true;
        }

        private ProblemDetails CreateProblemDetails(in HttpContext context, in Exception exception)
        {
            var errorCode = 500;
            var statusCode = context.Response.StatusCode;
            var reasonPhrase = ReasonPhrases.GetReasonPhrase(statusCode);
            if (string.IsNullOrEmpty(reasonPhrase))
            {
                reasonPhrase = UnhandledExceptionMsg;
            }

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = reasonPhrase,
                Extensions =
            {
                [nameof(errorCode)] = errorCode
            }
            };

            if (env.IsProduction())
            {
                return problemDetails;
            }

            problemDetails.Detail = exception.ToString();
            problemDetails.Extensions["traceId"] = context.TraceIdentifier;
            problemDetails.Extensions["data"] = exception.Data;

            return problemDetails;
        }

        private string ToJson(in ProblemDetails problemDetails)
        {
            try
            {
                return JsonSerializer.Serialize(problemDetails, SerializerOptions);
            }
            catch (Exception ex)
            {
                const string msg = "An exception has occurred while serializing error to JSON";
                logger.LogError(ex, msg);
            }

            return string.Empty;
        }

    }
}
