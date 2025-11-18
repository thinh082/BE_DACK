using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly DACKContext _context;

        public PaymentController(DACKContext context)
        {
            _context = context;
        }

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
                var phuongThucHopLe = new[] { "Tiền mặt", "Chuyển khoản", "Thẻ tín dụng", "Ví điện tử", "COD" };
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
                    PhuongThucThanhToan = dto.PhuongThucThanhToan,
                    TrangThai = "Thành công" // Có thể mở rộng thành "Đang xử lý" cho các cổng thanh toán online
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

                return Ok(new
                {
                    success = true,
                    message = "Thanh toán thành công.",
                    data = new
                    {
                        paymentId = thanhToan.Id,
                        orderId = donHang.Id,
                        soTienThanhToan = thanhToan.SoTienThanhToan,
                        phuongThuc = thanhToan.PhuongThucThanhToan,
                        ngayThanhToan = thanhToan.NgayThanhToan,
                        trangThaiDonHang = donHang.TrangThai,
                        tongDaThanhToan = tongDaThanhToan,
                        conLai = donHang.TongGiaTriDonHang - tongDaThanhToan
                    }
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
    }

    // DTO
    public class ThanhToanDto
    {
        public int OrderId { get; set; }
        public decimal SoTien { get; set; }
        public string PhuongThucThanhToan { get; set; } = "Tiền mặt";
    }
}