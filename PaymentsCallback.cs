using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LoanAPI
{
    public class PaymentsCallback : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentsCallback> _logger;

        // Inject configuration and logger via the constructor
        public PaymentsCallback(IConfiguration config, ILogger<PaymentsCallback> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string? reference, [FromQuery] string? trxref)
        {
            reference = string.IsNullOrWhiteSpace(reference) ? trxref : reference;

            // Paystack references are short and URL-safe; reject anything else before using it in a redirect
            if (string.IsNullOrWhiteSpace(reference) || !Regex.IsMatch(reference, "^[A-Za-z0-9._=-]{1,100}$"))
                return BadRequest("Invalid payment reference.");

            try
            {
                // === your existing logic: verify with Paystack (GET /transaction/verify/{reference})
                // and credit the wallet. It MUST be idempotent: the webhook may process the same reference too.
                // await _paymentService.VerifyAndCreditAsync(reference);
            }
            catch (Exception ex)
            {
                // Don't show the user an API error page. The front-end reads the real status from the transaction record.
                _logger.LogError(ex, "Payment callback verification failed for {Reference}", reference);
            }

            // The redirect target comes from configuration only (never from the request), so this can't be used as an open redirect
            var frontendBase = _config["Frontend:BaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrEmpty(frontendBase))
                return Problem("Frontend:BaseUrl is not configured.");

            return Redirect($"{frontendBase}/?reference={Uri.EscapeDataString(reference)}");
        }
    }
}