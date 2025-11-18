using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ShoppingCartController : ControllerBase
    {
        private readonly DACKContext _context;

        public ShoppingCartController(DACKContext context)
        {
            _context = context;
        }

        //Thêm sản phẩm vào giỏ
        [HttpPost("ThemVaoGio")]
        public async Task<IActionResult> ThemVaoGio([FromBody] AddToCartDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });

            try
            {
                //Lấy userId từ token
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                //Kiểm tra sản phẩm tồn tại
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId);
                if (product == null)
                    return NotFound(new { success = false, message = "Sản phẩm không tồn tại." });

                //Kiểm tra tồn kho
                if (dto.SoLuong <= 0)
                    return BadRequest(new { success = false, message = "Số lượng phải lớn hơn 0." });
                if (dto.SoLuong > product.SoLuongConLaiTrongKho)
                    return BadRequest(new { success = false, message = "Không đủ hàng trong kho." });

                //Tìm hoặc tạo giỏ hàng
                var gioHang = await _context.ShoppingCarts
                    .Include(g => g.ShoppingCartDetails)
                    .FirstOrDefaultAsync(g => g.CustomerId == userId);

                if (gioHang == null)
                {
                    gioHang = new ShoppingCart
                    {
                        CustomerId = userId
                    };
                    _context.ShoppingCarts.Add(gioHang);
                    await _context.SaveChangesAsync();
                }

                //Kiểm tra sản phẩm đã có trong giỏ chưa
                var existingItem = gioHang.ShoppingCartDetails.FirstOrDefault(d => d.ProductId == dto.ProductId);

                if (existingItem != null)
                {
                    existingItem.SoLuongTrongGh += dto.SoLuong;
                }
                else
                {
                    var newDetail = new ShoppingCartDetail
                    {
                        CartId = gioHang.Id,
                        ProductId = dto.ProductId,
                        SoLuongTrongGh = dto.SoLuong
                    };
                    _context.ShoppingCartDetails.Add(newDetail);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Đã thêm sản phẩm vào giỏ hàng thành công.",
                    data = new
                    {
                        gioHangId = gioHang.Id,
                        productId = product.Id,
                        tenSp = product.TenSp,
                        soLuong = dto.SoLuong,
                        gia = product.Gia
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi thêm sản phẩm vào giỏ hàng.",
                    error = ex.Message
                });
            }
        }

        //Lấy giỏ hàng của user
        [HttpGet("LayChiTietGioHang")]
        public async Task<IActionResult> LayGioHang()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var gioHang = await _context.ShoppingCarts
                    .Include(g => g.ShoppingCartDetails)
                        .ThenInclude(d => d.Product)
                            .ThenInclude(p => p.ProductImages) // Thêm dòng này
                    .FirstOrDefaultAsync(g => g.CustomerId == userId);

                if (gioHang == null || gioHang.ShoppingCartDetails.Count == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Giỏ hàng hiện đang trống.",
                        data = new List<object>()
                    });
                }

                var danhSach = gioHang.ShoppingCartDetails.Select(d => new
                {
                    productId = d.ProductId,
                    tenSp = d.Product.TenSp,
                    gia = d.Product.Gia,
                    soLuong = d.SoLuongTrongGh,
                    thanhTien = d.SoLuongTrongGh * d.Product.Gia,
                    hinhAnh = d.Product.ProductImages.Select(img => new
                    {
                        id = img.Id,
                        productId = img.ProductId,
                        url = img.HinhAnh
                    }).ToList()
                }).ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy giỏ hàng thành công.",
                    tongSoLuong = danhSach.Sum(x => x.soLuong),
                    tongTien = danhSach.Sum(x => x.thanhTien),
                    data = danhSach
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy giỏ hàng.",
                    error = ex.Message
                });
            }
        }

        //Xóa sản phẩm khỏi giỏ
        [HttpDelete("XoaKhoiGio/{productId}")]
        public async Task<IActionResult> XoaKhoiGio(int productId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var gioHang = await _context.ShoppingCarts
                    .Include(g => g.ShoppingCartDetails)
                    .FirstOrDefaultAsync(g => g.CustomerId == userId);

                if (gioHang == null)
                    return NotFound(new { success = false, message = "Không tìm thấy giỏ hàng của người dùng." });

                var chiTiet = gioHang.ShoppingCartDetails.FirstOrDefault(d => d.ProductId == productId);
                if (chiTiet == null)
                    return NotFound(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng." });

                _context.ShoppingCartDetails.Remove(chiTiet);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Đã xóa sản phẩm khỏi giỏ hàng thành công.",
                    data = new { productId }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi xóa sản phẩm khỏi giỏ hàng.",
                    error = ex.Message
                });
            }
        }
    }

    // DTO
    public class AddToCartDto
    {
        public int ProductId { get; set; }
        public int SoLuong { get; set; }
    }
}
