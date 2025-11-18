using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppDoCongNghe.Service;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly DACKContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public ProductController(DACKContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }
        [HttpGet("DanhSachSanPham")]
        public IActionResult DanhSachSanPham()
        {
           
            try
            {
                var danhSachSanPham = _context.Products
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        categoryId = p.CategoryId,
                        hinhAnh = p.ProductImages.Select(p => new
                        {
                            p.Id,
                            p.ProductId,
                            p.HinhAnh
                        })
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách sản phẩm thành công",
                    data = danhSachSanPham,
                    total = danhSachSanPham.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm",
                    error = ex.Message
                });
            }
        }
        [HttpGet("DanhSachDanhMuc")]
        public IActionResult DanhSachDanhMuc()
        {
            try
            {
                var danhSachDanhMuc = _context.Categories
                    .Select(c => new
                    {
                        id = c.Id,
                        tenDanhMuc = c.TenDanhMucSp,
                        moTa = c.MoTaDanhMuc
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách danh mục thành công",
                    data = danhSachDanhMuc,
                    total = danhSachDanhMuc.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách danh mục",
                    error = ex.Message
                });
            }
        }

        [HttpGet("DanhSachSanPhamTheoDanhMuc/{categoryId}")]
        public IActionResult DanhSachSanPhamTheoDanhMuc(int categoryId)
        {
            try
            {
                var sanPhamTheoDanhMuc = _context.Products
                    .Where(p => p.CategoryId == categoryId)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        categoryId = p.CategoryId,
                        hinhAnh = p.ProductImages.Select(p => new
                        {
                            p.Id,
                            p.ProductId,
                            p.HinhAnh
                        })
                    })
                    .ToList();

                if (sanPhamTheoDanhMuc == null || sanPhamTheoDanhMuc.Count == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy sản phẩm nào thuộc danh mục này"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách sản phẩm theo danh mục thành công",
                    data = sanPhamTheoDanhMuc,
                    total = sanPhamTheoDanhMuc.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm theo danh mục",
                    error = ex.Message
                });
            }
        }
        /*[HttpPost("ThemSanPham")]
        public async Task<IActionResult> ThemSanPham([FromBody] ProductCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });

            try
            {
                // Kiểm tra CategoryId có tồn tại không (nếu có truyền)
                if (dto.CategoryId.HasValue)
                {
                    var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId.Value);
                    if (!categoryExists)
                        return BadRequest(new { success = false, message = "Danh mục không tồn tại" });
                }

                var product = new Product
                {
                    TenSp = dto.TenSp,
                    MoTa = dto.MoTa,
                    Gia = dto.Gia,
                    SoLuongConLaiTrongKho = dto.SoLuongConLaiTrongKho,
                    CategoryId = dto.CategoryId, // nullable
                    ImageUrl = dto.ImageUrl
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Thêm sản phẩm thành công",
                    data = new
                    {
                        id = product.Id,
                        tenSp = product.TenSp,
                        moTa = product.MoTa,
                        gia = product.Gia,
                        soLuongConLaiTrongKho = product.SoLuongConLaiTrongKho,
                        categoryId = product.CategoryId,
                        imageUrl = product.ImageUrl
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi thêm sản phẩm", error = ex.Message });
            }
        }*/

        [HttpDelete("XoaSanPham/{id}")]
        public async Task<IActionResult> XoaSanPham(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm" });

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa sản phẩm thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa sản phẩm", error = ex.Message });
            }
        }
        // Thêm vào ProductController

        [HttpGet("ChiTietSanPham/{id}")]
        public async Task<IActionResult> ChiTietSanPham(int id)
        {
            try
            {
                var sanPham = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (sanPham == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy sản phẩm"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy chi tiết sản phẩm thành công",
                    data = new
                    {
                        id = sanPham.Id,
                        tenSp = sanPham.TenSp,
                        moTa = sanPham.MoTa,
                        gia = sanPham.Gia,
                        soLuongConLaiTrongKho = sanPham.SoLuongConLaiTrongKho,
                        conHang = sanPham.SoLuongConLaiTrongKho > 0,
                        categoryId = sanPham.CategoryId,
                        danhMuc = sanPham.Category != null ? new
                        {
                            id = sanPham.Category.Id,
                            tenDanhMuc = sanPham.Category.TenDanhMucSp,
                            moTa = sanPham.Category.MoTaDanhMuc
                        } : null,
                        hinhAnh = sanPham.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy chi tiết sản phẩm",
                    error = ex.Message
                });
            }
        }

        [HttpPost("Them/SuaSanPham")]
        public async Task<IActionResult> Create([FromForm] ProductRequet model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    Success = false,    
                    Message = "Dữ liệu không hợp lệ"
                });
            }

            var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = _context.Products.FirstOrDefault(r => r.Id == model.Id);

                if (product == null)
                {
                    var sp = new Product
                    {
                        TenSp = model.TenSp,
                        CategoryId = model.CategoryId,
                        MoTa = model.MoTa,
                        Gia = model.Gia,
                        SoLuongConLaiTrongKho = model.SoLuongConLaiTrongKho,
                    };
                    _context.Products.Add(sp);
                    _context.SaveChanges();


                    if (model.HinhAnh != null && model.HinhAnh.Any())
                    {
                        foreach (var file in model.HinhAnh)
                        {
                            var url = await _cloudinaryService.UploadImageAsync(file, "SanPham");
                            if (url != null)
                            {
                                _context.ProductImages.Add(new ProductImage
                                {
                                    ProductId = sp.Id,
                                    HinhAnh = url
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                    trans.Commit();
                    return Ok(new
                    {
                        Success = true,
                        Message = "Thêm sản phẩm thành công",
                        //Data = sp
                    });
                }
                else
                {
                    product.TenSp = model.TenSp;
                    product.CategoryId = model.CategoryId;
                    product.Gia = model.Gia;
                    product.SoLuongConLaiTrongKho = model.SoLuongConLaiTrongKho;
                    product.MoTa = model.MoTa;
                    _context.Products.Update(product);
                    _context.SaveChanges();

                    if (model.HinhAnh != null && model.HinhAnh.Any())
                    {
                        foreach (var file in model.HinhAnh)
                        {
                            var url = await _cloudinaryService.UploadImageAsync(file, "SanPham");
                            if (url != null)
                            {
                                _context.ProductImages.Add(new ProductImage
                                {
                                    ProductId = product.Id,
                                    HinhAnh = url
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                    trans.Commit();
                    return Ok(new
                    {
                        Success = true,
                        Message = "Cập nhật sản phẩm thành công",
                        // Data = product
                    });
                }
            }
            catch (Exception ex)
            {
                trans.Rollback();
                return BadRequest(ex.Message);
            }


        }

    }
    public class ProductCreateDto
    {
        public string TenSp { get; set; } = null!;
        public string? MoTa { get; set; }
        public decimal Gia { get; set; }
        public int SoLuongConLaiTrongKho { get; set; }
        public int? CategoryId { get; set; }
        public string? ImageUrl { get; set; }
    }
}
