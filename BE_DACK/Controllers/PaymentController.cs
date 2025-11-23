using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuanLyDatVeMayBay.Services.VnpayServices;
using QuanLyDatVeMayBay.Services.VnpayServices.Enums;
using VNPAY.NET.Models;
using VNPAY.NET.Utilities;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class PaymentController : ControllerBase
    {
        private readonly DACKContext _context;

        private readonly IConfiguration _configuration;

        private readonly IVnpay _vnpay;

        private readonly IOptions<VNPaySettings> _cfg;
        public PaymentController(DACKContext context , IConfiguration configuration , IVnpay vnpay , IOptions<VNPaySettings> cfg)
        {
            _context = context;
            _configuration = configuration;
            _vnpay = vnpay;
            _cfg = cfg;
        }
        [Authorize]
        // Tạo thanh toán cho đơn hàng
        [HttpPost("ThanhToan")]
        public async Task<IActionResult> ThanhToan([FromBody] ThanhToanDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });

            try
            {
                // Lấy userId từ token
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                // Kiểm tra đơn hàng
                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                // Kiểm tra trạng thái đơn hàng
                if (donHang.TrangThai == "Đã hủy")
                {
                    return BadRequest(new { success = false, message = "Đơn hàng đã bị hủy, không thể thanh toán." });
                }

                if (donHang.TrangThai == "Đã thanh toán" || donHang.TrangThai == "Hoàn thành")
                {
                    return BadRequest(new { success = false, message = "Đơn hàng đã được thanh toán." });
                }

                // Kiểm tra số tiền thanh toán
                decimal tongDaThanhToan = donHang.Payments
                    .Where(p => p.TrangThai == "Thành công")
                    .Sum(p => p.SoTienThanhToan);

                decimal conLai = donHang.TongGiaTriDonHang - tongDaThanhToan;

                if (dto.SoTien <= 0)
                {
                    return BadRequest(new { success = false, message = "Số tiền thanh toán phải lớn hơn 0." });
                }

                if (dto.SoTien > conLai)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Số tiền thanh toán vượt quá số tiền còn lại. Còn lại: {conLai:N0}đ"
                    });
                }

                // Validate phương thức thanh toán
                var phuongThucHopLe = new[] { "VNPAY", "COD" };
                if (!phuongThucHopLe.Contains(dto.PhuongThucThanhToan))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Phương thức thanh toán không hợp lệ. Chọn: {string.Join(", ", phuongThucHopLe)}"
                    });
                }




                // Tạo thanh toán mới
                var thanhToan = new Payment
                {
                    OrderId = dto.OrderId,
                    NgayThanhToan = DateTime.Now,
                    SoTienThanhToan = dto.SoTien,
                    PhuongThucThanhToan = dto.PhuongThucThanhToan,//
                    TrangThai = "Chờ thanh toán" // Có thể mở rộng thành "Đang xử lý" cho các cổng thanh toán online
                };

                _context.Payments.Add(thanhToan);


                // Cập nhật trạng thái đơn hàng
                tongDaThanhToan += dto.SoTien;

                if (tongDaThanhToan >= donHang.TongGiaTriDonHang)
                {
                    donHang.TrangThai = "Đã thanh toán";

                    // Cập nhật trạng thái chi tiết đơn hàng
                    foreach (var detail in donHang.OrderDetails)
                    {
                        detail.TrangThai = "Đã thanh toán";
                    }
                }
                else
                {
                    donHang.TrangThai = "Thanh toán một phần";
                }

                await _context.SaveChangesAsync();
                if (dto.PhuongThucThanhToan == "VNPAY")
                {
                    var cfg = _cfg.Value;

                    _vnpay.Initialize(
                      cfg.vnp_TmnCode,
                      cfg.vnp_HashSecret,
                      cfg.vnp_ReturnUrl,
                      cfg.vnp_Url
                     );

                    var ipAddress = NetworkHelper.GetIpAddress(HttpContext);
                    var request = new PaymentRequest
                    {
                        PaymentId = thanhToan.Id,
                        Money = (double)tongDaThanhToan,
                        Description = "Thanh toán đồ nội thất!",
                        IpAddress = ipAddress,
                        CreatedDate = DateTime.Now,
                        Currency = Currency.VND,
                        Language = DisplayLanguage.Vietnamese
                    };
                    var url = _vnpay.GetPaymentUrl(request);
                    return Ok(new
                    {
                        code = 202,
                        url = url
                    });

                }
                return Ok(new
                {
                    success = true,
                    message = "Thanh toán thành công.",
                    /*data = new
                    {
                        paymentId = thanhToan.Id,
                        orderId = donHang.Id,
                        soTienThanhToan = thanhToan.SoTienThanhToan,
                        phuongThuc = thanhToan.PhuongThucThanhToan,
                        ngayThanhToan = thanhToan.NgayThanhToan,
                        trangThaiDonHang = donHang.TrangThai,
                        tongDaThanhToan = tongDaThanhToan,
                        conLai = donHang.TongGiaTriDonHang - tongDaThanhToan
                    }*/
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi thanh toán.",
                    error = ex.Message
                });
            }
        }
        [Authorize]
        // Lấy lịch sử thanh toán của 1 đơn hàng
        [HttpGet("LichSuThanhToan/{orderId}")]
        public async Task<IActionResult> LichSuThanhToan(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                // Kiểm tra đơn hàng thuộc về user
                var donHang = await _context.Orders
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                var lichSu = donHang.Payments
                    .OrderByDescending(p => p.NgayThanhToan)
                    .Select(p => new
                    {
                        paymentId = p.Id,
                        ngayThanhToan = p.NgayThanhToan,
                        soTien = p.SoTienThanhToan,
                        phuongThuc = p.PhuongThucThanhToan,
                        trangThai = p.TrangThai
                    })
                    .ToList();

                decimal tongDaThanhToan = donHang.Payments
                    .Where(p => p.TrangThai == "Thành công")
                    .Sum(p => p.SoTienThanhToan);

                return Ok(new
                {
                    success = true,
                    message = "Lấy lịch sử thanh toán thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        tongGiaTriDonHang = donHang.TongGiaTriDonHang,
                        tongDaThanhToan = tongDaThanhToan,
                        conLai = donHang.TongGiaTriDonHang - tongDaThanhToan,
                        trangThaiDonHang = donHang.TrangThai,
                        lichSuThanhToan = lichSu
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy lịch sử thanh toán.",
                    error = ex.Message
                });
            }
        }
        [Authorize]
        // Lấy tất cả thanh toán của user
        [HttpGet("TatCaThanhToan")]
        public async Task<IActionResult> TatCaThanhToan()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var danhSach = await _context.Payments
                    .Include(p => p.Order)
                    .Where(p => p.Order.CustomerId == userId)
                    .OrderByDescending(p => p.NgayThanhToan)
                    .Select(p => new
                    {
                        paymentId = p.Id,
                        orderId = p.OrderId,
                        ngayThanhToan = p.NgayThanhToan,
                        soTien = p.SoTienThanhToan,
                        phuongThuc = p.PhuongThucThanhToan,
                        trangThai = p.TrangThai,
                        trangThaiDonHang = p.Order.TrangThai
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách thanh toán thành công.",
                    tongSoGiaoDich = danhSach.Count,
                    tongSoTien = danhSach.Where(p => p.trangThai == "Thành công").Sum(p => p.soTien),
                    data = danhSach
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách thanh toán.",
                    error = ex.Message
                });
            }
        }
        [Authorize]
        // Kiểm tra trạng thái thanh toán đơn hàng
        [HttpGet("KiemTraThanhToan/{orderId}")]
        public async Task<IActionResult> KiemTraThanhToan(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                decimal tongDaThanhToan = donHang.Payments
                    .Where(p => p.TrangThai == "Thành công")
                    .Sum(p => p.SoTienThanhToan);

                decimal conLai = donHang.TongGiaTriDonHang - tongDaThanhToan;
                bool daThanhToanDu = conLai <= 0;

                return Ok(new
                {
                    success = true,
                    message = "Kiểm tra trạng thái thanh toán thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        tongGiaTri = donHang.TongGiaTriDonHang,
                        daThanhToan = tongDaThanhToan,
                        conLai = conLai,
                        daThanhToanDu = daThanhToanDu,
                        trangThai = donHang.TrangThai,
                        soLanThanhToan = donHang.Payments.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi kiểm tra thanh toán.",
                    error = ex.Message
                });
            }
        }
        [Authorize]
        // Hủy thanh toán (chỉ trong trường hợp đặc biệt)
        [HttpPut("HuyThanhToan/{paymentId}")]
        public async Task<IActionResult> HuyThanhToan(int paymentId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var thanhToan = await _context.Payments
                    .Include(p => p.Order)
                        .ThenInclude(o => o.OrderDetails)
                    .FirstOrDefaultAsync(p => p.Id == paymentId && p.Order.CustomerId == userId);

                if (thanhToan == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy giao dịch thanh toán." });
                }

                if (thanhToan.TrangThai == "Đã hủy")
                {
                    return BadRequest(new { success = false, message = "Giao dịch này đã bị hủy trước đó." });
                }

                // Chỉ cho phép hủy trong vòng 24h và đơn hàng chưa giao
                var khoangThoiGian = DateTime.Now - thanhToan.NgayThanhToan;
                if (khoangThoiGian.TotalHours > 24)
                {
                    return BadRequest(new { success = false, message = "Chỉ có thể hủy thanh toán trong vòng 24 giờ." });
                }

                if (thanhToan.Order.TrangThai == "Đang giao hàng" || thanhToan.Order.TrangThai == "Hoàn thành")
                {
                    return BadRequest(new { success = false, message = "Không thể hủy thanh toán khi đơn hàng đang/đã giao." });
                }

                // Cập nhật trạng thái thanh toán
                thanhToan.TrangThai = "Đã hủy";

                // Cập nhật lại trạng thái đơn hàng
                var tongConLai = thanhToan.Order.Payments
                    .Where(p => p.TrangThai == "Thành công" && p.Id != paymentId)
                    .Sum(p => p.SoTienThanhToan);

                if (tongConLai <= 0)
                {
                    thanhToan.Order.TrangThai = "Chờ xác nhận";
                    foreach (var detail in thanhToan.Order.OrderDetails)
                    {
                        detail.TrangThai = "Chờ xác nhận";
                    }
                }
                else if (tongConLai < thanhToan.Order.TongGiaTriDonHang)
                {
                    thanhToan.Order.TrangThai = "Thanh toán một phần";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Hủy thanh toán thành công. Số tiền sẽ được hoàn lại trong 3-5 ngày.",
                    data = new
                    {
                        paymentId = thanhToan.Id,
                        soTienHoan = thanhToan.SoTienThanhToan,
                        trangThaiDonHang = thanhToan.Order.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi hủy thanh toán.",
                    error = ex.Message
                });
            }


           

        }

        [HttpGet("ReturnVnPay")]
        public async Task<IActionResult> ReturnVnPay()
        {
            if (Request.QueryString.HasValue)
            {
                try
                {
                    var paymentResult = _vnpay.GetPaymentResult(Request.Query);
                    var ThanhToan = await _context.Payments.FindAsync((int)paymentResult.PaymentId);
                    if (ThanhToan == null)
                    {
                        return BadRequest(new
                        {
                            message = "Không tìm thấy thông tin thanh toán"
                        });
                    }
                    ThanhToan.TrangThai = "Đã thanh toán thành công!";
                    _context.Payments.Update(ThanhToan);

                    await _context.SaveChangesAsync();


                    string html = @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thanh toán thành công - Decora</title>
    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap' rel='stylesheet'>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        body {
            font-family: 'Inter', sans-serif;
            font-weight: 400;
            line-height: 28px;
            color: #6a6a6a;
            font-size: 14px;
            background-color: #eff2f1;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            padding: 20px;
        }
        .success-container {
            background-color: #ffffff;
            text-align: center;
            padding: 60px 40px;
            border-radius: 16px;
            box-shadow: 0 6px 25px rgba(0, 0, 0, 0.1);
            max-width: 500px;
            width: 100%;
        }
        .checkmark-icon {
            width: 80px;
            height: 80px;
            background-color: #3b5d50;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 30px;
            color: #ffffff;
            font-size: 48px;
            font-weight: 700;
        }
        h1 {
            font-weight: 700;
            color: #2f2f2f;
            margin-bottom: 15px;
            font-size: 32px;
        }
        .success-message {
            color: #6a6a6a;
            margin-bottom: 30px;
            font-size: 16px;
            line-height: 1.6;
        }
        .countdown {
            color: #3b5d50;
            font-weight: 600;
            margin-bottom: 30px;
            font-size: 14px;
        }
        .btn {
            font-weight: 600;
            padding: 12px 30px;
            border-radius: 8px;
            color: #ffffff;
            font-size: 0.9rem;
            background: #2f2f2f;
            border-color: #2f2f2f;
            text-decoration: none;
            display: inline-block;
            transition: all 0.2s ease;
            border: none;
            cursor: pointer;
        }
        .btn:hover {
            background: #3b5d50;
            border-color: #3b5d50;
            color: #ffffff;
            text-decoration: none;
        }
        .btn-secondary {
            background: #f9bf29;
            border-color: #f9bf29;
            color: #2f2f2f;
            margin-left: 15px;
        }
        .btn-secondary:hover {
            background: #e6a91f;
            border-color: #e6a91f;
            color: #2f2f2f;
        }
    </style>
    <script>
        let countdown = 5;
        const countdownElement = document.getElementById('countdown');
        
        function updateCountdown() {
            if (countdownElement) {
                countdownElement.textContent = 'Tự động chuyển về trang chủ sau ' + countdown + ' giây...';
            }
            countdown--;
            if (countdown < 0) {
                window.location.href = 'http://localhost:5173/';
            }
        }
        
        window.onload = function() {
            setInterval(updateCountdown, 1000);
        };
    </script>
</head>
<body>
    <div class='success-container'>
        <div class='checkmark-icon'>✓</div>
        <h1>Thanh toán thành công!</h1>
        <p class='success-message'>Cảm ơn bạn đã mua hàng tại Decora.<br>Đơn hàng của bạn đang được xử lý và sẽ được giao trong thời gian sớm nhất.</p>
        <p class='countdown' id='countdown'>Tự động chuyển về trang chủ sau 5 giây...</p>
        <div>
            <a href='http://localhost:5173/' class='btn'>Về trang chủ</a>
            <a href='http://localhost:5173/orders.html' class='btn btn-secondary'>Xem đơn hàng</a>
        </div>
    </div>
</body>
</html>";

                    return Content(html, "text/html");

                }
                catch (Exception ex)
                {
                    return BadRequest(new { success = false, message = "Lỗi thanh toán." });
                }
            }

            return NotFound("có gì đó xảy ra rồi");
        }
    }

    // DTO
    public class ThanhToanDto
    {
        public int OrderId { get; set; }
        public decimal SoTien { get; set; }
        public string PhuongThucThanhToan { get; set; } = "Tiền mặt";
    }
}