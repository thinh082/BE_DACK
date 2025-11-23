using BE_DACK.Helpers;
using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly DACKContext _context;

        public OrderController(DACKContext context)
        {
            _context = context;
        }

        // Tạo đơn hàng từ giỏ hàng
        [HttpPost("TaoDonHang")]
        public async Task<IActionResult> TaoDonHang()
        {
            try
            {
                // Lấy userId từ token
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                // Lấy giỏ hàng của user
                var gioHang = await _context.ShoppingCarts
                    .Include(g => g.ShoppingCartDetails)
                        .ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(g => g.CustomerId == userId);

                // Kiểm tra giỏ hàng
                if (gioHang == null || !gioHang.ShoppingCartDetails.Any())
                {
                    return BadRequest(new { success = false, message = "Giỏ hàng trống, không thể tạo đơn hàng." });
                }

                // Kiểm tra tồn kho cho tất cả sản phẩm
                foreach (var detail in gioHang.ShoppingCartDetails)
                {
                    if (detail.Product == null)
                    {
                        return BadRequest(new { success = false, message = $"Sản phẩm ID {detail.ProductId} không tồn tại." });
                    }

                    if (detail.SoLuongTrongGh > detail.Product.SoLuongConLaiTrongKho)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Sản phẩm '{detail.Product.TenSp}' không đủ hàng trong kho. Còn lại: {detail.Product.SoLuongConLaiTrongKho}"
                        });
                    }
                }

                // Tính tổng giá trị đơn hàng SAU KHUYẾN MÃI
                decimal tongGiaTriGoc = 0;
                decimal tongGiaTriSauKhuyenMai = 0;

                foreach (var detail in gioHang.ShoppingCartDetails)
                {
                    var giaGoc = detail.Product.Gia;
                    var giaSauKhuyenMai = await PriceHelper.TinhGiaSauKhuyenMai(_context, detail.ProductId.Value, giaGoc);

                    tongGiaTriGoc += detail.SoLuongTrongGh.GetValueOrDefault() * giaGoc;
                    tongGiaTriSauKhuyenMai += detail.SoLuongTrongGh.GetValueOrDefault() * giaSauKhuyenMai;
                }

                // Tạo đơn hàng mới với giá SAU KHUYẾN MÃI
                var donHang = new Order
                {
                    CustomerId = userId,
                    NgayTaoDonHang = DateTime.Now,
                    TongGiaTriDonHang = tongGiaTriSauKhuyenMai, // Lưu giá sau khuyến mãi
                    TrangThai = "Chờ xác nhận"
                };

                _context.Orders.Add(donHang);
                await _context.SaveChangesAsync();

                // Tạo chi tiết đơn hàng và cập nhật tồn kho
                foreach (var detail in gioHang.ShoppingCartDetails)
                {
                    var giaSauKhuyenMai = await PriceHelper.TinhGiaSauKhuyenMai(_context, detail.ProductId.Value, detail.Product.Gia);

                    var orderDetail = new OrderDetail
                    {
                        OrderId = donHang.Id,
                        ProductId = detail.ProductId,
                        SoLuongSp = detail.SoLuongTrongGh.GetValueOrDefault(),
                        Gia = giaSauKhuyenMai, // Lưu giá sau khuyến mãi
                        TrangThai = "Chờ xác nhận"
                    };

                    _context.OrderDetails.Add(orderDetail);

                    // Giảm số lượng tồn kho
                    detail.Product.SoLuongConLaiTrongKho -= detail.SoLuongTrongGh.GetValueOrDefault();
                }

                // Xóa giỏ hàng sau khi tạo đơn
                _context.ShoppingCartDetails.RemoveRange(gioHang.ShoppingCartDetails);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Tạo đơn hàng thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        ngayTao = donHang.NgayTaoDonHang,
                        tongGiaTriGoc = tongGiaTriGoc,
                        tongGiaTriSauKhuyenMai = tongGiaTriSauKhuyenMai,
                        tietKiem = tongGiaTriGoc - tongGiaTriSauKhuyenMai,
                        trangThai = donHang.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tạo đơn hàng.",
                    error = ex.Message
                });
            }
        }


        // Lấy chi tiết đơn hàng
        [HttpGet("LayChiTietDonHang/{orderId}")]
        public async Task<IActionResult> LayChiTietDonHang(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                            .ThenInclude(p => p.ProductImages) // Thêm dòng này
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                var chiTiet = donHang.OrderDetails.Select(d => new
                {
                    productId = d.ProductId,
                    tenSp = d.Product?.TenSp,
                    gia = d.Gia,
                    soLuong = d.SoLuongSp,
                    thanhTien = d.SoLuongSp * d.Gia,
                    hinhAnh = d.Product.ProductImages.Select(img => new
                    {
                        id = img.Id,
                        productId = img.ProductId,
                        url = img.HinhAnh
                    }).ToList(),
                    trangThai = d.TrangThai
                }).ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy chi tiết đơn hàng thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        ngayTao = donHang.NgayTaoDonHang,
                        tongGiaTri = donHang.TongGiaTriDonHang,
                        trangThai = donHang.TrangThai,
                        khachHang = new
                        {
                            hoTen = donHang.Customer?.HoTen,
                            email = donHang.Customer?.Email,
                            sdt = donHang.Customer?.Sdt,
                            diaChi = donHang.Customer?.DiaChi
                        },
                        sanPham = chiTiet
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy chi tiết đơn hàng.",
                    error = ex.Message
                });
            }
        }

        // Lấy danh sách đơn hàng của user
        [HttpGet("LayDanhSachDonHang")]
        public async Task<IActionResult> LayDanhSachDonHang()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var danhSachDonHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                    .Where(o => o.CustomerId == userId)
                    .OrderByDescending(o => o.NgayTaoDonHang)
                    .Select(o => new
                    {
                        orderId = o.Id,
                        ngayTao = o.NgayTaoDonHang,
                        tongGiaTri = o.TongGiaTriDonHang,
                        trangThai = o.TrangThai,
                        soLuongSanPham = o.OrderDetails.Count,
                        sanPham = o.OrderDetails.Select(d => new
                        {
                            tenSp = d.Product.TenSp,
                            soLuong = d.SoLuongSp,
                            gia = d.Gia,
                           /* imageUrl = d.Product.ImageUrl*/
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách đơn hàng thành công.",
                    totalOrders = danhSachDonHang.Count,
                    data = danhSachDonHang
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách đơn hàng.",
                    error = ex.Message
                });
            }
        }

        // Hủy đơn hàng (chỉ khi đơn còn ở trạng thái "Chờ xác nhận")
        [HttpPut("HuyDonHang/{orderId}")]
        public async Task<IActionResult> HuyDonHang(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (donHang.TrangThai != "Chờ xác nhận")
                {
                    return BadRequest(new { success = false, message = "Chỉ có thể hủy đơn hàng đang ở trạng thái 'Chờ xác nhận'." });
                }

                // Hoàn trả số lượng vào kho
                foreach (var detail in donHang.OrderDetails)
                {
                    if (detail.Product != null)
                    {
                        detail.Product.SoLuongConLaiTrongKho += detail.SoLuongSp;
                    }
                }

                // Cập nhật trạng thái
                donHang.TrangThai = "Đã hủy";
                foreach (var detail in donHang.OrderDetails)
                {
                    detail.TrangThai = "Đã hủy";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Hủy đơn hàng thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        trangThai = donHang.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi hủy đơn hàng.",
                    error = ex.Message
                });
            }
        }

        // Admin: Lấy danh sách tất cả đơn hàng (cho admin)
        [HttpGet("DanhSachDonHangAdmin")]
        [Authorize]
        public async Task<IActionResult> DanhSachDonHangAdmin()
        {
            try
            {
                // Kiểm tra quyền admin
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var donHangs = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                    .OrderByDescending(o => o.NgayTaoDonHang)
                    .Select(o => new
                    {
                        id = o.Id,
                        customerId = o.CustomerId,
                        khachHang = o.Customer != null ? new
                        {
                            id = o.Customer.Id,
                            hoTen = o.Customer.HoTen,
                            email = o.Customer.Email,
                            sdt = o.Customer.Sdt,
                            diaChi = o.Customer.DiaChi
                        } : null,
                        ngayTao = o.NgayTaoDonHang,
                        tongGiaTri = o.TongGiaTriDonHang,
                        trangThai = o.TrangThai,
                        soLuongSanPham = o.OrderDetails.Count,
                        chiTietSanPham = o.OrderDetails.Select(d => new
                        {
                            productId = d.ProductId,
                            tenSp = d.Product != null ? d.Product.TenSp : "N/A",
                            soLuong = d.SoLuongSp,
                            gia = d.Gia,
                            thanhTien = d.SoLuongSp * d.Gia
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách đơn hàng thành công",
                    data = donHangs,
                    total = donHangs.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách đơn hàng",
                    error = ex.Message
                });
            }
        }
    }
}