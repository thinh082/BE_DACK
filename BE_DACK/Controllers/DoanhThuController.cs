using System.Globalization;
using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoanhThuController : ControllerBase
    {
        private readonly DACKContext _context;
        // Các định dạng ngày tháng chấp nhận được
        private static readonly string[] AcceptedDateFormats = { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
       
        private const string PAYMENT_SUCCESS = "Thành công";

        public DoanhThuController(DACKContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Query cơ bản: Chỉ lấy các giao dịch có trạng thái "Thành công"
        /// </summary>
        private IQueryable<Payment> BasePaymentQuery()
        {
            return _context.Payments
                .Include(p => p.Order)
                    .ThenInclude(o => o.Customer)
                // SỬA QUAN TRỌNG: So sánh với chuỗi "Thành công"
                // Trim() giúp loại bỏ khoảng trắng thừa nếu lỡ tay nhập sai trong DB
                .Where(p => p.TrangThai.Trim() == PAYMENT_SUCCESS);
        }

        // Helper build thông tin đơn hàng để code gọn hơn
        private static object BuildOrderInfo(Payment payment)
        {
            return new
            {
                payment.OrderId,
                payment.Order?.NgayTaoDonHang,
                payment.Order?.TongGiaTriDonHang,
                payment.Order?.TrangThai,
                Customer = payment.Order?.Customer == null
                    ? null
                    : new
                    {
                        payment.Order.Customer.Id,
                        payment.Order.Customer.HoTen,
                        payment.Order.Customer.Email,
                        payment.Order.Customer.Sdt
                    }
            };
        }

        #region Theo Ngày / Tháng / Năm

        [HttpGet("TheoNgay")]
        public async Task<IActionResult> GetByDate([FromQuery] string ngay)
        {
            if (string.IsNullOrWhiteSpace(ngay))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập ngày theo định dạng dd-MM-yyyy." });
            }

            if (!DateTime.TryParseExact(ngay, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayFilter))
            {
                return BadRequest(new { success = false, message = "Định dạng ngày không hợp lệ. Ví dụ hợp lệ: 20-11-2025." });
            }

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Date == ngayFilter.Date)
                .ToListAsync();

            var chiTiet = payments.Select(p => new
            {
                p.Id,
                p.OrderId,
                p.SoTienThanhToan,
                p.PhuongThucThanhToan,
                p.NgayThanhToan,
                Order = BuildOrderInfo(p)
            }).ToList();

            return Ok(new
            {
                success = true,
                ngay = ngayFilter.ToString("dd/MM/yyyy"),
                tongDoanhThu = chiTiet.Sum(x => x.SoTienThanhToan),
                soDon = chiTiet.Count,
                chiTiet
            });
        }

        [HttpGet("TheoThang")]
        public async Task<IActionResult> GetByMonth([FromQuery] int thang, [FromQuery] int nam)
        {
            if (thang < 1 || thang > 12)
                return BadRequest(new { success = false, message = "Tháng không hợp lệ." });

            // Nới lỏng điều kiện năm để test được dữ liệu tương lai (2025)
            if (nam < 2000 || nam > 2100)
                return BadRequest(new { success = false, message = "Năm không hợp lệ." });

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Month == thang && p.NgayThanhToan.Year == nam)
                .ToListAsync();

            var chiTietTheoNgay = payments
                .GroupBy(p => p.NgayThanhToan.Date)
                .Select(g => new
                {
                    ngay = g.Key.ToString("dd/MM/yyyy"),
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count()
                })
                .OrderBy(x => x.ngay)
                .ToList();

            return Ok(new
            {
                success = true,
                thang = thang.ToString("00"),
                nam,
                tongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                soDon = payments.Count,
                chiTietTheoNgay
            });
        }

        [HttpGet("TheoNam")]
        public async Task<IActionResult> GetByYear([FromQuery] int nam)
        {
            if (nam < 2000 || nam > 2100)
                return BadRequest(new { success = false, message = "Năm không hợp lệ." });

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Year == nam)
                .ToListAsync();

            var chiTietTheoThang = payments
                .GroupBy(p => p.NgayThanhToan.Month)
                .Select(g => new
                {
                    thang = g.Key,
                    tenThang = $"Tháng {g.Key}",
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count()
                })
                .OrderBy(x => x.thang)
                .ToList();

            return Ok(new
            {
                success = true,
                nam,
                tongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                soDon = payments.Count,
                chiTietTheoThang
            });
        }

        #endregion

        #region Khoảng thời gian & Thống kê chung

        [HttpGet("TheoKhoangThoiGian")]
        public async Task<IActionResult> GetByRange([FromQuery] string tuNgay, [FromQuery] string denNgay)
        {
            if (!DateTime.TryParseExact(tuNgay, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tuNgayFilter) ||
                !DateTime.TryParseExact(denNgay, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var denNgayFilter))
            {
                return BadRequest(new { success = false, message = "Định dạng ngày không hợp lệ." });
            }

            if (tuNgayFilter > denNgayFilter)
                return BadRequest(new { success = false, message = "Từ ngày phải nhỏ hơn đến ngày." });

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Date >= tuNgayFilter.Date && p.NgayThanhToan.Date <= denNgayFilter.Date)
                .ToListAsync();

            var chiTietTheoNgay = payments
                .GroupBy(p => p.NgayThanhToan.Date)
                .Select(g => new
                {
                    ngay = g.Key.ToString("dd/MM/yyyy"),
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count()
                })
                .OrderBy(x => x.ngay)
                .ToList();

            var soNgay = (denNgayFilter - tuNgayFilter).Days + 1;

            return Ok(new
            {
                success = true,
                tuNgay = tuNgayFilter.ToString("dd/MM/yyyy"),
                denNgay = denNgayFilter.ToString("dd/MM/yyyy"),
                soNgay,
                tongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                soDon = payments.Count,
                chiTietTheoNgay
            });
        }

        [HttpGet("ThongKeChung")]
        public async Task<IActionResult> GetOverview()
        {
            // Lấy toàn bộ các giao dịch "Thành công" về để tính toán
            var tatCaThanhToan = await BasePaymentQuery().ToListAsync();

            var homNay = DateTime.Today; // 00:00:00 hôm nay
            var thangHienTai = DateTime.Now.Month;
            var namHienTai = DateTime.Now.Year;

            // Lọc dữ liệu trên RAM (Client evaluation)
            var doanhThuHomNay = tatCaThanhToan.Where(p => p.NgayThanhToan.Date == homNay).ToList();
            var doanhThuThangNay = tatCaThanhToan.Where(p => p.NgayThanhToan.Month == thangHienTai && p.NgayThanhToan.Year == namHienTai).ToList();
            var doanhThuNamNay = tatCaThanhToan.Where(p => p.NgayThanhToan.Year == namHienTai).ToList();

            var topKhachHang = tatCaThanhToan
                .Where(p => p.Order?.Customer != null)
                .GroupBy(p => new
                {
                    p.Order!.Customer!.Id,
                    p.Order.Customer.HoTen,
                    p.Order.Customer.Email,
                    p.Order.Customer.Sdt
                })
                .Select(g => new
                {
                    khachHangId = g.Key.Id,
                    hoTen = g.Key.HoTen,
                    email = g.Key.Email,
                    sdt = g.Key.Sdt,
                    tongChi = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count()
                })
                .OrderByDescending(x => x.tongChi)
                .Take(5)
                .ToList();

            var thongKePhuongThuc = tatCaThanhToan
                .GroupBy(p => p.PhuongThucThanhToan ?? "Không xác định")
                .Select(g => new
                {
                    phuongThuc = g.Key,
                    soLuong = g.Count(),
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    tyLe = tatCaThanhToan.Count == 0 ? 0 : Math.Round((decimal)g.Count() * 100 / tatCaThanhToan.Count, 2)
                })
                .OrderByDescending(x => x.tongTien)
                .ToList();

            // Biểu đồ 7 ngày gần nhất
            var bieudoTuanNay = Enumerable.Range(0, 7)
                .Select(i => homNay.AddDays(-i))
                .Select(ngay => new
                {
                    ngay = ngay.ToString("dd/MM"),
                    ngayDayDu = ngay.ToString("dd/MM/yyyy"),
                    doanhThu = tatCaThanhToan.Where(p => p.NgayThanhToan.Date == ngay).Sum(p => p.SoTienThanhToan),
                    soDon = tatCaThanhToan.Count(p => p.NgayThanhToan.Date == ngay)
                })
                .OrderBy(x => DateTime.ParseExact(x.ngayDayDu, "dd/MM/yyyy", CultureInfo.InvariantCulture))
                .ToList();

            // Thống kê trạng thái đơn hàng (Cần query riêng từ bảng Order)
            var thongKeTrangThaiDonHang = await _context.Orders
                .GroupBy(o => o.TrangThai)
                .Select(g => new
                {
                    trangThai = g.Key,
                    soLuong = g.Count(),
                    tongGiaTri = g.Sum(o => o.TongGiaTriDonHang)
                })
                .OrderByDescending(x => x.soLuong)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                tongQuat = new
                {
                    tongDoanhThuTatCa = tatCaThanhToan.Sum(p => p.SoTienThanhToan),
                    tongSoDon = tatCaThanhToan.Count
                },
                homNay = new
                {
                    ngay = homNay.ToString("dd/MM/yyyy"),
                    tongDoanhThu = doanhThuHomNay.Sum(p => p.SoTienThanhToan),
                    soDon = doanhThuHomNay.Count
                },
                thangNay = new
                {
                    thang = thangHienTai,
                    nam = namHienTai,
                    tongDoanhThu = doanhThuThangNay.Sum(p => p.SoTienThanhToan),
                    soDon = doanhThuThangNay.Count
                },
                namNay = new
                {
                    nam = namHienTai,
                    tongDoanhThu = doanhThuNamNay.Sum(p => p.SoTienThanhToan),
                    soDon = doanhThuNamNay.Count
                },
                topKhachHang,
                thongKePhuongThuc,
                bieudoTuanNay,
                thongKeTrangThaiDonHang
            });
        }

        #endregion

        #region Đơn hàng (Logic giữ nguyên nhưng lọc theo PAYMENT_SUCCESS)

        [HttpGet("ThongKeDonHang")]
        public async Task<IActionResult> GetOrderStatistic()
        {
            var thongKe = await _context.Orders
                .Include(o => o.Payments)
                .GroupBy(o => o.TrangThai)
                .Select(g => new
                {
                    trangThai = g.Key,
                    soLuongDon = g.Count(),
                    tongGiaTri = g.Sum(o => o.TongGiaTriDonHang),
                    // Chỉ tính tiền những giao dịch "Thành công"
                    daDuocThanhToan = g.Sum(o => o.Payments
                        .Where(p => p.TrangThai == PAYMENT_SUCCESS)
                        .Sum(p => p.SoTienThanhToan)),
                    // Còn lại = Tổng - Đã thanh toán
                    conLai = g.Sum(o => o.TongGiaTriDonHang) - g.Sum(o => o.Payments
                        .Where(p => p.TrangThai == PAYMENT_SUCCESS)
                        .Sum(p => p.SoTienThanhToan))
                })
                .OrderByDescending(x => x.soLuongDon)
                .ToListAsync();

            var tongDon = await _context.Orders.CountAsync();
            var tongGiaTriTatCaDon = await _context.Orders.SumAsync(o => o.TongGiaTriDonHang);

            return Ok(new
            {
                success = true,
                tongDon,
                tongGiaTriTatCaDon,
                chiTietTheoTrangThai = thongKe
            });
        }

        #endregion
    }
}