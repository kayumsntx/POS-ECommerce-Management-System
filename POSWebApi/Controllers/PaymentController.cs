using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POSWebApi.Models;
using POSWebApi.Services;
using System.Globalization;

namespace POSWebApi.Controllers
{
  
    [Route("api/[controller]")]  
    [ApiController]
    [AllowAnonymous]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ISslCommerzService _sslCommerz;
        private readonly SslCommerzSettings _settings;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            AppDbContext db,
            ISslCommerzService sslCommerz,
            IOptions<SslCommerzSettings> settings,
            ILogger<PaymentController> logger)
        {
            _db         = db;
            _sslCommerz = sslCommerz;
            _settings   = settings.Value;
            _logger     = logger;
        }



        // SSLCommerz POSTs form data to these URLs; GET is accepted too for manual testing.
        [HttpPost("success")]
        [HttpGet("success")]
        public async Task<IActionResult> Success(CancellationToken ct)
        {
            var tranId = GetParam("tran_id");
            var valId  = GetParam("val_id");

            if (string.IsNullOrWhiteSpace(tranId))
                return BadRequest(new { message = "tran_id is required." });

            var result = await ConfirmPaymentAsync(tranId, valId, ct);
            return Respond(result, "success");
        }

      
        [HttpPost("fail")]
        [HttpGet("fail")]
        public async Task<IActionResult> Fail(CancellationToken ct)
        {
            var tranId = GetParam("tran_id");
            if (string.IsNullOrWhiteSpace(tranId))
                return BadRequest(new { message = "tran_id is required." });

            var result = await AbandonPaymentAsync(tranId, PaymentStatus.Failed, ct);
            return Respond(result, "fail");
        }

      
        [HttpPost("cancel")]
        [HttpGet("cancel")]
        public async Task<IActionResult> Cancel(CancellationToken ct)
        {
            var tranId = GetParam("tran_id");
            if (string.IsNullOrWhiteSpace(tranId))
                return BadRequest(new { message = "tran_id is required." });

            var result = await AbandonPaymentAsync(tranId, PaymentStatus.Cancelled, ct);
            return Respond(result, "cancel");
        }

       
        [HttpPost("ipn")]
        public async Task<IActionResult> Ipn(CancellationToken ct)
        {
            var tranId = GetParam("tran_id");
            var valId  = GetParam("val_id");
            var status = GetParam("status");

            if (string.IsNullOrWhiteSpace(tranId))
                return BadRequest(new { message = "tran_id is required." });

            if (status != null && status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                await AbandonPaymentAsync(tranId, PaymentStatus.Cancelled, ct);
                return Ok(new { message = "Cancellation notification processed." });
            }

            if (status != null && status.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
            {
                await AbandonPaymentAsync(tranId, PaymentStatus.Failed, ct);
                return Ok(new { message = "Failure notification processed." });
            }

            var result = await ConfirmPaymentAsync(tranId, valId, ct);

            return result.Outcome == Outcome.GatewayUnavailable
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Message })
                : Ok(new { message = result.Message });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Core logic
        // ──────────────────────────────────────────────────────────────────────

        private async Task<PaymentResult> ConfirmPaymentAsync(string tranId, string? valId, CancellationToken ct)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.PaymentTransactionId == tranId, ct);
            if (order == null)
                return new PaymentResult(Outcome.NotFound, null, "No order matches this transaction.");

            // Idempotency: success + IPN both arrive for the same payment.
            if (order.PaymentStatus == PaymentStatus.Paid)
                return new PaymentResult(Outcome.Paid, order, "Payment already recorded.");

            SslCommerzValidationResult? validation = null;
            if (!string.IsNullOrWhiteSpace(valId))
            {
                validation = await _sslCommerz.ValidateAsync(valId, ct);
            }

            decimal paidAmount = order.PayableAmount;

            if (validation != null)
            {
                bool matches =
                    validation.IsValid &&
                    validation.TransactionId.Equals(tranId, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(validation.Currency) ||
                     validation.Currency.Equals("BDT", StringComparison.OrdinalIgnoreCase)) &&
                    (validation.Amount <= 0m || Math.Abs(validation.Amount - order.PayableAmount) < 1.00m);

                if (!matches && !_settings.IsSandbox)
                {
                    _logger.LogWarning(
                        "SSLCommerz validation mismatch for {TranId}: status={Status}, tran={GwTran}, amount={Amount}, currency={Currency}, expected={Expected}",
                        tranId, validation.Status, validation.TransactionId, validation.Amount, validation.Currency, order.PayableAmount);

                    await FailOrderAsync(order, PaymentStatus.Failed, ct);
                    return new PaymentResult(Outcome.Invalid, order, "Payment validation failed.");
                }

                if (validation.Amount > 0m)
                    paidAmount = validation.Amount;
            }
            else if (!_settings.IsSandbox)
            {
                
                return new PaymentResult(Outcome.GatewayUnavailable, order, "Could not validate the payment with the gateway.");
            }
            else
            {
              
                var callbackStatus = GetParam("status");
                if (!string.IsNullOrWhiteSpace(callbackStatus) &&
                    (callbackStatus.Equals("FAILED", StringComparison.OrdinalIgnoreCase) ||
                     callbackStatus.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)))
                {
                    await FailOrderAsync(order, PaymentStatus.Failed, ct);
                    return new PaymentResult(Outcome.Invalid, order, $"Payment status reported as {callbackStatus}.");
                }

                var amountStr = GetParam("currency_amount") ?? GetParam("amount");
                if (decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount) && parsedAmount > 0m)
                {
                    paidAmount = parsedAmount;
                }
            }

            bool alreadyRecorded = await _db.Payments.AnyAsync(p => p.TransactionId == tranId, ct);
            if (!alreadyRecorded)
            {
                _db.Payments.Add(new Payment
                {
                    OrderId         = order.OrderId,
                    PaymentMethodId = (int)order.PaymentMethod,
                    Amount          = paidAmount,
                    TransactionId   = tranId,
                    PaymentDate     = DateTime.UtcNow
                });
            }

            order.PaymentStatus = PaymentStatus.Paid;

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save payment for {TranId}", tranId);
                return new PaymentResult(Outcome.Error, order, "Could not record the payment.");
            }

            return new PaymentResult(Outcome.Paid, order, "Payment successful.");
        }

        private async Task<PaymentResult> AbandonPaymentAsync(string tranId, PaymentStatus newStatus, CancellationToken ct)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.PaymentTransactionId == tranId, ct);
            if (order == null)
                return new PaymentResult(Outcome.NotFound, null, "No order matches this transaction.");

           
            if (order.PaymentStatus == PaymentStatus.Unpaid)
                await FailOrderAsync(order, newStatus, ct);

            return new PaymentResult(
                newStatus == PaymentStatus.Cancelled ? Outcome.Cancelled : Outcome.Failed,
                order,
                newStatus == PaymentStatus.Cancelled ? "Payment cancelled." : "Payment failed.");
        }

      
        private async Task FailOrderAsync(Order order, PaymentStatus status, CancellationToken ct)
        {
            if (order.PaymentStatus != PaymentStatus.Unpaid) return;

            order.PaymentStatus = status;

            if (order.Status != OrderStatus.Cancelled)
            {
                order.Status = OrderStatus.Cancelled;

                var items = await _db.OrderItems.Where(oi => oi.OrderId == order.OrderId).ToListAsync(ct);
                var productIds = items.Select(i => i.ProductId).ToList();
                var inventories = await _db.Inventories
                    .Where(inv => productIds.Contains(inv.ProductId))
                    .OrderBy(inv => inv.InventoryId)
                    .ToListAsync(ct);

                foreach (var item in items)
                {
                    var inv = inventories.FirstOrDefault(i => i.ProductId == item.ProductId);
                    if (inv != null) inv.Quantity += item.Quantity;
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private string? GetParam(string key)
        {
            if (Request.HasFormContentType && Request.Form.TryGetValue(key, out var f) && !string.IsNullOrEmpty(f))
                return f.ToString();
            return Request.Query.TryGetValue(key, out var q) && !string.IsNullOrEmpty(q) ? q.ToString() : null;
        }

       
        private IActionResult Respond(PaymentResult r, string kind)
        {
            var status = (r.Outcome == Outcome.Paid ? "success" : kind == "success" ? "fail" : kind);

            if (r.Order != null && r.Order.IsOnlineOrder)
            {
                var tranParam = !string.IsNullOrWhiteSpace(r.Order.PaymentTransactionId)
                    ? $"&tranId={Uri.EscapeDataString(r.Order.PaymentTransactionId)}"
                    : "";
                var amountParam = r.Order.PayableAmount > 0
                    ? $"&amount={r.Order.PayableAmount.ToString("F2", CultureInfo.InvariantCulture)}"
                    : "";
                return Redirect($"http://localhost:4200/payment-success?status={status}&orderId={r.Order.OrderId}{tranParam}{amountParam}");
            }

            if (!string.IsNullOrWhiteSpace(_settings.PaymentResultUrl))
            {
                var sep = _settings.PaymentResultUrl.Contains('?') ? "&" : "?";
                return Redirect($"{_settings.PaymentResultUrl}{sep}status={status}" +
                                $"&orderId={r.Order?.OrderId}");
            }

            var body = new
            {
                message       = r.Message,
                orderId       = r.Order?.OrderId,
                transactionId = r.Order?.PaymentTransactionId,
                paymentStatus = r.Order?.PaymentStatus.ToString()
            };

            return r.Outcome switch
            {
                Outcome.NotFound           => NotFound(body),
                Outcome.GatewayUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, body),
                Outcome.Error              => StatusCode(StatusCodes.Status500InternalServerError, body),
                Outcome.Invalid            => BadRequest(body),
                _                          => Ok(body)
            };
        }

        private enum Outcome { Paid, Failed, Cancelled, Invalid, NotFound, GatewayUnavailable, Error }

        private record PaymentResult(Outcome Outcome, Order? Order, string Message);
    }
}
